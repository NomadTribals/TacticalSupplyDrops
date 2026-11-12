using SPTarkov.DI.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;
using TacticalSupplyDrops.Server.Models;

namespace TacticalSupplyDrops.Server.Services;

[Injectable]
public class SupplyOrderService
{
    public SupplyOrder CreateOrder(
        IEnumerable<SupplyOrderItem> items,
        int totalPrice)
    {
        ArgumentNullException.ThrowIfNull(items);

        List<SupplyOrderItem> itemList = items.ToList();

        if (itemList.Count == 0)
        {
            throw new ArgumentException(
                "A supply order must contain at least one item.",
                nameof(items));
        }

        if (itemList.Any(item =>
            item is null ||
            string.IsNullOrWhiteSpace(item.ItemId) ||
            item.Quantity < 1))
        {
            throw new ArgumentException(
                "The supply order contains an invalid item.",
                nameof(items));
        }

        if (totalPrice < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalPrice));
        }

        return new SupplyOrder
        {
            Items = itemList,
            TotalPrice = totalPrice,
            Status = SupplyOrderStatus.Pending
        };
    }
}