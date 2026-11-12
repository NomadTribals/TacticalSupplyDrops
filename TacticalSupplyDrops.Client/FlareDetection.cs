
using System;
using EFT;
using EFT.InventoryLogic;
using HarmonyLib;
using BepInEx.Logging;
using UnityEngine;

namespace TacticalSupplyDrops.Client
{
    [HarmonyPatch(
        typeof(ClientGameWorld),
        nameof(ClientGameWorld.OnFlare))]
    public static class FlareDetection
    {
        private static ManualLogSource _logger;

        private static Vector3 _lastFlarePosition;
        private static float _lastFlareTime = -10f;

        private const string TsdFlareTemplateId =
    "6a0000000000000000000001";

        private const float DuplicateWindowSeconds = 2f;
        private const float DuplicateDistance = 0.1f;

        public static void Initialize(ManualLogSource logger)
        {
            _logger = logger;
        }

        [HarmonyPostfix]
        public static void OnFlarePostfix(
            IPlayer iPlayer,
            Vector3 flarePosition,
            AmmoTemplate ammoTemplate)
        {
            if (iPlayer == null || ammoTemplate == null)
            {
                return;
            }

            try
            {
                string templateId =
                    ammoTemplate._id.ToString();

                // Only the custom Tactical Supply Drops flare
                // can trigger TSD-specific behavior.
                if (!string.Equals(
                    templateId,
                    TsdFlareTemplateId,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                bool samePosition =
                    Vector3.Distance(
                        flarePosition,
                        _lastFlarePosition) <= DuplicateDistance;

                bool withinTimeWindow =
                    Time.time - _lastFlareTime <=
                    DuplicateWindowSeconds;

                if (samePosition && withinTimeWindow)
                {
                    _logger?.LogInfo(
                        "[TSD] Duplicate TSD flare notification ignored.");

                    return;
                }

                _lastFlarePosition = flarePosition;
                _lastFlareTime = Time.time;

                _logger?.LogInfo(
                    "[TSD] Tactical Supply Drops flare detected. " +
                    "Ammo: " + ammoTemplate.Name +
                    " | Template ID: " + templateId +
                    " | Player: " + iPlayer.ProfileId +
                    " | Position: " + flarePosition);
            }
            catch (Exception ex)
            {
                _logger?.LogError(
                    "[TSD] Flare detection error: " + ex);
            }
        }
    }
}
