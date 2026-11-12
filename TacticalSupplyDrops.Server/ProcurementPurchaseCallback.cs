
using System;
using System.Threading;
using System.Threading.Tasks;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.ItemEvent;
using TacticalSupplyDrops.Server.Models;
using TacticalSupplyDrops.Server.Services;

namespace TacticalSupplyDrops.Server;

[Injectable]
public class ProcurementPurchaseCallback(
    ISptLogger<ProcurementPurchaseCallback> logger,
    ProcurementPurchaseService purchaseService)
{
    public async ValueTask<ItemEventRouterResponse> HandlePurchase(
        string url,
        PmcData pmcData,
        ProcurementPurchaseRequest request,
        MongoId sessionId,
        ItemEventRouterResponse output,
        CancellationToken cancellationToken)
    {
        if (request == null ||
            string.IsNullOrWhiteSpace(request.RequestId) ||
            request.Quantity <= 0)
        {
            output.Warnings.Add(new Warning
            {
                ErrorMessage = "A valid request ID and purchase quantity are required."
            });

            return output;
        }

        if (pmcData == null)
        {
            output.Warnings.Add(new Warning
            {
                ErrorMessage = "Unable to retrieve the player's PMC profile."
            });

            return output;
        }

        var result = await purchaseService.PurchaseAsync(
            pmcData,
            sessionId,
            output,
            request.RequestId,
            request.Quantity,
            cancellationToken);

        if (!result.Success)
        {
            logger.Warning(
                $"Tactical Supply Drops procurement failed: {result.Message}");

            output.Warnings.Add(new Warning
            {
                ErrorMessage = result.Message
            });

            return output;
        }

        if (result.IsDuplicate)
        {
            logger.Info(
                $"Tactical Supply Drops duplicate procurement request: {request.RequestId}");

            return output;
        }

        logger.Success(
            $"Tactical Supply Drops procurement completed: " +
            $"{result.Quantity} cartridges for {result.TotalPrice} roubles.");

        return output;
    }
}
