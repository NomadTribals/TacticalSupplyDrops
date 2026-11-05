
using System.Collections.Generic;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;

namespace TacticalSupplyDrops.Server.Models;

public class SupplyOrderItem
{
    public string ItemId { get; set; }

    public int Quantity { get; set; }

    public List<Item> Items { get; set; } = new();

    public SupplyOrderItem(string itemId, int quantity)
    {
        ItemId = itemId;
        Quantity = quantity;
    }
}
