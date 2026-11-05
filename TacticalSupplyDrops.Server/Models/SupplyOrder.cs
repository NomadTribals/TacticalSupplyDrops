
using System;
using System.Collections.Generic;

namespace TacticalSupplyDrops.Server.Models;

public class SupplyOrder
{
    public string OrderId { get; set; } = Guid.NewGuid().ToString();

    public string RequestId { get; set; } = string.Empty;

    public List<SupplyOrderItem> Items { get; set; } = new();

    public int TotalPrice { get; set; }
    public int TotalFootprint { get; set; }
    public SupplyOrderStatus Status { get; set; } = SupplyOrderStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
