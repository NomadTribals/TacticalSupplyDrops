using System.Collections.Generic;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using TacticalSupplyDrops.Server.Services;
using Xunit;

namespace TacticalSupplyDrops.Tests;

public class DeliverySelectionServiceTests
{
    private static DeliverySelectionService CreateService()
    {
        var stashService = new StashInventoryService();

        var dimensionsProvider = new TestDimensionsProvider();

        var footprintService = new ItemFootprintService(
            dimensionsProvider);

        var limitService = new DeliveryFootprintLimitService(
            footprintService);

        return new DeliverySelectionService(
            stashService,
            limitService);
    }

    [Fact]
    public void TryValidateSelection_ShouldAcceptItemsWithinLimit()
    {
        var service = CreateService();
        var stashId = new MongoId("64b000000000000000000001");

        var item1 = CreateItem(
            "64b000000000000000000002", (string)stashId);

        var item2 = CreateItem(
            "64b000000000000000000003", (string)stashId);

        var profile = CreateProfile(stashId, item1, item2);

        bool result = service.TryValidateSelection(
            profile,
            new List<string>
            {
                (string)item1.Id,
                (string)item2.Id
            },
            out var selectedItems,
            out int totalFootprint);

        Assert.True(result);
        Assert.Equal(2, selectedItems.Count);
        Assert.Equal(12, totalFootprint);
    }

    [Fact]
    public void TryValidateSelection_ShouldRejectItemsOverLimit()
    {
        var service = CreateService();
        var stashId = new MongoId("64b000000000000000000001");

        var item1 = CreateItem(
            "64b000000000000000000004", (string)stashId);

        var item2 = CreateItem(
            "64b000000000000000000005", (string)stashId);

        var profile = CreateProfile(stashId, item1, item2);

        bool result = service.TryValidateSelection(
            profile,
            new List<string>
            {
                (string)item1.Id,
                (string)item2.Id
            },
            out var selectedItems,
            out int totalFootprint);

        Assert.False(result);
        Assert.Empty(selectedItems);
        Assert.Equal(0, totalFootprint);
    }

    [Fact]
    public void TryValidateSelection_ShouldRejectItemsOutsideStash()
    {
        var service = CreateService();
        var stashId = new MongoId("64b000000000000000000001");

        var outsideItem = CreateItem(
            "64b000000000000000000006", "equipment");

        var profile = CreateProfile(stashId, outsideItem);

        bool result = service.TryValidateSelection(
            profile,
            new List<string> { (string)outsideItem.Id },
            out var selectedItems,
            out int totalFootprint);

        Assert.False(result);
        Assert.Empty(selectedItems);
        Assert.Equal(0, totalFootprint);
    }

    [Fact]
    public void TryValidateSelection_ShouldRejectDuplicateItemIds()
    {
        var service = CreateService();
        var stashId = new MongoId("64b000000000000000000001");

        var item = CreateItem(
            "64b000000000000000000007", (string)stashId);

        var profile = CreateProfile(stashId, item);

        bool result = service.TryValidateSelection(
            profile,
            new List<string>
            {
                (string)item.Id,
                (string)item.Id
            },
            out var selectedItems,
            out int totalFootprint);

        Assert.False(result);
        Assert.Empty(selectedItems);
        Assert.Equal(0, totalFootprint);
    }

    [Fact]
    public void TryValidateSelection_ShouldRejectEmptySelection()
    {
        var service = CreateService();
        var stashId = new MongoId("64b000000000000000000001");
        var profile = CreateProfile(stashId);

        bool result = service.TryValidateSelection(
            profile,
            new List<string>(),
            out var selectedItems,
            out int totalFootprint);

        Assert.False(result);
        Assert.Empty(selectedItems);
        Assert.Equal(0, totalFootprint);
    }

    [Fact]
    public void TryValidateSelection_ShouldIncludeContainerContents()
    {
        var service = CreateService();
        var stashId = new MongoId("64b000000000000000000001");

        var backpack = CreateItem(
            "64b000000000000000000020", (string)stashId);

        var contents = CreateItem(
            "64b000000000000000000021", (string)backpack.Id);

        var profile = CreateProfile(stashId, backpack, contents);

        bool result = service.TryValidateSelection(
            profile,
            new List<string> { (string)backpack.Id },
            out var selectedItems,
            out int totalFootprint);

        Assert.True(result);
        Assert.Equal(2, selectedItems.Count);
        Assert.Contains(selectedItems, item => item.Id == backpack.Id);
        Assert.Contains(selectedItems, item => item.Id == contents.Id);
        Assert.Equal(7, totalFootprint);
    }

    [Fact]
    public void TryValidateSelection_ShouldNotCountContainerContentsTwice()
    {
        var service = CreateService();
        var stashId = new MongoId("64b000000000000000000001");

        var backpack = CreateItem(
            "64b000000000000000000022", (string)stashId);

        var contents = CreateItem(
            "64b000000000000000000023", (string)backpack.Id);

        var profile = CreateProfile(stashId, backpack, contents);

        bool result = service.TryValidateSelection(
            profile,
            new List<string>
            {
                (string)backpack.Id,
                (string)contents.Id
            },
            out var selectedItems,
            out int totalFootprint);

        Assert.True(result);
        Assert.Equal(2, selectedItems.Count);
        Assert.Equal(7, totalFootprint);
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

    private class TestDimensionsProvider : IItemDimensionsProvider
    {
        public (int? Width, int? Height) GetDimensions(Item item)
        {
            string id = (string)item.Id;

            return id switch
            {
                "64b000000000000000000002" => (2, 3),
                "64b000000000000000000003" => (2, 3),
                "64b000000000000000000004" => (6, 10),
                "64b000000000000000000005" => (2, 3),
                "64b000000000000000000020" => (2, 3),
                "64b000000000000000000021" => (1, 1),
                "64b000000000000000000022" => (2, 3),
                "64b000000000000000000023" => (1, 1),
                _ => (1, 1)
            };
        }
    }
}