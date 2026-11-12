
namespace TacticalSupplyDrops.Server.Services;

public enum FenceReputationTier
{
    Poor,
    Neutral,
    Good,
    Excellent
}

public class DeliveryPricingService
{
    private const decimal BaseDeliveryFee = 5000m;

    private const decimal PoorReputationMultiplier = 1.50m;
    private const decimal NeutralReputationMultiplier = 1.00m;
    private const decimal GoodReputationMultiplier = 0.90m;
    private const decimal ExcellentReputationMultiplier = 0.75m;

    public int GetDeliveryFee(
        FenceReputationTier reputationTier,
        decimal additionalItemFees = 0m, decimal baseFee = BaseDeliveryFee)
    {
        if (additionalItemFees < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(additionalItemFees));
        }

        decimal multiplier = reputationTier switch
        {
            FenceReputationTier.Poor => PoorReputationMultiplier,
            FenceReputationTier.Neutral => NeutralReputationMultiplier,
            FenceReputationTier.Good => GoodReputationMultiplier,
            FenceReputationTier.Excellent => ExcellentReputationMultiplier,
            _ => NeutralReputationMultiplier
        };

        decimal totalFee = (baseFee + additionalItemFees) * multiplier;

        return checked((int)Math.Ceiling(totalFee));
    }
}
