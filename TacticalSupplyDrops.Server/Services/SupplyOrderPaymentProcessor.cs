using System.Collections.Generic;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.ItemEvent;

namespace TacticalSupplyDrops.Server.Services;

[Injectable]
public class SupplyOrderPaymentProcessor(
    StashPaymentPlanService paymentPlanService,
    StashPaymentDeductionService paymentDeductionService)
{
    public bool TryCreatePaymentPlan(
        PmcData profile,
        int amount,
        out List<StashPaymentDeduction> deductions)
    {
        return paymentPlanService.TryCreatePlan(
            profile,
            amount,
            out deductions);
    }

    public bool TryProcessPayment(
        PmcData profile,
        MongoId sessionId,
        ItemEventRouterResponse output,
        IEnumerable<StashPaymentDeduction> deductions)
    {
        return paymentDeductionService.TryApplyPlan(
            profile,
            sessionId,
            output,
            deductions);
    }
}