using SPTarkov.DI.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TacticalSupplyDrops.Server.Services;

[Injectable]
public class SupplyOrderPricingService
{
    private readonly ItemDeliveryPricingService _itemPricingService = new();

    public int CalculateSupplyOrderPrice(
        IEnumerable<(int ItemBaseValue, int Quantity)> items,
        FenceReputationTier reputationTier)
    {
        ArgumentNullException.ThrowIfNull(items);

        decimal totalItemDeliveryFees = 0;

        foreach (var item in items)
        {
            totalItemDeliveryFees += _itemPricingService.CalculateItemDeliveryFee(
                item.ItemBaseValue,
                item.Quantity);
        }

        var deliveryPricingService = new DeliveryPricingService();

        return deliveryPricingService.GetDeliveryFee(
            reputationTier,
            totalItemDeliveryFees);
    }

    public int CalculateSupplyOrderPrice(
        IEnumerable<StashItemDetails> items,
        FenceReputationTier reputationTier)
    {
        ArgumentNullException.ThrowIfNull(items);

        var pricingItems = items.Select(item =>
            (ItemBaseValue: item.BaseValue, Quantity: item.Quantity));

        return CalculateSupplyOrderPrice(pricingItems, reputationTier);
    }
}