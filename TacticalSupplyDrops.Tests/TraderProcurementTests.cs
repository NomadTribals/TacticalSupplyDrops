using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using TacticalSupplyDrops.Server.Services;
using TacticalSupplyDrops.Server.Models;

namespace TacticalSupplyDrops.Tests;

public class TraderProcurementTests
{
    [Fact]
    public void WalletKeepsCurrencyBalancesSeparateAndIncludesNestedStacks()
    {
        var wallet = TraderProcurementService.WalletFromItems([
            Stack(Money.ROUBLES, 15000), Stack(Money.ROUBLES, 20000, "wallet"),
            Stack(Money.DOLLARS, 700), Stack(Money.DOLLARS, 300, "wallet"), Stack(Money.EUROS, 250)
        ]);
        Assert.Equal(35000, wallet.roubles);
        Assert.Equal(1000, wallet.dollars);
        Assert.Equal(250, wallet.euros);
        Assert.Equal("USD", TraderProcurementService.CurrencyCode(Money.DOLLARS));
        Assert.Equal("EUR", TraderProcurementService.CurrencyCode(Money.EUROS));
        Assert.Equal("RUB", TraderProcurementService.CurrencyCode(Money.ROUBLES));
    }

    [Fact]
    public void DisplayNameKeepsCustomAmmoStatsWithoutUnityMarkup()
    {
        Assert.Equal("5.8x42mm DVX12 [50/46]", TraderProcurementService.DisplayName("5.8x42mm DVX12 <color=#808080>[50/46]</color>"));
        Assert.Equal("Aishi", TraderProcurementService.DisplayName("<b>Aishi</b>"));
    }

    [Fact]
    public void CargoCheckoutConsumesOnlyPurchasedQuantityAndIsIdempotent()
    {
        var first = new ProcurementPurchase { Status = "Staged", CargoSelections = [new() { itemId = "ammo", quantity = 100 }] };
        var second = new ProcurementPurchase { Status = "Staged", CargoSelections = [new() { itemId = "ammo", quantity = 50 }] };
        List<ProcurementPurchase> purchases = [first, second];
        Dictionary<string, int> selected = new() { ["ammo"] = 130 };
        TraderProcurementService.AllocateCargo(purchases, "cargo1", selected);
        Assert.Equal("CargoCommitted", first.Status);
        Assert.Equal(20, Assert.Single(second.CargoSelections).quantity);
        TraderProcurementService.AllocateCargo(purchases, "cargo1", selected);
        Assert.Equal(20, Assert.Single(second.CargoSelections).quantity);
        TraderProcurementService.AllocateCargo(purchases, "cargo2", new() { ["ammo"] = 20 });
        Assert.Equal("CargoCommitted", second.Status);
    }

    private static Item Stack(MongoId template, int count, string? parent = null) => new()
    {
        Id = new MongoId(), Template = template, ParentId = parent,
        Upd = new Upd { StackObjectsCount = count }
    };

    [Fact]
    public void PaymentCombinesStacksAndGroupsRepeatedCosts()
    {
        var first = Stack(Money.ROUBLES, 100);
        var second = Stack(Money.ROUBLES, 300);
        var payment = TraderProcurementService.BuildPayment([first, second],
            [new() { Template = Money.ROUBLES, Count = 50 }, new() { Template = Money.ROUBLES, Count = 25 }], 4);
        Assert.NotNull(payment);
        Assert.Equal(300, payment.Sum(x => x.Count));
        Assert.Equal(2, payment.Count);
        Assert.Equal(100, first.Upd!.StackObjectsCount); // quote construction cannot spend anything
    }

    [Fact]
    public void PaymentRejectsInsufficientCurrencyAndOverflow()
    {
        var stash = new List<Item> { Stack(Money.ROUBLES, 100) };
        Assert.Null(TraderProcurementService.BuildPayment(stash, [new() { Template = Money.ROUBLES, Count = 101 }], 1));
        Assert.Null(TraderProcurementService.BuildPayment(stash, [new() { Template = Money.ROUBLES, Count = int.MaxValue }], 2));
    }

    [Fact]
    public void PaymentDoesNotConsumeAnItemWithContents()
    {
        var template = new MongoId();
        var container = Stack(template, 1);
        var child = Stack(Money.ROUBLES, 100, container.Id.ToString());
        Assert.Null(TraderProcurementService.BuildPayment([container, child], [new() { Template = template, Count = 1 }], 1));
    }

    [Fact]
    public void PriceTokenChangesWhenPriceChanges()
    {
        var cost = new BarterScheme { Template = Money.ROUBLES, Count = 1200 };
        string previous = TraderProcurementService.PriceToken([cost]);
        cost.Count = 1201;
        Assert.NotEqual(previous, TraderProcurementService.PriceToken([cost]));
    }

    [Fact]
    public void RestrictedBartersNeedExplicitSelection()
    {
        Assert.True(TraderProcurementService.IsAutomaticPayment(new() { Template = Money.ROUBLES, Count = 1200 }));
        Assert.False(TraderProcurementService.IsAutomaticPayment(new() { Template = new MongoId(), Count = 1, Level = 30 }));
        Assert.False(TraderProcurementService.IsAutomaticPayment(new() { Template = new MongoId(), Count = 1, OnlyFunctional = true }));
        Assert.False(TraderProcurementService.IsAutomaticPayment(new() { Template = Money.ROUBLES, Count = double.NaN }));
    }

    [Fact]
    public void PersonalLimitCannotBecomeNegative()
    {
        Assert.Equal(-1, TraderProcurementService.Remaining(Stack(Money.ROUBLES, 100)));
        var item = Stack(Money.ROUBLES, 100);
        item.Upd!.BuyRestrictionMax = 50;
        item.Upd.BuyRestrictionCurrent = 45;
        Assert.Equal(5, TraderProcurementService.Remaining(item));
        item.Upd.BuyRestrictionCurrent = 51;
        Assert.Equal(0, TraderProcurementService.Remaining(item));
    }
}
