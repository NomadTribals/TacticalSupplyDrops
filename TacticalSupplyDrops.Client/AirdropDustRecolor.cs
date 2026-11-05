
using System;
using System.Collections.Generic;
using EFT.Airdrop;
using HarmonyLib;
using BepInEx.Logging;
using UnityEngine;

namespace TacticalSupplyDrops.Client
{
    [HarmonyPatch(
        typeof(ClientAirDrop),
        nameof(ClientAirDrop.ManualUpdate))]
    public static class AirdropDustRecolor
    {
        private static ManualLogSource _logger;

        private static readonly HashSet<int> RecoloredAirdrops =
            new HashSet<int>();

        public static void Initialize(ManualLogSource logger)
        {
            _logger = logger;
        }

        [HarmonyPostfix]
        public static void ManualUpdatePostfix(
            ClientAirDrop __instance)
        {
            if (__instance == null ||
                __instance._airdropDust == null)
            {
                return;
            }

            GameObject dustObject = __instance._airdropDust;

            // The landing effect is inactive until the cargo lands.
            if (!dustObject.activeInHierarchy)
            {
                return;
            }

            int instanceId = dustObject.GetInstanceID();

            // Only recolor each landing effect once.
            if (RecoloredAirdrops.Contains(instanceId))
            {
                return;
            }

            try
            {
                ParticleSystem particleSystem =
                    dustObject.GetComponent<ParticleSystem>();

                if (particleSystem == null)
                {
                    _logger?.LogWarning(
                        "[TSD] Airdrop landing dust has no ParticleSystem.");

                    return;
                }

                ParticleSystem.MainModule main =
                    particleSystem.main;

                // Keep the starting colour neutral.
                // Preserve the original alpha range.
                main.startColor =
                    new ParticleSystem.MinMaxGradient(
                        new Color(1f, 1f, 1f, 0.6117647f),
                        new Color(1f, 1f, 1f, 0.8f));

                // Create a warm orange gradient.
                Gradient orangeGradient = new Gradient();

                GradientColorKey[] colorKeys =
                {
                    new GradientColorKey(
                        new Color(1f, 0.35f, 0f),
                        0f),

                    new GradientColorKey(
                        new Color(1f, 0.65f, 0.10f),
                        0.232f),

                    new GradientColorKey(
                        new Color(1f, 0.35f, 0f),
                        0.671f)
                };

                GradientAlphaKey[] alphaKeys =
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.188f),
                    new GradientAlphaKey(0f, 1f)
                };

                orangeGradient.SetKeys(
                    colorKeys,
                    alphaKeys);

                ParticleSystem.ColorOverLifetimeModule colorModule =
                    particleSystem.colorOverLifetime;

                colorModule.enabled = true;

                colorModule.color =
                    new ParticleSystem.MinMaxGradient(
                        orangeGradient);

                RecoloredAirdrops.Add(instanceId);

                _logger?.LogInfo(
                    "[TSD] Successfully recolored an airdrop landing effect orange. " +
                    "Instance ID: " + instanceId);
            }
            catch (Exception ex)
            {
                _logger?.LogError(
                    "[TSD] Could not recolor airdrop landing effect: " +
                    ex);
            }
        }
    }
}
