using SPTarkov.DI.Annotations;
namespace TacticalSupplyDrops.Server.Services;

[Injectable]
public class SupplyOrderPricingService
{
    public const decimal StandardBaseFee = 5000m;
    public const decimal StandardRatePerKg = 1000m;
    public const decimal DroneBaseFee = 2000m;
    public const decimal DroneRatePerKg = 500m;

    public int CalculateSupplyOrderPrice(IEnumerable<(decimal UnitWeight, int Quantity)> items,
        FenceReputationTier reputationTier, string deliveryMethod = "standard")
    {
        ArgumentNullException.ThrowIfNull(items);
        if (deliveryMethod is not ("standard" or "drone")) throw new ArgumentException("Unknown delivery service.");
        decimal totalWeight = 0;
        foreach (var item in items)
        {
            if (item.UnitWeight < 0 || item.Quantity < 1) throw new ArgumentOutOfRangeException(nameof(items));
            totalWeight += item.UnitWeight * item.Quantity;
        }
        bool drone = deliveryMethod == "drone";
        return new DeliveryPricingService().GetDeliveryFee(reputationTier,
            totalWeight * (drone ? DroneRatePerKg : StandardRatePerKg), drone ? DroneBaseFee : StandardBaseFee);
    }

    public int CalculateSupplyOrderPrice(IEnumerable<StashItemDetails> items,
        FenceReputationTier reputationTier, string deliveryMethod = "standard")
    {
        ArgumentNullException.ThrowIfNull(items);
        return CalculateSupplyOrderPrice(items.Select(item => ((decimal)item.UnitWeight, item.Quantity)), reputationTier, deliveryMethod);
    }
}
