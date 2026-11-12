using System;
using System.Collections.Generic;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;

namespace TacticalSupplyDrops.Server.Services;

public record StashPaymentDeduction(
    string ItemId,
    int QuantityToRemove
);

[Injectable]
public class StashPaymentPlanService(
    StashInventoryService stashInventoryService)
{
    public bool TryCreatePlan(
        PmcData profile,
        int amount,
        out List<StashPaymentDeduction> deductions)
    {
        deductions = new List<StashPaymentDeduction>();

        if (profile == null || amount <= 0)
        {
            return false;
        }

        var stashItems = stashInventoryService.GetStashItems(profile);

        var roubleStacks = stashItems
            .Where(item => item.Template == Money.ROUBLES)
            .ToList();

        Console.WriteLine(
            $"TSD PAYMENT DIAGNOSTIC: Found {roubleStacks.Count} rouble stacks.");

        Console.WriteLine(
            $"TSD PAYMENT DIAGNOSTIC: Total roubles found: " +
            $"{roubleStacks.Sum(item => item.GetItemStackSize())}.");

        int remaining = amount;

        foreach (Item stack in roubleStacks)
        {
            if (remaining <= 0)
            {
                break;
            }

            int available = stack.GetItemStackSize();
            int toRemove = Math.Min(available, remaining);

            deductions.Add(new StashPaymentDeduction(
                (string)stack.Id,
                toRemove));
            Console.WriteLine(
    $"TSD PAYMENT DIAGNOSTIC: Selected stack {stack.Id}, " +
    $"parent {stack.ParentId}, removing {toRemove} roubles.");

            remaining -= toRemove;
        }

        if (remaining > 0)
        {
            deductions.Clear();
            return false;
        }

        return true;
    }
}
