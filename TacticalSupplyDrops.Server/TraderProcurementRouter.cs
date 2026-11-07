using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.DI.Routing;
using SPTarkov.Server.Core.Utils;
using TacticalSupplyDrops.Server.Services;

namespace TacticalSupplyDrops.Server;

[Injectable(TypePriority = OnLoadOrder.Routers + 2)]
public class TraderProcurementRouter(JsonUtil jsonUtil, TraderProcurementService service)
    : StaticRouter(jsonUtil, new RouteAction[]
    {
        new RouteAction<TraderCatalogRequest>("/client/tactical-supply-drops/trader-catalog",
            (url, request, sessionId, output, token) => ValueTask.FromResult(service.GetCatalog(request, sessionId)))
    });

[Injectable(TypePriority = OnLoadOrder.Routers + 2)]
public class TraderProcurementItemRouter(TraderProcurementService service)
    : ItemEventRouter(new ItemRouteAction[]
    {
        new ItemRouteAction<TraderProcurementRequest>("TacticalSupplyDropsProcureTraderItem",
            (url, profile, request, sessionId, output, token) => service.Purchase(profile, request, sessionId, output, token))
    });
