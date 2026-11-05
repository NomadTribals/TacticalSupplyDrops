
using TacticalSupplyDrops.Server.Models;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.DI.Routing;

namespace TacticalSupplyDrops.Server;

[Injectable(TypePriority = OnLoadOrder.Routers + 1)]
public class SupplyDropItemEventRouter : ItemEventRouter
{
    public SupplyDropItemEventRouter(
        SupplyDropStaticRouterCallback callback,
        ProcurementPurchaseCallback procurementCallback,
        ISptLogger<SupplyDropItemEventRouter> logger)
        : base(new ItemRouteAction[]
        {
            new ItemRouteAction<SupplyDropOrderRequest>(
                "TacticalSupplyDropsCreateOrder",
                async (url, pmcData, body, sessionId, output, cancellationToken) =>
                    await callback.HandleCreateOrder(
                        url,
                        pmcData,
                        body,
                        sessionId,
                        output,
                        cancellationToken)),

           new ItemRouteAction<ProcurementPurchaseRequest>(
    "TacticalSupplyDropsPurchaseCartridges",
    async (url, pmcData, body, sessionId, output, cancellationToken) =>
        await procurementCallback.HandlePurchase(
            url,
            pmcData,
            body,
            sessionId,
            output,
            cancellationToken))
        })
    {
        logger.Success(
            "Tactical Supply Drops item-event router was created!");
    }
}
