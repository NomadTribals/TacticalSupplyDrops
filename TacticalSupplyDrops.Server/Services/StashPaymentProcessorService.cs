
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.ItemEvent;

namespace TacticalSupplyDrops.Server.Services;

[Injectable]
public class StashPaymentProcessorService(
    StashPaymentPlanService paymentPlanService,
    StashPaymentDeductionService deductionService)
{
    public bool TryProcessPayment(
        PmcData profile,
        MongoId sessionId,
        ItemEventRouterResponse output,
        int amount)
    {
        // First, create a plan for paying the requested amount.
        if (!paymentPlanService.TryCreatePlan(
            profile,
            amount,
            out var deductions))
        {
            return false;
        }

        // Then apply the deductions to the player's stash.
        return deductionService.TryApplyPlan(
            profile,
            sessionId,
            output,
            deductions);
    }
}
