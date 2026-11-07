
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Inventory;
using SPTarkov.Server.Core.Utils.Cloners;
using System;
using System.Collections.Generic;
using System.Linq;
using TacticalSupplyDrops.Server.Models;

namespace TacticalSupplyDrops.Server.Services;

[Injectable]
public class SupplyDropCargoBuilder(
    ItemHelper itemHelper,
    ICloner cloner)
{
    public Item CreateCrate()
    {
        return new Item
        {
            Id = new MongoId(),
            Template = ItemTpl.LOOTCONTAINER_AIRDROP_COMMON_SUPPLY_CRATE,
            Upd = new Upd
            {
                SpawnedInSession = true,
                StackObjectsCount = 1
            }
        };
    }

    public List<Item> BuildCargo(SupplyOrder order)
    {
        ArgumentNullException.ThrowIfNull(order);

        Item crate = CreateCrate();

        List<Item> savedItems = order.Items
     .Where(entry => entry.Items != null)
     .SelectMany(entry => entry.Items!)
     .Where(item => item != null)
     .Select(item => item!)
     .GroupBy(item => item.Id.ToString())
     .Select(group => group.First())
     .ToList();

        if (savedItems.Count == 0)
            throw new InvalidOperationException(
                "The supply order contains no cargo items.");

        Dictionary<string, Item> itemsById = savedItems
            .ToDictionary(item => item.Id.ToString());

        Dictionary<string, List<Item>> childrenByParent = new();

        foreach (Item item in savedItems)
        {
            string? parentId = item.ParentId?.ToString();

            if (string.IsNullOrWhiteSpace(parentId))
                continue;

            if (!childrenByParent.TryGetValue(parentId, out var children))
            {
                children = new List<Item>();
                childrenByParent[parentId] = children;
            }

            children.Add(item);
        }

        List<Item> roots = savedItems
            .Where(item =>
                string.IsNullOrWhiteSpace(item.ParentId?.ToString()) ||
                !itemsById.ContainsKey(item.ParentId!.ToString()))
            .ToList();

        if (roots.Count == 0)
            throw new InvalidOperationException(
                "The supply order contains no valid cargo roots.");

        int[,] containerMapping =
            itemHelper.GetContainerMapping(crate.Template);

        List<Item> cargo = new();

        foreach (Item root in roots)
        {
            List<Item> originalGroup = GetItemGroup(
                root,
                childrenByParent);

            List<Item> group = originalGroup
                .Select(item => cloner.Clone(item))
                .ToList();

            Item copiedRoot = group[0];

            ItemSize itemSize =
                itemHelper.GetItemSize(group, copiedRoot.Id);

            FindSlotResult slot = containerMapping.FindSlotForItem(
                itemSize.Width,
                itemSize.Height);

            if (!(slot.Success ?? false) ||
                !slot.X.HasValue ||
                !slot.Y.HasValue)
            {
                throw new InvalidOperationException(
                    $"Cargo item {copiedRoot.Template} does not fit in the airdrop crate.");
            }

            bool rotated = slot.Rotation ?? false;

            copiedRoot.ParentId = crate.Id.ToString();
            copiedRoot.SlotId = "main";

            copiedRoot.Location = new Dictionary<string, object>
            {
                ["x"] = slot.X.Value,
                ["y"] = slot.Y.Value,
                ["r"] = rotated ? 1 : 0
            };

            containerMapping.TryFillContainerMapWithItem(
                slot.X.Value,
                slot.Y.Value,
                itemSize.Width,
                itemSize.Height,
                rotated,
                out _);

            cargo.AddRange(group);
        }

        return new List<Item> { crate }
            .Concat(cargo)
            .ToList();
    }

    private static List<Item> GetItemGroup(
        Item root,
        Dictionary<string, List<Item>> childrenByParent)
    {
        List<Item> result = new();

        Queue<Item> pending = new();
        HashSet<string> visited = new();

        pending.Enqueue(root);

        while (pending.Count > 0)
        {
            Item current = pending.Dequeue();
            string currentId = current.Id.ToString();

            if (!visited.Add(currentId))
                continue;

            result.Add(current);

            if (!childrenByParent.TryGetValue(
                currentId,
                out var children))
                continue;

            foreach (Item child in children)
                pending.Enqueue(child);
        }

        return result;
    }
}
