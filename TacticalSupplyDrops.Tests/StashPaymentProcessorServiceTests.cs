
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.ItemEvent;
using SPTarkov.Server.Core.Models.Enums;
using System.Collections.Generic;
using System.Linq;
using TacticalSupplyDrops.Server.Services;
using Xunit;

namespace TacticalSupplyDrops.Tests;

public class StashPaymentProcessorServiceTests
{
    private class FakeItemRemover : IInventoryItemRemover
    {
        public void RemoveItemByCount(
            PmcData profile,
            MongoId itemId,
            int count,
            MongoId sessionId,
            ItemEventRouterResponse output)
        {
            var item = profile.Inventory.Items.First(
                item => item.Id == itemId);

            int remaining = item.GetItemStackSize() - count;

            if (remaining <= 0)
            {
                profile.Inventory.Items.Remove(item);
            }
            else
            {
                item.Upd.StackObjectsCount = remaining;
            }
        }
        public void RemoveItem(
    PmcData profile,
    MongoId itemId,
    MongoId sessionId,
    ItemEventRouterResponse output)
        {
            // No action needed in these tests yet.
        }
    }

    [Fact]
    public void TryProcessPayment_ShouldDeductPaymentWhenFundsAreAvailable()
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

        var stashService = new StashInventoryService();

        var processor = new StashPaymentProcessorService(
            new StashPaymentPlanService(stashService),
            new StashPaymentDeductionService(
                stashService,
                new FakeItemRemover()));

        bool result = processor.TryProcessPayment(
            profile,
            new MongoId("64b000000000000000000010"),
            new ItemEventRouterResponse(),
            4000);

        Assert.True(result);
        Assert.Equal(6000, money.Upd.StackObjectsCount);
    }

    [Fact]
    public void TryProcessPayment_ShouldNotDeductWhenFundsAreInsufficient()
    {
        var stashId = new MongoId("64b000000000000000000001");

        var money = new Item
        {
            Id = new MongoId("64b000000000000000000002"),
            Template = Money.ROUBLES,
            ParentId = (string)stashId,
            Upd = new Upd { StackObjectsCount = 5000 }
        };

        var profile = new PmcData
        {
            Inventory = new BotBaseInventory
            {
                Stash = stashId,
                Items = new List<Item> { money }
            }
        };

        var stashService = new StashInventoryService();

        var processor = new StashPaymentProcessorService(
            new StashPaymentPlanService(stashService),
            new StashPaymentDeductionService(
                stashService,
                new FakeItemRemover()));

        bool result = processor.TryProcessPayment(
            profile,
            new MongoId("64b000000000000000000010"),
            new ItemEventRouterResponse(),
            10000);

        Assert.False(result);
        Assert.Equal(5000, money.Upd.StackObjectsCount);
    }
}
