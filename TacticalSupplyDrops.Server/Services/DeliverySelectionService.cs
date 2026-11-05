
using System.Collections.Generic;
using System.Linq;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;

namespace TacticalSupplyDrops.Server.Services;

[Injectable]
public class DeliverySelectionService(
    StashInventoryService stashInventoryService,
    DeliveryFootprintLimitService footprintLimitService)
{
    public bool TryValidateSelection(
        PmcData profile,
        IEnumerable<string> selectedItemIds,
        out List<Item> selectedItems,
        out int totalFootprint)
    {
        selectedItems = new List<Item>();
        totalFootprint = 0;

        if (profile?.Inventory?.Stash == null ||
            selectedItemIds == null)
        {
            return false;
        }

        string stashId = (string)profile.Inventory.Stash.Value;

        if (string.IsNullOrWhiteSpace(stashId))
        {
            return false;
        }

        List<string> requestedIds = selectedItemIds.ToList();

        if (requestedIds.Count == 0 ||
            requestedIds.Any(string.IsNullOrWhiteSpace) ||
            requestedIds.Distinct().Count() != requestedIds.Count)
        {
            return false;
        }

        List<Item> stashItems =
            stashInventoryService.GetStashItems(profile);

        Dictionary<string, Item> itemsById = stashItems
            .ToDictionary(
                item => (string)item.Id);

        Dictionary<string, List<Item>> childrenByParent = stashItems
            .Where(item => !string.IsNullOrWhiteSpace(item.ParentId))
            .GroupBy(item => item.ParentId)
            .ToDictionary(
                group => group.Key,
                group => group.ToList());

        HashSet<string> requestedIdSet = requestedIds.ToHashSet();

        // Every requested item must exist in the stash.
        if (requestedIds.Any(id => !itemsById.ContainsKey(id)))
        {
            return false;
        }

        // Find the top-level stash container for each requested item.
        // Nested items are valid only when their top-level container
        // is also included in the selection.
        foreach (string requestedId in requestedIds)
        {
            Item currentItem = itemsById[requestedId];

            while (currentItem.ParentId != stashId)
            {
                if (string.IsNullOrWhiteSpace(currentItem.ParentId) ||
                    !itemsById.TryGetValue(
                        currentItem.ParentId,
                        out Item? parentItem))
                {
                    selectedItems.Clear();
                    return false;
                }

                currentItem = parentItem;
            }

            string rootId = (string)currentItem.Id;

            if (!requestedIdSet.Contains(rootId))
            {
                selectedItems.Clear();
                return false;
            }
        }

        // Expand only top-level selected items.
        // This automatically includes their nested contents.
        foreach (string requestedId in requestedIds)
        {
            Item item = itemsById[requestedId];

            if (item.ParentId != stashId)
            {
                continue;
            }

            selectedItems.AddRange(
                item.GetItemWithChildren(childrenByParent));
        }

        // Prevent container contents from being counted more than once.
        selectedItems = selectedItems
            .DistinctBy(item => item.Id)
            .ToList();

        totalFootprint = footprintLimitService.CalculateTotalFootprint(
            selectedItems);

        if (totalFootprint >
            DeliveryFootprintLimitService.MaximumFootprint)
        {
            selectedItems.Clear();
            totalFootprint = 0;
            return false;
        }

        return true;
    }

    public bool TryValidateSelection(
        PmcData profile,
        IEnumerable<string> selectedItemIds,
        Dictionary<string, int> requestedQuantities,
        out List<Item> selectedItems,
        out Dictionary<string, int> validatedQuantities,
        out int totalFootprint)
    {
        selectedItems = new List<Item>();
        validatedQuantities = new Dictionary<string, int>();
        totalFootprint = 0;

        if (requestedQuantities == null ||
            selectedItemIds == null)
        {
            return false;
        }

        List<string> requestedIds = selectedItemIds.ToList();

        if (requestedIds.Count == 0 ||
            requestedIds.Any(string.IsNullOrWhiteSpace) ||
            requestedIds.Distinct().Count() != requestedIds.Count ||
            requestedQuantities.Count != requestedIds.Count ||
            requestedIds.Any(id => !requestedQuantities.ContainsKey(id)))
        {
            return false;
        }

        if (!TryValidateSelection(
            profile,
            requestedIds,
            out selectedItems,
            out totalFootprint))
        {
            return false;
        }

        List<Item> stashItems =
            stashInventoryService.GetStashItems(profile);

        foreach (string id in requestedIds)
        {
            Item? item = stashItems.FirstOrDefault(
                entry => (string)entry.Id == id);

            if (item == null)
            {
                selectedItems.Clear();
                validatedQuantities.Clear();
                totalFootprint = 0;
                return false;
            }

            int availableQuantity = item.GetItemStackSize();
            int requestedQuantity = requestedQuantities[id];

            bool hasContents = selectedItems.Any(
                selectedItem => selectedItem.ParentId == id);

            if (requestedQuantity < 1 ||
                requestedQuantity > availableQuantity ||
                (hasContents && requestedQuantity != availableQuantity))
            {
                selectedItems.Clear();
                validatedQuantities.Clear();
                totalFootprint = 0;
                return false;
            }

            validatedQuantities[id] = requestedQuantity;
        }

        return true;
    }
}
