
using System;

namespace TacticalSupplyDrops.Server.Services;

public class ItemDeliveryPricingService
{
    private const decimal ItemValuePercentage = 0.05m;

    public int CalculateItemDeliveryFee(int itemBaseValue, int quantity)
    {
        if (itemBaseValue < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(itemBaseValue));
        }

        if (quantity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity));
        }

        decimal totalItemValue = (decimal)itemBaseValue * quantity;
        decimal deliveryFee = totalItemValue * ItemValuePercentage;

        return (int)Math.Ceiling(deliveryFee);
    }
}
