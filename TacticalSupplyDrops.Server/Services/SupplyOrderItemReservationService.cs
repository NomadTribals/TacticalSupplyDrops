
using System.Collections.Generic;
using System.Linq;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.ItemEvent;

namespace TacticalSupplyDrops.Server.Services;

[Injectable]
public class SupplyOrderItemReservationService(
    StashInventoryService stashInventoryService,
    IInventoryItemRemover itemRemover)
{
    public bool TryReserveItems(
        PmcData profile,
        MongoId sessionId,
        ItemEventRouterResponse output,
        IEnumerable<Item> selectedItems)
    {
        if (selectedItems == null)
        {
            return false;
        }

        // Preserve the existing full-stack behavior for older callers.
        Dictionary<string, int> quantities = selectedItems
            .Where(item => item != null)
            .ToDictionary(
                item => (string)item.Id,
                item => item.GetItemStackSize());

        return TryReserveItems(
            profile,
            sessionId,
            output,
            selectedItems,
            quantities);
    }

    public bool TryReserveItems(
        PmcData profile,
        MongoId sessionId,
        ItemEventRouterResponse output,
        IEnumerable<Item> selectedItems,
        Dictionary<string, int> requestedQuantities)
    {
        if (profile == null ||
            output == null ||
            selectedItems == null ||
            requestedQuantities == null)
        {
            return false;
        }

        List<Item> items = selectedItems
            .DistinctBy(item => item.Id)
            .ToList();

        if (items.Count == 0)
        {
            return false;
        }

        var stashItems = stashInventoryService.GetStashItems(profile);

        var stashIds = stashItems
            .Select(item => (string)item.Id)
            .ToHashSet();

        if (items.Any(item => !stashIds.Contains((string)item.Id)))
        {
            return false;
        }

        var selectedIds = items
            .Select(item => (string)item.Id)
            .ToHashSet();

        var removalRoots = items
            .Where(item =>
                string.IsNullOrWhiteSpace(item.ParentId) ||
                !selectedIds.Contains(item.ParentId))
            .ToList();

        if (removalRoots.Count == 0)
        {
            return false;
        }

        // Validate every removal before modifying the inventory.
        foreach (Item root in removalRoots)
        {
            string id = (string)root.Id;

            if (!requestedQuantities.TryGetValue(id, out int quantity))
            {
                return false;
            }

            int available = root.GetItemStackSize();

            bool hasContents = stashItems.Any(
                child => child.ParentId == id);

            if (quantity < 1 ||
                quantity > available ||
                (hasContents && quantity != available))
            {
                return false;
            }
        }

        // Apply removals through SPT's inventory helper.
        foreach (Item root in removalRoots)
        {
            string id = (string)root.Id;
            int quantity = requestedQuantities[id];
            int available = root.GetItemStackSize();

            if (quantity < available)
            {
                itemRemover.RemoveItemByCount(
                    profile,
                    root.Id,
                    quantity,
                    sessionId,
                    output);
            }
            else
            {
                itemRemover.RemoveItem(
                    profile,
                    root.Id,
                    sessionId,
                    output);
            }
        }

        return true;
    }
}
