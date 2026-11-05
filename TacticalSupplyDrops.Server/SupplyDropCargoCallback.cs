
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Utils;
using System;
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
    JsonUtil jsonUtil)
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

        var order = await orderRepository.GetOrderAsync(
            profile.Id.Value,
            request.OrderId,
            cancellationToken);

        if (order == null)
        {
            return jsonUtil.Serialize(new
            {
                success = false,
                message = "The requested supply order could not be found."
            }, typeof(object));
        }

        try
        {
            var cargo = cargoBuilder.BuildCargo(order);

            return jsonUtil.Serialize(new
            {
                icon = "Common",
                container = cargo
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
