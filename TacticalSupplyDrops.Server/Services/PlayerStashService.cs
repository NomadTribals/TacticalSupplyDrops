using SPTarkov.DI.Annotations;
using System.Collections.Generic;
using SPTarkov.Server.Core.Models.Eft.Common;
using TacticalSupplyDrops.Server.Services;

namespace TacticalSupplyDrops.Server.Services;

[Injectable]
public class PlayerStashService(
    StashInventoryService stashInventoryService,
    StashItemDetailsService stashItemDetailsService)
{
    public List<StashItemDetails> GetPlayerStashDetails(PmcData profile)
    {
        var stashItems = stashInventoryService.GetStashItems(profile);

        return stashItemDetailsService.GetItemDetails(stashItems);
    }
}