using System.Collections.Generic;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using TacticalSupplyDrops.Server.Services;
using Xunit;

namespace TacticalSupplyDrops.Tests;

public class StashItemDetailsServiceTests
{
    [Fact]
    public void GetItemDetails_ShouldReturnItemInformation()
    {
        var item = new Item
        {
            Id = new MongoId("64b000000000000000000001"),
            Template = new MongoId("64b000000000000000000002")
        };

        var provider = new TestItemDetailsProvider();
        var service = new StashItemDetailsService(provider);

        var result = service.GetItemDetails(new List<Item> { item });

        Assert.Single(result);
        Assert.Equal("Test Item One", result[0].Name);
        Assert.Equal(25000, result[0].BaseValue);
        Assert.Equal(1, result[0].Quantity);
    }
    [Fact]
    public void GetItemDetails_ShouldReturnMultipleItems()
    {
        var item1 = new Item
        {
            Id = new MongoId("64b000000000000000000001"),
            Template = new MongoId("64b000000000000000000002")
        };

        var item2 = new Item
        {
            Id = new MongoId("64b000000000000000000003"),
            Template = new MongoId("64b000000000000000000004")
        };

        var provider = new TestItemDetailsProvider();
        var service = new StashItemDetailsService(provider);

        var result = service.GetItemDetails(new List<Item> { item1, item2 });

        Assert.Equal(2, result.Count);

        Assert.Equal("Test Item One", result[0].Name);
        Assert.Equal(25000, result[0].BaseValue);

        Assert.Equal("Test Item Two", result[1].Name);
        Assert.Equal(50000, result[1].BaseValue);
    }
    [Fact]
    public void GetItemDetails_ShouldReturnCorrectStackQuantity()
    {
        var item = new Item
        {
            Id = new MongoId("64b000000000000000000001"),
            Template = new MongoId("64b000000000000000000002"),
            Upd = new Upd
            {
                StackObjectsCount = 120
            }
        };

        var provider = new TestItemDetailsProvider();
        var service = new StashItemDetailsService(provider);

        var result = service.GetItemDetails(new List<Item> { item });

        Assert.Single(result);
        Assert.Equal(120, result[0].Quantity);
    }
    [Fact]
    public void GetItemDetails_WithEmptyList_ShouldReturnEmptyList()
    {
        var provider = new TestItemDetailsProvider();
        var service = new StashItemDetailsService(provider);

        var result = service.GetItemDetails(new List<Item>());

        Assert.Empty(result);
    }
    [Fact]
    public void GetItemDetails_WithNoAvailablePrice_ShouldReturnZero()
    {
        var item = new Item
        {
            Id = new MongoId("64b000000000000000000005"),
            Template = new MongoId("64b000000000000000000006")
        };

        var provider = new TestItemDetailsProvider();
        var service = new StashItemDetailsService(provider);

        var result = service.GetItemDetails(new List<Item> { item });

        Assert.Single(result);
        Assert.Equal("Unknown Price Item", result[0].Name);
        Assert.Equal(0, result[0].BaseValue);
    }

    private class TestItemDetailsProvider : IItemDetailsProvider
    {
        public (string Name, int BaseValue, int Width, int Height, string IconUrl, string Category)
            GetDetails(Item item)
        {
            string itemId = (string)item.Id;

            if (itemId == "64b000000000000000000001")
            {
                return ("Test Item One", 25000, 1, 1, string.Empty, "OTHER");
            }

            if (itemId == "64b000000000000000000005")
            {
                return ("Unknown Price Item", 0, 1, 1, string.Empty, "OTHER");
            }

            return ("Test Item Two", 50000, 1, 1, string.Empty, "OTHER");
        }
        public bool IsInstalledInEquipmentSlot(Item item, Item? parent)
        {
            return false;
        }
    }

}