using TacticalSupplyDrops.Server.Services;

namespace TacticalSupplyDrops.Tests;

public class AffordableDeliveryTests
{
    [Theory]
    [InlineData(FenceReputationTier.Neutral, 6000)]
    [InlineData(FenceReputationTier.Good, 5400)]
    [InlineData(FenceReputationTier.Excellent, 4500)]
    [InlineData(FenceReputationTier.Poor, 9000)]
    public void OneKilogramOfAmmoUsesWeightBasedDeliveryFee(FenceReputationTier reputation, int expected)
    {
        var price = new SupplyOrderPricingService().CalculateSupplyOrderPrice(new[] { (.01m, 100) }, reputation);
        Assert.Equal(expected, price);
    }
}
