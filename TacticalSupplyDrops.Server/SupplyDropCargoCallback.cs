
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TacticalSupplyDrops.Server.Models;
using TacticalSupplyDrops.Server.Services;

namespace TacticalSupplyDrops.Server;

[Injectable]
public class SupplyDropCargoCallback(
    SupplyOrderRepository orderRepository,
    SupplyDropCargoBuilder cargoBuilder,
    ProfileHelper profileHelper,
    JsonUtil jsonUtil, CourierConversionService conversion, FenceReputationService fenceReputation)
{
    public async ValueTask<string> HandleGetCargo(
        string url,
        SupplyDropCargoRequest request,
        MongoId sessionId,
        CancellationToken cancellationToken)
    {
        if (request == null ||
            string.IsNullOrWhiteSpace(request.OrderId))
        {
            return jsonUtil.Serialize(new
            {
                success = false,
                message = "A valid order ID is required."
            }, typeof(object));
        }

        var profile = profileHelper.GetPmcProfile(sessionId);

        if (profile == null)
        {
            return jsonUtil.Serialize(new
            {
                success = false,
                message = "Player profile could not be found."
            }, typeof(object));
        }

        var ids = request.OrderIds ?? new List<string> { request.OrderId };
        if (ids.Count == 0 || ids.Count > 256 || ids.Any(string.IsNullOrWhiteSpace) || ids.Distinct().Count() != ids.Count)
            return jsonUtil.Serialize(new { success = false, message = "The pending cargo list is invalid." }, typeof(object));
        var savedOrders = await orderRepository.GetOrdersAsync(profile.Id.Value, cancellationToken);
        var orders = new List<SupplyOrder>();
        foreach (var id in ids)
        {
            var order = savedOrders.FirstOrDefault(entry => entry.OrderId == id);
            if (order == null)
                return jsonUtil.Serialize(new { success = false, message = "A pending order could not be found for this profile." }, typeof(object));
            orders.Add(order);
        }
        try
        {
            // Fill one crate in purchase order. Retain later orders for the next crate.
            var included = new List<SupplyOrder>();
            List<SPTarkov.Server.Core.Models.Eft.Common.Tables.Item>? cargo = null;
            SupplyOrder? shipment = null;
            foreach (var order in orders)
            {
                if (order.DeliveryMethod != orders[0].DeliveryMethod) break;
                var candidate = cargoBuilder.CombineOrders(included.Append(order).ToList());
                if (candidate.TotalFootprint > (candidate.DeliveryMethod == "drone" ? DroneDeliveryPolicy.Capacity : DeliveryFootprintLimitService.MaximumFootprint) && included.Count > 0) break;
                try { cargo = cargoBuilder.BuildCargo(candidate); }
                catch (InvalidOperationException) when (included.Count > 0) { break; }
                shipment = candidate;
                included.Add(order);
            }
            if (cargo == null || shipment == null) throw new InvalidOperationException("No pending cargo fits in the crate.");
            var courierQuote = conversion.Quote(included, fenceReputation.GetReputationTier(profile));
            return jsonUtil.Serialize(new
            {
                courierTotalFee = courierQuote.TotalFee, courierAdditionalFee = courierQuote.AdditionalFee,
                icon = "Common", container = cargo, order = shipment, deliveryMethod = shipment.DeliveryMethod,
                orderIds = included.Select(order => order.OrderId).ToArray(),
                deferredCount = orders.Count - included.Count
            }, typeof(object));
        }
        catch (Exception)
        {
            return jsonUtil.Serialize(new
            {
                success = false,
                message = "The supply order cargo could not be prepared."
            }, typeof(object));
        }
    }
}
