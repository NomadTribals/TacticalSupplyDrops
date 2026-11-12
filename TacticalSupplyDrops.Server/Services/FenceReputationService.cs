
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Enums;

namespace TacticalSupplyDrops.Server.Services;

[Injectable]
public class FenceReputationService
{
    public FenceReputationTier GetReputationTier(PmcData profile)
    {
        // If Fence information is missing, use neutral pricing.
        if (profile?.TradersInfo == null ||
            !profile.TradersInfo.TryGetValue(Traders.FENCE, out var fenceInfo) ||
            fenceInfo?.Standing == null)
        {
            return FenceReputationTier.Neutral;
        }

        double standing = fenceInfo.Standing.Value;

        if (standing < 0)
        {
            return FenceReputationTier.Poor;
        }

        if (standing < 1)
        {
            return FenceReputationTier.Neutral;
        }

        if (standing < 2)
        {
            return FenceReputationTier.Good;
        }

        return FenceReputationTier.Excellent;
    }
}
