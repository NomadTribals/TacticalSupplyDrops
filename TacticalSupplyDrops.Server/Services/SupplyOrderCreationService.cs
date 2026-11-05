
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
    

    // Compatibility overload for callers that order entire stacks.
    // Duplicate detection happens inside the main overload before
    // any stash validation is attempted.
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
            return new SupplyOrderCreationResult(false, null, 0);
        }

        List<string> requestedIds = selectedItemIds.ToList();

        requestId ??= System.Guid.NewGuid().ToString("N");

        if (string.IsNullOrWhiteSpace(requestId))
        {
            return new SupplyOrderCreationResult(false, null, 0);
        }

        SemaphoreSlim profileLock =
    await profileTransactionLockService.AcquireAsync(
        profile.Id.Value,
        cancellationToken);

        try
        {
            // IMPORTANT: check duplicates before validating stash items.
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
                return new SupplyOrderCreationResult(false, null, 0);
            }

            // An empty quantity dictionary means this is an older caller
            // requesting complete stacks.
            if (requestedQuantities.Count == 0)
            {
                if (!selectionService.TryValidateSelection(
                    profile,
                    requestedIds,
                    out var fullStackItems,
                    out _))
                {
                    return new SupplyOrderCreationResult(false, null, 0);
                }

                foreach (string id in requestedIds)
                {
                    Item? item = fullStackItems.FirstOrDefault(
                        entry => (string)entry.Id == id);

                    if (item == null)
                    {
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
                return new SupplyOrderCreationResult(
                    false,
                    null,
                    totalFootprint);
            }

            var itemDetails = itemDetailsService.GetItemDetails(selectedItems);

            foreach (var details in itemDetails)
            {
                if (validatedQuantities.TryGetValue(
                    details.ItemId,
                    out int requestedQuantity))
                {
                    details.Quantity = requestedQuantity;
                }
            }

            int totalPrice = pricingService.CalculateSupplyOrderPrice(
                itemDetails,
                reputationTier);

            List<SupplyOrderItem> orderItems = new();

            foreach (Item item in selectedItems)
            {
                string itemId = (string)item.Id;

                Item? copiedItem = cloner.Clone(item);

                if (copiedItem == null)
                {
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

            SupplyOrder preparedOrder = orderService.CreateOrder(
                orderItems,
                totalPrice);

            preparedOrder.RequestId = requestId;
            preparedOrder.TotalFootprint = totalFootprint;

            if (!paymentProcessor.TryCreatePaymentPlan(
                profile,
                totalPrice,
                out var paymentPlan))
            {
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
