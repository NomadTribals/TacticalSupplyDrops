using System.Collections.Generic;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using TacticalSupplyDrops.Server.Services;
using Xunit;

namespace TacticalSupplyDrops.Tests;

public class StashCurrencyServiceTests
{
    [Fact]
    public void GetRoublesInStash_ShouldCountRoublesInStash()
    {
        var stashId = new MongoId("64b000000000000000000001");

        var money = new Item
        {
            Id = new MongoId("64b000000000000000000002"),
            Template = Money.ROUBLES,
            ParentId = (string)stashId,
            Upd = new Upd { StackObjectsCount = 50000 }
        };

        var profile = new PmcData
        {
            Inventory = new BotBaseInventory
            {
                Stash = stashId,
                Items = new List<Item> { money }
            }
        };

        var service = new StashCurrencyService(
            new StashInventoryService());

        var result = service.GetRoublesInStash(profile);

        Assert.Equal(50000, result);
    }

    [Fact]
    public void GetRoublesInStash_ShouldIncludeNestedRoubles()
    {
        var stashId = new MongoId("64b000000000000000000001");

        var backpack = new Item
        {
            Id = new MongoId("64b000000000000000000002"),
            Template = new MongoId("64b000000000000000000003"),
            ParentId = (string)stashId
        };

        var money = new Item
        {
            Id = new MongoId("64b000000000000000000004"),
            Template = Money.ROUBLES,
            ParentId = (string)backpack.Id,
            Upd = new Upd { StackObjectsCount = 25000 }
        };

        var profile = new PmcData
        {
            Inventory = new BotBaseInventory
            {
                Stash = stashId,
                Items = new List<Item> { backpack, money }
            }
        };

        var service = new StashCurrencyService(
            new StashInventoryService());

        var result = service.GetRoublesInStash(profile);

        Assert.Equal(25000, result);
    }

    [Fact]
    public void GetRoublesInStash_ShouldIgnoreRoublesOutsideStash()
    {
        var stashId = new MongoId("64b000000000000000000001");

        var stashMoney = new Item
        {
            Id = new MongoId("64b000000000000000000002"),
            Template = Money.ROUBLES,
            ParentId = (string)stashId,
            Upd = new Upd { StackObjectsCount = 30000 }
        };

        var pocketMoney = new Item
        {
            Id = new MongoId("64b000000000000000000003"),
            Template = Money.ROUBLES,
            ParentId = "equipment",
            Upd = new Upd { StackObjectsCount = 10000 }
        };

        var profile = new PmcData
        {
            Inventory = new BotBaseInventory
            {
                Stash = stashId,
                Items = new List<Item> { stashMoney, pocketMoney }
            }
        };

        var service = new StashCurrencyService(
            new StashInventoryService());

        var result = service.GetRoublesInStash(profile);

        Assert.Equal(30000, result);
    }
}