namespace TacticalSupplyDrops.Server.Services;

public class ItemDeliveryPricingService
{
    public int CalculateItemDeliveryFee(decimal unitWeight, int quantity, decimal ratePerKg = 1000m)
    {
        if (unitWeight < 0) throw new ArgumentOutOfRangeException(nameof(unitWeight));
        if (quantity < 1) throw new ArgumentOutOfRangeException(nameof(quantity));
        if (ratePerKg < 0) throw new ArgumentOutOfRangeException(nameof(ratePerKg));
        return checked((int)Math.Ceiling(unitWeight * quantity * ratePerKg));
    }
}
