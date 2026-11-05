
namespace TacticalSupplyDrops.Server.Models;

public class ProcurementPurchaseResult
{
    public bool Success { get; set; }

    public bool IsDuplicate { get; set; }

    public string Message { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public int TotalPrice { get; set; }
}
