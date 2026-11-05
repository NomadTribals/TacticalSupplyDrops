
using System.Collections.Generic;
using System.Linq;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.ItemEvent;
using TacticalSupplyDrops.Server.Services;
using Xunit;

namespace TacticalSupplyDrops.Tests;

public class SupplyOrderItemReservationServiceTests
{
    private class FakeItemRemover : IInventoryItemRemover
    {
        public List<string> RemovedItemIds { get; } = new();

        public void RemoveItemByCount(
            PmcData profile,
            MongoId itemId,
            int count,
            MongoId sessionId,
            ItemEventRouterResponse output)
        {
        }

        public void RemoveItem(
            PmcData profile,
            MongoId itemId,
            MongoId sessionId,
            ItemEventRouterResponse output)
        {
            RemovedItemIds.Add((string)itemId);

            var item = profile.Inventory.Items.FirstOrDefault(
                item => item.Id == itemId);

            if (item == null)
            {
                return;
            }

            var children = profile.Inventory.Items
                .Where(child => child.ParentId == (string)itemId)
                .ToList();

            foreach (var child in children)
            {
                RemoveItem(profile, child.Id, sessionId, output);
            }

            profile.Inventory.Items.Remove(item);
        }
    }

    private static Item CreateItem(string id, string parentId)
    {
        return new Item
        {
            Id = new MongoId(id),
            Template = new MongoId("64b000000000000000000099"),
            ParentId = parentId
        };
    }

    private static PmcData CreateProfile(
        MongoId stashId,
        params Item[] items)
    {
        return new PmcData
        {
            Inventory = new BotBaseInventory
            {
                Stash = stashId,
                Items = new List<Item>(items)
            }
        };
    }

    [Fact]
    public void TryReserveItems_WithContainerAndChildSelected_ShouldRemoveBothOnlyOnce()
    {
        var stashId = new MongoId("64b000000000000000000001");

        var container = CreateItem(
            "64b000000000000000000002",
            (string)stashId);

        var child = CreateItem(
            "64b000000000000000000003",
            (string)container.Id);

        var profile = CreateProfile(stashId, container, child);
        var remover = new FakeItemRemover();

        var service = new SupplyOrderItemReservationService(
            new StashInventoryService(),
            remover);

        bool result = service.TryReserveItems(
            profile,
            new MongoId("64b000000000000000000020"),
            new ItemEventRouterResponse(),
            new List<Item> { container, child });

        Assert.True(result);
        Assert.Empty(profile.Inventory.Items);
        Assert.Equal(2, remover.RemovedItemIds.Count);
        Assert.Contains((string)container.Id, remover.RemovedItemIds);
        Assert.Contains((string)child.Id, remover.RemovedItemIds);
    }

    [Fact]
    public void TryReserveItems_WithItemOutsideStash_ShouldRejectWithoutRemovingAnything()
    {
        var stashId = new MongoId("64b000000000000000000001");

        var item = CreateItem(
            "64b000000000000000000004",
            "equipment");

        var profile = CreateProfile(stashId, item);
        var remover = new FakeItemRemover();

        var service = new SupplyOrderItemReservationService(
            new StashInventoryService(),
            remover);

        bool result = service.TryReserveItems(
            profile,
            new MongoId("64b000000000000000000020"),
            new ItemEventRouterResponse(),
            new List<Item> { item });

        Assert.False(result);
        Assert.Empty(remover.RemovedItemIds);
        Assert.Single(profile.Inventory.Items);
    }

    [Fact]
    public void TryReserveItems_WithSingleItemSelected_ShouldRemoveItem()
    {
        var stashId = new MongoId("64b000000000000000000001");

        var item = CreateItem(
            "64b000000000000000000005",
            (string)stashId);

        var profile = CreateProfile(stashId, item);
        var remover = new FakeItemRemover();

        var service = new SupplyOrderItemReservationService(
            new StashInventoryService(),
            remover);

        bool result = service.TryReserveItems(
            profile,
            new MongoId("64b000000000000000000020"),
            new ItemEventRouterResponse(),
            new List<Item> { item });

        Assert.True(result);
        Assert.Empty(profile.Inventory.Items);
        Assert.Single(remover.RemovedItemIds);
        Assert.Equal((string)item.Id, remover.RemovedItemIds[0]);
    }
}
