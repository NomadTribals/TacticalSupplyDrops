
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Utils;
using TacticalSupplyDrops.Server.Models;

namespace TacticalSupplyDrops.Server;

[Injectable(TypePriority = OnLoadOrder.Routers + 1)]
public class SupplyDropStaticRouter : StaticRouter
{
    public SupplyDropStaticRouter(
        JsonUtil jsonUtil,
        SupplyDropStaticRouterCallback callback,
        SupplyDropCargoCallback cargoCallback,
        ISptLogger<SupplyDropStaticRouter> logger)
        : base(jsonUtil, new RouteAction[]
        {
            new RouteAction<EmptyRequestData>(
                "/client/tactical-supply-drops/test",
                async (url, info, sessionId, output, cancellationToken) =>
                    await callback.HandleTest(url, sessionId)),

            new RouteAction<EmptyRequestData>(
                "/client/tactical-supply-drops/get-stash",
                async (url, info, sessionId, output, cancellationToken) =>
                    await callback.HandleGetStash(url, sessionId)),

            new RouteAction<SupplyDropOrderLookupRequest>(
                "/client/tactical-supply-drops/get-order",
                async (url, info, sessionId, output, cancellationToken) =>
                    await callback.HandleGetOrder(
                        url,
                        info,
                        sessionId,
                        cancellationToken)),

            new RouteAction<SupplyDropCargoRequest>(
                "/client/tactical-supply-drops/get-cargo",
                async (url, info, sessionId, output, cancellationToken) =>
                    await cargoCallback.HandleGetCargo(
                        url,
                        info,
                        sessionId,
                        cancellationToken))
        })
    {
        logger.Success(
            "Tactical Supply Drops custom router was created!");
    }
}
