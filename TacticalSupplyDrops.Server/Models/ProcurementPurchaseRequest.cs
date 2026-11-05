
using SPTarkov.Server.Core.Models.Eft.Common.Request;
using SPTarkov.Server.Core.Models.Utils;

namespace TacticalSupplyDrops.Server.Models;

public record ProcurementPurchaseRequest : BaseInteractionRequestData, IRequestData
{
    public string RequestId { get; set; } = string.Empty;

    public int Quantity { get; set; }
}
