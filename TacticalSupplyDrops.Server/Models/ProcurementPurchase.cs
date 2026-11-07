
using System;

namespace TacticalSupplyDrops.Server.Models;

public class ProcurementPurchase
{
    public string TraderId { get; set; } = string.Empty;
    public string OfferId { get; set; } = string.Empty;
    public System.Collections.Generic.List<ProcurementCargoSelection> CargoSelections { get; set; } = new();
    public System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, int>> CargoAllocations { get; set; } = new();
    public string PurchaseId { get; set; } =
        Guid.NewGuid().ToString("N");

    public string RequestId { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public int TotalPrice { get; set; }

    public string Status { get; set; } = "Pending";

    public string MessageId { get; set; } = string.Empty;

    public string TransactionMarker { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } =
        DateTime.UtcNow;

    public DateTime? PaidAtUtc { get; set; }

    public DateTime? DeliveredAtUtc { get; set; }

    public string LastError { get; set; } = string.Empty;
}

public class ProcurementCargoSelection
{
    public string itemId { get; set; } = string.Empty;
    public int quantity { get; set; }
}
