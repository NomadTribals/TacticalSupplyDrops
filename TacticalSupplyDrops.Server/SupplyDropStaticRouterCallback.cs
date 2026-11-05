using TacticalSupplyDrops.Server.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Request;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.ItemEvent;
using SPTarkov.Server.Core.Utils;
using TacticalSupplyDrops.Server.Services;

namespace TacticalSupplyDrops.Server;

public class SupplyDropStashItem
{
    public string Id { get; set; } = string.Empty;
    public string TemplateId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ParentId { get; set; }
    public string? SlotId { get; set; }
    public int StackCount { get; set; }
    public int BaseValue { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string IconUrl { get; set; } = string.Empty;
    public string Tag { get; set; } = string.Empty;
    public string Category { get; set; } = "OTHER";
}

[Injectable]
public class SupplyDropStaticRouterCallback(
    ISptLogger<SupplyDropStaticRouterCallback> logger,
    FenceReputationService fenceReputationService,
    SupplyOrderCreationService orderCreationService,
    SupplyOrderRepository orderRepository,
    ProfileHelper profileHelper,
    IStashInventoryProvider stashInventoryProvider,
    IStashItemDetailsProvider stashItemDetailsProvider,
    JsonUtil jsonUtil)
{
    public ValueTask<string> HandleTest(
        string url,
        MongoId sessionId)
    {
        logger.Success("Tactical Supply Drops test route received a request!");

        return ValueTask.FromResult(
            "{\"success\":true,\"message\":\"Tactical Supply Drops test route is working!\"}");
    }



    public ValueTask<string> HandleGetStash(
        string url,
        MongoId sessionId)
    {
        PmcData? profile = profileHelper.GetPmcProfile(sessionId);

        if (profile == null)
        {
            logger.Warning("Tactical Supply Drops could not retrieve the PMC profile.");

            return ValueTask.FromResult(
                "{\"success\":false,\"message\":\"PMC profile not found.\",\"items\":[]}");
        }

        if (profile.Inventory?.Stash == null)
        {
            logger.Warning("Tactical Supply Drops could not identify the stash container.");

            return ValueTask.FromResult(
                "{\"success\":false,\"message\":\"Stash container not found.\",\"items\":[]}");
        }

        string stashId = (string)profile.Inventory.Stash.Value;

        if (string.IsNullOrWhiteSpace(stashId))
        {
            logger.Warning("Tactical Supply Drops received an invalid stash container ID.");

            return ValueTask.FromResult(
                "{\"success\":false,\"message\":\"Invalid stash container ID.\",\"items\":[]}");
        }

        // Retrieve all stash items, including nested container contents.
        List<Item> stashItems =
            stashInventoryProvider.GetStashItems(profile);

        // Build a lookup so each item can find its parent.
        Dictionary<string, Item> itemsById =
            stashItems.ToDictionary(item => (string)item.Id);

        // Generate details for every item.
        List<StashItemDetails> itemDetails =
            stashItemDetailsProvider.GetItemDetails(stashItems);

        Dictionary<string, StashItemDetails> detailsById =
            itemDetails.ToDictionary(details => details.ItemId);

        List<SupplyDropStashItem> responseItems = new();

        int hiddenInstalledItems = 0;

        foreach (Item item in stashItems)
        {
            string itemId = (string)item.Id;

            // Find the item's parent, if it has one.
            Item? parent = null;

            if (!string.IsNullOrWhiteSpace(item.ParentId))
            {
                itemsById.TryGetValue(item.ParentId, out parent);
            }

            // Do not list components installed in equipment slots.
            if (stashItemDetailsProvider.IsInstalledInEquipmentSlot(item, parent))
            {
                hiddenInstalledItems++;
                continue;
            }

            if (!detailsById.TryGetValue(itemId, out var details))
            {
                continue;
            }

            responseItems.Add(new SupplyDropStashItem
            {
                Id = itemId,
                TemplateId = (string)item.Template,
                Name = details.Name,
                ParentId = item.ParentId,
                SlotId = item.SlotId,
                StackCount = details.Quantity,
                BaseValue = details.BaseValue,
                Width = details.Width,
                Height = details.Height,
                IconUrl = details.IconUrl,
                Tag = details.Tag,
                Category = details.Category
            });
        }

        logger.Info(
            $"Tactical Supply Drops retrieved {responseItems.Count} selectable stash items. Hidden installed components: {hiddenInstalledItems}.");

        return ValueTask.FromResult(
            jsonUtil.Serialize(new
            {
                success = true,
                items = responseItems
            }, typeof(object)));
    }



    public async ValueTask<string> HandleGetOrder(
        string url,
        SupplyDropOrderLookupRequest request,
        MongoId sessionId,
        CancellationToken cancellationToken)
    {
        if (request == null ||
            string.IsNullOrWhiteSpace(request.RequestId))
        {
            return jsonUtil.Serialize(new
            {
                success = false,
                message = "A valid request ID is required.",
                order = (SupplyOrder?)null
            }, typeof(object));
        }

        PmcData? profile = profileHelper.GetPmcProfile(sessionId);

        if (profile == null)
        {
            logger.Warning(
                "Tactical Supply Drops could not retrieve the PMC profile for an order lookup.");

            return jsonUtil.Serialize(new
            {
                success = false,
                message = "PMC profile not found.",
                order = (SupplyOrder?)null
            }, typeof(object));
        }

        SupplyOrder? order =
            await orderRepository.GetOrderByRequestIdAsync(
                profile.Id.Value,
                request.RequestId,
                cancellationToken);

        if (order == null)
        {
            logger.Warning(
                $"Tactical Supply Drops could not find order for request: {request.RequestId}");

            return jsonUtil.Serialize(new
            {
                success = false,
                message = "Supply order not found.",
                order = (SupplyOrder?)null
            }, typeof(object));
        }

        logger.Success(
            $"Tactical Supply Drops retrieved order {order.OrderId} with {order.Items.Count} cargo entries.");

        return jsonUtil.Serialize(new
        {
            success = true,
            order = order
        }, typeof(object));
    }

    public async ValueTask<ItemEventRouterResponse> HandleCreateOrder(
        string url,
        PmcData profile,
        SupplyDropOrderRequest request,
        MongoId sessionId,
        ItemEventRouterResponse output,
        CancellationToken cancellationToken)
    {
        if (request == null ||
            string.IsNullOrWhiteSpace(request.RequestId) ||
            request.SelectedItemIds == null ||
            request.SelectedItemIds.Count == 0 ||
            request.SelectedItemQuantities == null ||
            request.SelectedItemQuantities.Count == 0)
        {
            output.Warnings.Add(new Warning
            {
                ErrorMessage = "A valid request ID, selected items, and item quantities are required."
            });

            return output;
        }

        if (profile == null)
        {
            output.Warnings.Add(new Warning
            {
                ErrorMessage = "Unable to retrieve the player's PMC profile."
            });

            return output;
        }

        var reputationTier =
            fenceReputationService.GetReputationTier(profile);

        var result = await orderCreationService.TryCreateOrderAsync(
            profile,
            sessionId,
            output,
            request.SelectedItemIds,
            request.SelectedItemQuantities,
            reputationTier,
            cancellationToken,
            request.RequestId);

        if (!result.Success || result.Order == null)
        {
            logger.Warning("Tactical Supply Drops order creation failed.");

            output.Warnings.Add(new Warning
            {
                ErrorMessage = "The supply order could not be created."
            });

            return output;
        }

        if (result.IsDuplicate)
        {
            logger.Info(
                $"Tactical Supply Drops duplicate request received: {request.RequestId}");

            return output;
        }

        logger.Success(
            $"Tactical Supply Drops order created: {result.Order.OrderId}");

        return output;
    }
}