
using System;
using System.IO;
using System.Threading.Tasks;

using BepInEx.Logging;
using Comfort.Common;
using EFT;
using HarmonyLib;
using JsonType;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace TacticalSupplyDrops.Client
{
    [HarmonyPatch(
        typeof(ClientBackendSession),
        nameof(ClientBackendSession.LoadLootContainerData))]
    public static class TacticalSupplyDropsAirdropCargoPatch
    {
        // This must match AirDropTemplateId on the custom TSD flare.
        public const string TsdAirdropLootId = "TSD_ACTIVE_ORDER";

        private static readonly string CargoFilePath =
            Path.Combine(BepInEx.Paths.ConfigPath, "TacticalSupplyDrops", "cargo.json");

        private static readonly ManualLogSource Logger =
            BepInEx.Logging.Logger.CreateLogSource(
                "TacticalSupplyDrops.AirdropCargo");

        [HarmonyPrefix]
        public static bool LoadLootContainerDataPrefix(
            string containerId,
            ref Task<Result<AirdropContainerData>> __result)
        {
            // Let every ordinary airdrop use the original game method.
            if (!string.Equals(
                containerId,
                TsdAirdropLootId,
                StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            try
            {
                if (!File.Exists(CargoFilePath))
                {
                    throw new FileNotFoundException(
                        "The TSD cargo file does not exist.",
                        CargoFilePath);
                }

                string fileContents =
                    File.ReadAllText(CargoFilePath);

                if (string.IsNullOrWhiteSpace(fileContents))
                {
                    throw new InvalidOperationException(
                        "The TSD cargo file is empty.");
                }

                JObject envelope =
                    JObject.Parse(fileContents);

                string orderId =
                    envelope["orderId"]?.Value<string>();

                if (string.IsNullOrWhiteSpace(orderId))
                {
                    throw new InvalidOperationException(
                        "The TSD cargo file has no OrderId.");
                }

                JObject cargoJson =
                    envelope["cargo"] as JObject;

                if (cargoJson == null)
                {
                    throw new InvalidOperationException(
                        "The TSD cargo file has no cargo object.");
                }

                AirdropContainerData cargoData =
                    JsonConvert.DeserializeObject<AirdropContainerData>(
                        cargoJson.ToString(Formatting.None));

                if (cargoData == null ||
                    cargoData.data == null ||
                    cargoData.data.Length == 0)
                {
                    throw new InvalidOperationException(
                        "The TSD cargo data contains no items.");
                }

                if (string.IsNullOrWhiteSpace(cargoData.Icon))
                {
                    throw new InvalidOperationException(
                        "The TSD cargo data has no crate icon.");
                }

                // Supply the game with the same Result type returned
                // by its original backend request.
                __result = Task.FromResult(
                    new Result<AirdropContainerData>(cargoData));

                Logger.LogInfo(
                    "[TSD] Supplying custom cargo for order " +
                    orderId +
                    " (" + cargoData.data.Length + " items).");

                // Skip the vanilla backend request for this identifier.
                return false;
            }
            catch (Exception ex)
            {
                Logger.LogError(
                    "[TSD] Could not load custom airdrop cargo: " + ex);

                // Return a failed result rather than allowing the
                // special identifier to fall through to vanilla loot.
                __result = Task.FromResult(
                    new Result<AirdropContainerData>(
                        null,
                        "Tactical Supply Drops cargo could not be loaded."));

                return false;
            }
        }
    }
}
