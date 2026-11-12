namespace TacticalSupplyDrops.Server.Models;

public enum SupplyOrderStatus
{
    Pending,
    Approved,
    InTransit,
    Delivered,
    Failed,
    Cancelled
}