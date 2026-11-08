using System;
using System.Collections.Generic;
using EFT.Interactive;
using EFT.InventoryLogic;
using HarmonyLib;

namespace TacticalSupplyDrops.Client
{
    [HarmonyPatch(typeof(LootItem), nameof(LootItem.CreateLootContainer))]
    public static class TsdCargoCrateName
    {
        private static readonly HashSet<string> ShipmentRoots = new HashSet<string>(StringComparer.Ordinal);
        private static readonly object Gate = new object();

        internal static void Register(string rootId)
        {
            if (string.IsNullOrWhiteSpace(rootId)) return;
            lock (Gate) ShipmentRoots.Add(rootId);
        }

        [HarmonyPrefix]
        public static void Prefix(Item item, ref string name)
        {
            if (item == null) return;
            lock (Gate)
                if (ShipmentRoots.Contains(item.Id)) name = "TSD Cargo Crate";
        }
    }
}
