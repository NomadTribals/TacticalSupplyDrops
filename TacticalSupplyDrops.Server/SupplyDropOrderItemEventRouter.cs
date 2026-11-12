
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.DI.Routing;
using TacticalSupplyDrops.Server.Models;

namespace TacticalSupplyDrops.Server;

[Injectable(TypePriority = OnLoadOrder.Routers)]
public sealed class SupplyDropOrderItemEventRouter : ItemEventRouter
{
    public SupplyDropOrderItemEventRouter(
        SupplyDropStaticRouterCallback callback)
        : base(
        [
            new ItemRouteAction<SupplyDropOrderRequest>(
                "/client/tactical-supply-drops/create-order",
                async (
                    url,
                    pmcData,
                    request,
                    sessionId,
                    output,
                    cancellationToken
                ) => await callback.HandleCreateOrder(
                    url,
                    pmcData,
                    request,
                    sessionId,
                    output,
                    cancellationToken))
        ])
    {
    }
}
