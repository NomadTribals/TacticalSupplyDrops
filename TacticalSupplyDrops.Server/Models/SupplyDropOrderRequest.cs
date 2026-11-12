
using System.Collections.Generic;
using SPTarkov.Server.Core.Models.Eft.Common.Request;
using SPTarkov.Server.Core.Models.Utils;

namespace TacticalSupplyDrops.Server.Models;

public record SupplyDropOrderRequest : BaseInteractionRequestData
{
    public string DeliveryMethod { get; set; } = "standard";
    public string RequestId { get; set; } = string.Empty;

    public List<string> SelectedItemIds { get; set; } = new();

    public Dictionary<string, int> SelectedItemQuantities { get; set; } = new();
}

public record SupplyDropOrderLookupRequest : BaseInteractionRequestData, IRequestData
{
    public string RequestId { get; set; } = string.Empty;
}

public record SupplyDropCargoRequest : BaseInteractionRequestData, IRequestData
{
    public string OrderId { get; set; } = string.Empty;
    public List<string>? OrderIds { get; set; }
}

public record SupplyDropCourierConversionRequest : BaseInteractionRequestData
{
    public List<string> OrderIds { get; set; } = new();
    public int ExpectedAdditionalFee { get; set; }
}
