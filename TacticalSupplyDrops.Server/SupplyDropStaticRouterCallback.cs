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
    public double UnitWeight { get; set; }
    public bool IsLooseAmmo { get; set; }
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
    JsonUtil jsonUtil,
    ProcurementRepository procurementRepository,
    TraderProcurementService traderProcurement,
    CourierConversionService courierConversion)
{
    public async ValueTask<ItemEventRouterResponse> HandleCourierConversion(string url, PmcData profile,
        SupplyDropCourierConversionRequest request, MongoId sessionId, ItemEventRouterResponse output, CancellationToken cancellationToken)
    {
        string? error = await courierConversion.Convert(profile, sessionId, output, request.OrderIds,
            request.ExpectedAdditionalFee, fenceReputationService.GetReputationTier(profile), cancellationToken);
        if (error != null) output.Warnings.Add(new Warning { ErrorMessage = error });
        return output;
    }

    public ValueTask<string> HandleTest(
        string url,
        MongoId sessionId)
    {
        logger.Success("Tactical Supply Drops test route received a request!");

        return ValueTask.FromResult(
            "{\"success\":true,\"message\":\"Tactical Supply Drops test route is working!\"}");
    }



    public async ValueTask<string> HandleGetStash(
        string url,
        MongoId sessionId)
    {
        PmcData? profile = profileHelper.GetPmcProfile(sessionId);

        if (profile == null)
        {
            logger.Warning("Tactical Supply Drops could not retrieve the PMC profile.");

            return (
                "{\"success\":false,\"message\":\"PMC profile not found.\",\"items\":[]}");
        }

        if (profile.Inventory?.Stash == null)
        {
            logger.Warning("Tactical Supply Drops could not identify the stash container.");

            return (
                "{\"success\":false,\"message\":\"Stash container not found.\",\"items\":[]}");
        }

        string stashId = (string)profile.Inventory.Stash.Value;

        if (string.IsNullOrWhiteSpace(stashId))
        {
            logger.Warning("Tactical Supply Drops received an invalid stash container ID.");

            return (
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
        // TEMPORARY DIAGNOSTICS: investigate missing rigs.
        int armorCount = itemDetails.Count(item => item.Category == "ARMOR");
        int otherCount = itemDetails.Count(item => item.Category == "OTHER");

        logger.Info(
            $"TSD DIAGNOSTICS: Total stash items: {stashItems.Count}, ARMOR: {armorCount}, OTHER: {otherCount}.");

        foreach (StashItemDetails detail in itemDetails.Where(item =>
                     item.Name.Contains("Thunderbolt", StringComparison.OrdinalIgnoreCase) ||
                     item.Name.Contains("Hercules", StringComparison.OrdinalIgnoreCase)))
        {
            Item? diagnosticParent = null;

            Item? diagnosticItem = stashItems.FirstOrDefault(item =>
                (string)item.Id == detail.ItemId);

            if (diagnosticItem != null &&
                !string.IsNullOrWhiteSpace(diagnosticItem.ParentId))
            {
                itemsById.TryGetValue(diagnosticItem.ParentId, out diagnosticParent);
            }

            bool isInstalled = diagnosticItem != null &&
                stashItemDetailsProvider.IsInstalledInEquipmentSlot(
                    diagnosticItem, diagnosticParent);

            logger.Info(
                $"TSD RIG DIAGNOSTIC: {detail.Name} | Category: {detail.Category} | Installed: {isInstalled} | Item ID: {detail.ItemId}");
        }

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
                Category = details.Category,
                IsLooseAmmo = details.IsLooseAmmo,
                UnitWeight = details.UnitWeight
            });
        }

        logger.Info(
            $"Tactical Supply Drops retrieved {responseItems.Count} selectable stash items. Hidden installed components: {hiddenInstalledItems}.");

        return (
            jsonUtil.Serialize(new
            {
                success = true,
                stashRootId = profile.Inventory?.Stash?.ToString(),
                balanceRoubles = traderProcurement.GetBalance(profile),
                wallet = traderProcurement.GetWallet(profile),
                deliveryPricing = new {
                    standardBaseFee = SupplyOrderPricingService.StandardBaseFee,
                    standardRatePerKg = SupplyOrderPricingService.StandardRatePerKg,
                    droneBaseFee = SupplyOrderPricingService.DroneBaseFee,
                    droneRatePerKg = SupplyOrderPricingService.DroneRatePerKg,
                    droneMaxWeightKg = DroneDeliveryPolicy.MaximumWeightKg,
                    fenceMultiplier = new DeliveryPricingService().GetDeliveryFee(fenceReputationService.GetReputationTier(profile), 0, 10000) / 10000m
                },
                procurementCargo = (await procurementRepository.GetPurchasesAsync(profile.Id!.Value))
                    .Where(p => p.Status == "Staged")
                    .SelectMany(p => p.CargoSelections ?? new())
                    .GroupBy(s => s.itemId)
                    .Select(g => new ProcurementCargoSelection { itemId = g.Key, quantity = (int)Math.Min(int.MaxValue, g.Sum(s => (long)s.quantity)) })
                    .Where(s => responseItems.Any(i => i.Id == s.itemId))
                    .ToList(),
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
            request.RequestId, request.DeliveryMethod);

        if (!result.Success || result.Order == null)
        {
            logger.Warning("Tactical Supply Drops order creation failed.");

            output.Warnings.Add(new Warning
            {
                ErrorMessage = result.Error ?? "The supply order could not be created."
            });

            return output;
        }

        await traderProcurement.CommitCargoAsync(profile, result.Order.OrderId, request.SelectedItemQuantities, cancellationToken);

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
