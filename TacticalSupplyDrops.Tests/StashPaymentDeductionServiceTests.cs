
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

public class StashPaymentDeductionServiceTests
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

            int currentCount = item.GetItemStackSize();
            int remaining = currentCount - count;

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
    public void TryApplyPlan_ShouldRemoveRequestedRoubles()
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

        var service = new StashPaymentDeductionService(
            new StashInventoryService(),
            new FakeItemRemover());

        var plan = new List<StashPaymentDeduction>
        {
            new((string)money.Id, 4000)
        };

        var result = service.TryApplyPlan(
            profile,
            new MongoId("64b000000000000000000010"),
            new ItemEventRouterResponse(),
            plan);

        Assert.True(result);
        Assert.Equal(6000, money.Upd.StackObjectsCount);
    }

    [Fact]
    public void TryApplyPlan_ShouldRejectInsufficientStackWithoutRemovingMoney()
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

        var service = new StashPaymentDeductionService(
            new StashInventoryService(),
            new FakeItemRemover());

        var plan = new List<StashPaymentDeduction>
        {
            new((string)money.Id, 6000)
        };

        var result = service.TryApplyPlan(
            profile,
            new MongoId("64b000000000000000000010"),
            new ItemEventRouterResponse(),
            plan);

        Assert.False(result);
        Assert.Equal(5000, money.Upd.StackObjectsCount);
    }
}
