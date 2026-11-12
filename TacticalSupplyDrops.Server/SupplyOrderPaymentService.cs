using SPTarkov.DI.Annotations;

namespace TacticalSupplyDrops.Server.Services;

[Injectable]
public class SupplyOrderPaymentService(
    StashCurrencyService stashCurrencyService)
{
    public bool CanAffordOrder(
        SPTarkov.Server.Core.Models.Eft.Common.PmcData profile,
        int orderPrice)
    {
        if (profile == null || orderPrice < 0)
        {
            return false;
        }

        int availableRoubles =
            stashCurrencyService.GetRoublesInStash(profile);

        return availableRoubles >= orderPrice;
    }
}