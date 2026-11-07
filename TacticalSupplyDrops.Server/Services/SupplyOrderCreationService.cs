
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.ItemEvent;
using SPTarkov.Server.Core.Utils.Cloners;
using TacticalSupplyDrops.Server.Models;

namespace TacticalSupplyDrops.Server.Services;

public record SupplyOrderCreationResult(
    bool Success,
    SupplyOrder? Order,
    int TotalFootprint,
    bool IsDuplicate = false);

[Injectable]
public class SupplyOrderCreationService(
    DeliverySelectionService selectionService,
    StashItemDetailsService itemDetailsService,
    SupplyOrderPricingService pricingService,
    SupplyOrderService orderService,
    SupplyOrderPaymentProcessor paymentProcessor,
    SupplyOrderItemReservationService reservationService,
    SupplyOrderRepository orderRepository,
    ICloner cloner,
    ProfileTransactionLockService profileTransactionLockService)
{
    private static void LogFailure(string stage, string reason)
    {
        Console.WriteLine(
            $"[Tactical Supply Drops] ORDER FAILURE [{stage}]: {reason}");
    }

    // Compatibility overload for callers that order entire stacks.
    public Task<SupplyOrderCreationResult> TryCreateOrderAsync(
        PmcData profile,
        MongoId sessionId,
        ItemEventRouterResponse output,
        IEnumerable<string> selectedItemIds,
        FenceReputationTier reputationTier,
        CancellationToken cancellationToken = default,
        string? requestId = null)
    {
        return TryCreateOrderAsync(
            profile,
            sessionId,
            output,
            selectedItemIds,
            new Dictionary<string, int>(),
            reputationTier,
            cancellationToken,
            requestId);
    }

    public async Task<SupplyOrderCreationResult> TryCreateOrderAsync(
        PmcData profile,
        MongoId sessionId,
        ItemEventRouterResponse output,
        IEnumerable<string> selectedItemIds,
        Dictionary<string, int> requestedQuantities,
        FenceReputationTier reputationTier,
        CancellationToken cancellationToken = default,
        string? requestId = null)
    {
        if (profile?.Id == null ||
            selectedItemIds == null ||
            requestedQuantities == null)
        {
            LogFailure(
                "INITIAL VALIDATION",
                "Profile ID, selected item IDs, or requested quantities were null.");

            return new SupplyOrderCreationResult(false, null, 0);
        }

        List<string> requestedIds = selectedItemIds.ToList();

        requestId ??= Guid.NewGuid().ToString("N");

        if (string.IsNullOrWhiteSpace(requestId))
        {
            LogFailure("REQUEST ID", "Request ID was empty.");

            return new SupplyOrderCreationResult(false, null, 0);
        }

        SemaphoreSlim profileLock =
            await profileTransactionLockService.AcquireAsync(
                profile.Id.Value,
                cancellationToken);

        try
        {
            SupplyOrder? existingOrder =
                await orderRepository.GetOrderByRequestIdAsync(
                    profile.Id.Value,
                    requestId,
                    cancellationToken);

            if (existingOrder != null)
            {

                return new SupplyOrderCreationResult(
                    true,
                    existingOrder,
                    existingOrder.TotalFootprint,
                    true);
            }

            if (profile.Inventory?.Items == null ||
                output?.ProfileChanges == null ||
                !output.ProfileChanges.TryGetValue(
                    sessionId,
                    out var profileChange) ||
                profileChange?.Items == null ||
                profileChange.Items.NewItems == null ||
                profileChange.Items.ChangedItems == null ||
                profileChange.Items.DeletedItems == null)
            {
                LogFailure(
                    "PROFILE CHANGES",
                    "Inventory or required profile-change collections were missing.");

                return new SupplyOrderCreationResult(false, null, 0);
            }

            // Older callers request complete stacks.
            if (requestedQuantities.Count == 0)
            {
                if (!selectionService.TryValidateSelection(
                    profile,
                    requestedIds,
                    out var fullStackItems,
                    out _))
                {
                    LogFailure(
                        "FULL STACK SELECTION",
                        "The initial full-stack selection validation failed.");

                    return new SupplyOrderCreationResult(false, null, 0);
                }

                foreach (string id in requestedIds)
                {
                    Item? item = fullStackItems.FirstOrDefault(
                        entry => (string)entry.Id == id);

                    if (item == null)
                    {
                        LogFailure(
                            "FULL STACK LOOKUP",
                            $"Selected item {id} could not be found.");

                        return new SupplyOrderCreationResult(false, null, 0);
                    }

                    requestedQuantities[id] =
                        item.Upd?.StackObjectsCount is double count
                            ? (int)count
                            : 1;
                }
            }

            if (!selectionService.TryValidateSelection(
                profile,
                requestedIds,
                requestedQuantities,
                out var selectedItems,
                out var validatedQuantities,
                out int totalFootprint))
            {
                LogFailure(
                    "SELECTION VALIDATION",
                    $"Selection validation rejected the request. " +
                    $"Footprint reported: {totalFootprint}.");

                return new SupplyOrderCreationResult(
                    false,
                    null,
                    totalFootprint);
            }

            List<StashItemDetails> itemDetails;

            try
            {
                itemDetails = itemDetailsService.GetItemDetails(selectedItems);
            }
            catch (Exception exception)
            {
                LogFailure(
                    "ITEM DETAILS",
                    $"Exception retrieving item details: {exception}");

                throw;
            }

            foreach (var details in itemDetails)
            {
                if (validatedQuantities.TryGetValue(
                    details.ItemId,
                    out int requestedQuantity))
                {
                    details.Quantity = requestedQuantity;
                }

            }

            int totalPrice;

            try
            {
                totalPrice = pricingService.CalculateSupplyOrderPrice(
                    itemDetails,
                    reputationTier);
            }
            catch (Exception exception)
            {
                LogFailure(
                    "PRICING",
                    $"Exception calculating order price: {exception}");

                throw;
            }

            List<SupplyOrderItem> orderItems = new();

            foreach (Item item in selectedItems)
            {
                string itemId = (string)item.Id;

                Item? copiedItem;

                try
                {
                    copiedItem = cloner.Clone(item);
                }
                catch (Exception exception)
                {
                    LogFailure(
                        "ITEM CLONING",
                        $"Exception cloning item {itemId}, " +
                        $"template {(string)item.Template}: {exception}");

                    throw;
                }

                if (copiedItem == null)
                {
                    LogFailure(
                        "ITEM CLONING",
                        $"Cloner returned null for item {itemId}, " +
                        $"template {(string)item.Template}.");

                    return new SupplyOrderCreationResult(
                        false,
                        null,
                        totalFootprint);
                }

                int quantity = item.Upd?.StackObjectsCount is double count
                    ? (int)count
                    : 1;

                if (validatedQuantities.TryGetValue(
                    itemId,
                    out int requestedQuantity))
                {
                    quantity = requestedQuantity;

                    int originalQuantity =
                        item.Upd?.StackObjectsCount is double stackCount
                            ? (int)stackCount
                            : 1;

                    if (requestedQuantity < originalQuantity)
                    {
                        if (copiedItem.Upd == null)
                        {
                            LogFailure(
                                "STACK QUANTITY",
                                $"Item {itemId} has no Upd data, " +
                                "so its partial stack quantity cannot be set.");

                            return new SupplyOrderCreationResult(
                                false,
                                null,
                                totalFootprint);
                        }

                        copiedItem.Upd.StackObjectsCount =
                            requestedQuantity;
                    }
                }

                orderItems.Add(new SupplyOrderItem(
                    itemId,
                    quantity)
                {
                    Items = new List<Item> { copiedItem }
                });
            }

            SupplyOrder preparedOrder;

            try
            {
                preparedOrder = orderService.CreateOrder(
                    orderItems,
                    totalPrice);
            }
            catch (Exception exception)
            {
                LogFailure(
                    "ORDER PREPARATION",
                    $"Exception creating the order: {exception}");

                throw;
            }

            preparedOrder.RequestId = requestId;
            preparedOrder.TotalFootprint = totalFootprint;

            if (!paymentProcessor.TryCreatePaymentPlan(
                profile,
                totalPrice,
                out var paymentPlan))
            {
                LogFailure(
                    "PAYMENT PLAN",
                    $"Could not create payment plan for {totalPrice} roubles.");

                return new SupplyOrderCreationResult(
                    false,
                    null,
                    totalFootprint);
            }

            List<Item> originalInventory = profile.Inventory.Items
                .Select(item => cloner.Clone(item))
                .ToList();

            var originalInsurance = profile.InsuredItems?
                .Select(item => cloner.Clone(item))
                .ToList();

            var originalNewItems = profileChange.Items.NewItems
                .Select(item => cloner.Clone(item))
                .ToList();

            var originalChangedItems = profileChange.Items.ChangedItems
                .Select(item => cloner.Clone(item))
                .ToList();

            var originalDeletedItems = profileChange.Items.DeletedItems
                .Select(item => cloner.Clone(item))
                .ToList();

            void RestoreOriginalState()
            {
                profile.Inventory.Items = originalInventory;
                profile.InsuredItems = originalInsurance;

                profileChange.Items.NewItems = originalNewItems;
                profileChange.Items.ChangedItems = originalChangedItems;
                profileChange.Items.DeletedItems = originalDeletedItems;
            }

            try
            {

                if (!reservationService.TryReserveItems(
                    profile,
                    sessionId,
                    output,
                    selectedItems,
                    validatedQuantities))
                {
                    LogFailure(
                        "ITEM RESERVATION",
                        "The reservation service rejected the selected items.");

                    RestoreOriginalState();

                    return new SupplyOrderCreationResult(
                        false,
                        null,
                        totalFootprint);
                }

                if (!paymentProcessor.TryProcessPayment(
                    profile,
                    sessionId,
                    output,
                    paymentPlan))
                {
                    LogFailure(
                        "PAYMENT PROCESSING",
                        "The payment deduction service rejected the payment.");

                    RestoreOriginalState();

                    return new SupplyOrderCreationResult(
                        false,
                        null,
                        totalFootprint);
                }

                await orderRepository.AddOrderAsync(
                    profile.Id.Value,
                    preparedOrder,
                    cancellationToken);

                return new SupplyOrderCreationResult(
                    true,
                    preparedOrder,
                    totalFootprint);
            }
            catch
            {
                RestoreOriginalState();
                throw;
            }
        }
        finally
        {
            profileLock.Release();
        }
    }
}
