using System.Collections.Generic;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using TacticalSupplyDrops.Server.Services;
using Xunit;

namespace TacticalSupplyDrops.Tests;

public class SupplyOrderPaymentServiceTests
{
    [Fact]
    public void CanAffordOrder_ShouldReturnTrueWhenPlayerHasEnoughRoubles()
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

        var currencyService = new StashCurrencyService(
            new StashInventoryService());

        var paymentService = new SupplyOrderPaymentService(
            currencyService);

        bool result = paymentService.CanAffordOrder(profile, 30000);

        Assert.True(result);
    }

    [Fact]
    public void CanAffordOrder_ShouldReturnFalseWhenPlayerHasInsufficientRoubles()
    {
        var stashId = new MongoId("64b000000000000000000001");

        var money = new Item
        {
            Id = new MongoId("64b000000000000000000002"),
            Template = Money.ROUBLES,
            ParentId = (string)stashId,
            Upd = new Upd { StackObjectsCount = 10000 }
        };

        var profile = new PmcData
        {
            Inventory = new BotBaseInventory
            {
                Stash = stashId,
                Items = new List<Item> { money }
            }
        };

        var currencyService = new StashCurrencyService(
            new StashInventoryService());

        var paymentService = new SupplyOrderPaymentService(
            currencyService);

        bool result = paymentService.CanAffordOrder(profile, 30000);

        Assert.False(result);
    }

    [Fact]
    public void CanAffordOrder_ShouldReturnTrueWhenRoublesExactlyMatchPrice()
    {
        var stashId = new MongoId("64b000000000000000000001");

        var money = new Item
        {
            Id = new MongoId("64b000000000000000000002"),
            Template = Money.ROUBLES,
            ParentId = (string)stashId,
            Upd = new Upd { StackObjectsCount = 30000 }
        };

        var profile = new PmcData
        {
            Inventory = new BotBaseInventory
            {
                Stash = stashId,
                Items = new List<Item> { money }
            }
        };

        var currencyService = new StashCurrencyService(
            new StashInventoryService());

        var paymentService = new SupplyOrderPaymentService(
            currencyService);

        bool result = paymentService.CanAffordOrder(profile, 30000);

        Assert.True(result);
    }
}