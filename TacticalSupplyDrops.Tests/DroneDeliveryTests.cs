using TacticalSupplyDrops.Server.Services;

namespace TacticalSupplyDrops.Tests;

public class DroneDeliveryTests
{
    [Theory]
    [InlineData("AMMO")]
    [InlineData("MEDICAL")]
    [InlineData("FOOD")]
    public void SmallSuppliesAreSupported(string category)
    {
        Assert.Null(DroneDeliveryPolicy.Validate(new[] { new StashItemDetails { Name = "Supplies", Category = category, Width = 1, Height = 1 } }, 1));
    }

    [Theory]
    [InlineData("WEAPONS")]
    [InlineData("ARMOR")]
    [InlineData("CONTAINERS")]
    [InlineData("OTHER")]
    public void UnsupportedCargoHasClearReason(string category)
    {
        Assert.Contains("Standard Cargo", DroneDeliveryPolicy.Validate(new[] { new StashItemDetails { Name = "Camera", Category = category, Width = 1, Height = 1 } }, 1));
    }

    [Fact]
    public void OversizedAndOverCapacityCargoAreRejected()
    {
        Assert.Contains("too large", DroneDeliveryPolicy.Validate(new[] { new StashItemDetails { Name = "Supply", Category = "MEDICAL", Width = 5, Height = 1 } }, 5));
        Assert.Contains("12 cargo cells", DroneDeliveryPolicy.Validate(Array.Empty<StashItemDetails>(), 13));
    }

    [Fact]
    public void PackingUsesRotationAndRejectsImpossibleLayouts()
    {
        Assert.NotNull(DroneDeliveryPolicy.Pack(new[] { (3, 2), (3, 2) }));
        Assert.Null(DroneDeliveryPolicy.Pack(new[] { (2, 2), (2, 2), (2, 2) }));
        Assert.NotNull(DroneDeliveryPolicy.Pack(Enumerable.Repeat((1, 1), 12).ToArray()));
        Assert.Null(DroneDeliveryPolicy.Pack(Enumerable.Repeat((1, 1), 13).ToArray()));
    }

    [Fact]
    public void PayloadWeightIncludesEveryRoundAndEnforcesFiveKgLimit()
    {
        var ammo = new StashItemDetails { Name = "Ammo", Category = "AMMO", Width = 1, Height = 1, UnitWeight = .05, Quantity = 100 };
        Assert.Null(DroneDeliveryPolicy.Validate(new[] { ammo }, 1));
        ammo.Quantity = 101;
        Assert.Contains("5 kg", DroneDeliveryPolicy.Validate(new[] { ammo }, 1));
    }

    [Fact]
    public void SameWeightCostsTheSameRegardlessOfItemValue()
    {
        var pricing = new SupplyOrderPricingService();
        var common = new StashItemDetails { UnitWeight = .01, Quantity = 100, BaseValue = 100 };
        var rare = new StashItemDetails { UnitWeight = .01, Quantity = 100, BaseValue = 100000 };
        Assert.Equal(2500, pricing.CalculateSupplyOrderPrice(new[] { common }, FenceReputationTier.Neutral, "drone"));
        Assert.Equal(2500, pricing.CalculateSupplyOrderPrice(new[] { rare }, FenceReputationTier.Neutral, "drone"));
    }

    [Fact]
    public void DroneRateIsLowerThanStandardAndKeepsReputationDiscount()
    {
        var pricing = new SupplyOrderPricingService();
        var ammo = new[] { (.01m, 100) };
        Assert.Equal(2500, pricing.CalculateSupplyOrderPrice(ammo, FenceReputationTier.Neutral, "drone"));
        Assert.Equal(1875, pricing.CalculateSupplyOrderPrice(ammo, FenceReputationTier.Excellent, "drone"));
        Assert.Equal(6000, pricing.CalculateSupplyOrderPrice(ammo, FenceReputationTier.Neutral, "standard"));
    }
}
