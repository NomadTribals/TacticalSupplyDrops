
using System;
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
    private static void Log(string message)
    {
        Console.WriteLine(
            $"[Tactical Supply Drops] SELECTION DIAGNOSTIC: {message}");
    }

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
            Log("Rejected: profile stash or selected IDs were null.");
            return false;
        }

        string stashId = (string)profile.Inventory.Stash.Value;

        if (string.IsNullOrWhiteSpace(stashId))
        {
            Log("Rejected: stash ID was empty.");
            return false;
        }

        List<string> requestedIds = selectedItemIds.ToList();

        if (requestedIds.Count == 0 ||
            requestedIds.Any(string.IsNullOrWhiteSpace) ||
            requestedIds.Distinct().Count() != requestedIds.Count)
        {
            Log("Rejected: selection was empty, invalid, or contained duplicates.");
            return false;
        }

        List<Item> stashItems =
            stashInventoryService.GetStashItems(profile);

        Dictionary<string, Item> itemsById = stashItems
            .ToDictionary(item => (string)item.Id);

        Dictionary<string, List<Item>> childrenByParent = stashItems
            .Where(item => !string.IsNullOrWhiteSpace(item.ParentId))
            .GroupBy(item => item.ParentId)
            .ToDictionary(
                group => group.Key,
                group => group.ToList());

        Log(
            $"Starting validation. Stash items: {stashItems.Count}. " +
            $"Requested IDs: {string.Join(", ", requestedIds)}.");

        // Every requested item must exist in the stash.
        foreach (string id in requestedIds)
        {
            if (!itemsById.TryGetValue(id, out Item? item))
            {
                Log($"Rejected: item {id} was not found in the stash.");
                return false;
            }

            Log(
                $"Found item {id}. Template: {item.Template}. " +
                $"Parent: {item.ParentId ?? "(null)"}.");
        }

        // Verify that every selected item belongs to the stash.
        // Unlike the previous implementation, its parent container
        // does NOT need to be selected.
        foreach (string requestedId in requestedIds)
        {
            Item currentItem = itemsById[requestedId];

            while (currentItem.ParentId != stashId)
            {
                if (string.IsNullOrWhiteSpace(currentItem.ParentId))
                {
                    Log(
                        $"Rejected: item {requestedId} has no valid path " +
                        "back to the stash.");

                    selectedItems.Clear();
                    return false;
                }

                if (!itemsById.TryGetValue(
                    currentItem.ParentId,
                    out Item? parentItem))
                {
                    Log(
                        $"Rejected: parent {currentItem.ParentId} of item " +
                        $"{currentItem.Id} was not found.");

                    selectedItems.Clear();
                    return false;
                }

                currentItem = parentItem;
            }

            Log(
                $"Verified item {requestedId} belongs to the stash.");
        }

        // Expand every requested item independently.
        // Selecting a container includes its descendants.
        // Selecting an individual nested item does not include
        // its parent or unrelated contents.
        foreach (string requestedId in requestedIds)
        {
            Item item = itemsById[requestedId];

            List<Item> itemAndChildren =
                item.GetItemWithChildren(childrenByParent);

            Log(
                $"Expanded selection {requestedId}: " +
                $"{itemAndChildren.Count} item(s).");

            selectedItems.AddRange(itemAndChildren);
        }

        // Prevent duplicate items when both a container and one
        // of its contents have been selected.
        selectedItems = selectedItems
            .DistinctBy(item => item.Id)
            .ToList();

        Log(
            $"Total unique selected items: {selectedItems.Count}.");

        totalFootprint = footprintLimitService.CalculateTotalFootprint(
            selectedItems);

        Log($"Calculated footprint: {totalFootprint}.");

        if (totalFootprint >
            DeliveryFootprintLimitService.MaximumFootprint)
        {
            Log(
                $"Rejected: footprint {totalFootprint} exceeds maximum " +
                $"{DeliveryFootprintLimitService.MaximumFootprint}.");

            selectedItems.Clear();
            totalFootprint = 0;
            return false;
        }

        Log("Base selection validation succeeded.");

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
            Log("Quantity validation rejected: IDs or quantities were null.");
            return false;
        }

        List<string> requestedIds = selectedItemIds.ToList();

        if (requestedIds.Count == 0 ||
            requestedIds.Any(string.IsNullOrWhiteSpace) ||
            requestedIds.Distinct().Count() != requestedIds.Count ||
            requestedQuantities.Count != requestedIds.Count ||
            requestedIds.Any(id => !requestedQuantities.ContainsKey(id)))
        {
            Log("Quantity validation rejected: invalid ID or quantity mapping.");
            return false;
        }

        if (!TryValidateSelection(
            profile,
            requestedIds,
            out selectedItems,
            out totalFootprint))
        {
            Log("Quantity validation rejected because base validation failed.");
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
                Log($"Quantity validation rejected: item {id} was not found.");

                selectedItems.Clear();
                validatedQuantities.Clear();
                totalFootprint = 0;
                return false;
            }

            int availableQuantity = item.GetItemStackSize();
            int requestedQuantity = requestedQuantities[id];

            bool hasContents = selectedItems.Any(
                selectedItem => selectedItem.ParentId == id);

            Log(
                $"Quantity check: {id}. Requested: {requestedQuantity}. " +
                $"Available: {availableQuantity}. Has contents: {hasContents}.");

            if (requestedQuantity < 1 ||
                requestedQuantity > availableQuantity ||
                (hasContents && requestedQuantity != availableQuantity))
            {
                Log(
                    $"Quantity validation rejected item {id}. " +
                    $"Requested: {requestedQuantity}, available: " +
                    $"{availableQuantity}, has contents: {hasContents}.");

                selectedItems.Clear();
                validatedQuantities.Clear();
                totalFootprint = 0;
                return false;
            }

            validatedQuantities[id] = requestedQuantity;
        }

        Log("Quantity validation succeeded.");

        return true;
    }
}
