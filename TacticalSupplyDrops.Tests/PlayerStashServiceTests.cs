using System.Collections.Generic;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using TacticalSupplyDrops.Server.Services;
using Xunit;

namespace TacticalSupplyDrops.Tests;

public class PlayerStashServiceTests
{
    [Fact]
    public void GetPlayerStashDetails_ShouldReturnStashItemDetails()
    {
        var stashId = new MongoId("64b000000000000000000001");

        var item = new Item
        {
            Id = new MongoId("64b000000000000000000002"),
            Template = new MongoId("64b000000000000000000003"),
            ParentId = (string)stashId
        };

        var profile = new PmcData
        {
            Inventory = new BotBaseInventory
            {
                Stash = stashId,
                Items = new List<Item> { item }
            }
        };

        var inventoryService = new StashInventoryService();
        var detailsService = new StashItemDetailsService(
            new TestItemDetailsProvider());

        var playerStashService = new PlayerStashService(
            inventoryService,
            detailsService);

        var result = playerStashService.GetPlayerStashDetails(profile);

        Assert.Single(result);
        Assert.Equal("Test Item", result[0].Name);
        Assert.Equal(25000, result[0].BaseValue);
    }
    [Fact]
    public void GetPlayerStashDetails_ShouldIncludeNestedContainerItems()
    {
        var stashId = new MongoId("64b000000000000000000001");

        var backpack = new Item
        {
            Id = new MongoId("64b000000000000000000002"),
            Template = new MongoId("64b000000000000000000003"),
            ParentId = (string)stashId
        };

        var storedItem = new Item
        {
            Id = new MongoId("64b000000000000000000004"),
            Template = new MongoId("64b000000000000000000005"),
            ParentId = (string)backpack.Id
        };

        var profile = new PmcData
        {
            Inventory = new BotBaseInventory
            {
                Stash = stashId,
                Items = new List<Item> { backpack, storedItem }
            }
        };

        var inventoryService = new StashInventoryService();
        var detailsService = new StashItemDetailsService(
            new TestItemDetailsProvider());

        var playerStashService = new PlayerStashService(
            inventoryService,
            detailsService);

        var result = playerStashService.GetPlayerStashDetails(profile);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, item => item.ItemId == (string)backpack.Id);
        Assert.Contains(result, item => item.ItemId == (string)storedItem.Id);
    }

    private class TestItemDetailsProvider : IItemDetailsProvider
    {
        public (string Name, int BaseValue, int Width, int Height, string IconUrl, string Category)
        GetDetails(Item item)
        {
            return ("Test Item", 25000, 1, 1, string.Empty, "OTHER");
        }
        public bool IsInstalledInEquipmentSlot(Item item, Item? parent)
        {
            return false;
        }
    }

}