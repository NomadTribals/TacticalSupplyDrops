using SPTarkov.DI.Annotations;
using System.Collections.Generic;
using System.Linq;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;

namespace TacticalSupplyDrops.Server.Services;

public interface IStashInventoryProvider
{
    List<Item> GetStashItems(PmcData profile);
}
[Injectable]
public class StashInventoryService : IStashInventoryProvider
{
    public List<Item> GetStashItems(PmcData profile)
    {
        if (profile?.Inventory?.Stash == null)
        {
            return new List<Item>();
        }

        // Get the stash container ID.
        string? stashId = (string)profile.Inventory.Stash.Value;

        if (string.IsNullOrWhiteSpace(stashId))
        {
            return new List<Item>();
        }

        // Get all inventory items.
        List<Item>? allItems = profile.Inventory.Items;

        if (allItems == null || allItems.Count == 0)
        {
            return new List<Item>();
        }

        // Find items directly inside the stash.
        List<Item> stashRootItems = allItems
            .Where(item => item.ParentId == stashId)
            .ToList();

        // Build SPT's parent-ID lookup once.
        Dictionary<string, List<Item>> childrenByParent =
            allItems.CreateParentIdLookupCache(out _);

        // Collect stash items and their nested contents.
        List<Item> stashItems = new();

        foreach (Item stashRootItem in stashRootItems)
        {
            stashItems.AddRange(
                stashRootItem.GetItemWithChildren(childrenByParent));
        }

        return stashItems
            .DistinctBy(item => item.Id)
            .ToList();
    }
}
