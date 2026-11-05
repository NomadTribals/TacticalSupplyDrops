
using System.Collections.Generic;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using TacticalSupplyDrops.Server.Services;
using Xunit;

namespace TacticalSupplyDrops.Tests
{
    public class StashPaymentPlanServiceTests
    {
        [Fact]
        public void TryCreatePlan_ShouldDeductAcrossMultipleStacks()
        {
            var stashId = new MongoId("64b000000000000000000001");

            var firstStack = new Item
            {
                Id = new MongoId("64b000000000000000000002"),
                Template = Money.ROUBLES,
                ParentId = (string)stashId,
                Upd = new Upd { StackObjectsCount = 10000 }
            };

            var secondStack = new Item
            {
                Id = new MongoId("64b000000000000000000003"),
                Template = Money.ROUBLES,
                ParentId = (string)stashId,
                Upd = new Upd { StackObjectsCount = 15000 }
            };

            var profile = new PmcData
            {
                Inventory = new BotBaseInventory
                {
                    Stash = stashId,
                    Items = new List<Item> { firstStack, secondStack }
                }
            };

            var service = new StashPaymentPlanService(
                new StashInventoryService());

            var result = service.TryCreatePlan(
                profile, 18000, out var deductions);

            Assert.True(result);
            Assert.Equal(2, deductions.Count);
            Assert.Equal((string)firstStack.Id, deductions[0].ItemId);
            Assert.Equal(10000, deductions[0].QuantityToRemove);
            Assert.Equal((string)secondStack.Id, deductions[1].ItemId);
            Assert.Equal(8000, deductions[1].QuantityToRemove);
        }

        [Fact]
        public void TryCreatePlan_ShouldFailWhenFundsAreInsufficient()
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

            var service = new StashPaymentPlanService(
                new StashInventoryService());

            var result = service.TryCreatePlan(
                profile, 10000, out var deductions);

            Assert.False(result);
            Assert.Empty(deductions);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-100)]
        public void TryCreatePlan_ShouldRejectInvalidAmounts(int amount)
        {
            var service = new StashPaymentPlanService(
                new StashInventoryService());

            var result = service.TryCreatePlan(
                new PmcData(), amount, out var deductions);

            Assert.False(result);
            Assert.Empty(deductions);
        }
    }
}
