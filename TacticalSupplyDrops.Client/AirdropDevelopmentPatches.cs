using BepInEx;
using BepInEx.Configuration;
using EFT.Airdrop;
using HarmonyLib;
using UnityEngine;

namespace TacticalSupplyDrops.Client
{
    public static class AirdropDevelopmentConfig
    {
        public static ConfigEntry<float> CrateMultiplier;


        public static void Initialize(ConfigFile config)
        {
            CrateMultiplier = config.Bind(
                "Airdrop Release Settings",
                "CrateMultiplier",
                3f,
                "Crate descent and parachute speed multiplier.");

            config.Save();
        }
    }

    [HarmonyPatch(typeof(ServerAirDrop), nameof(ServerAirDrop.Fall))]
    public static class CrateFallPatch
    {
        [HarmonyPrefix]
        public static void Prefix(ref float deltaTime)
        {
            if (AirdropDevelopmentConfig.CrateMultiplier == null)
            {
                return;
            }

            deltaTime *= AirdropDevelopmentConfig.CrateMultiplier.Value;
        }
    }

    [HarmonyPatch(typeof(ServerAirDrop), nameof(ServerAirDrop.ParashuteOpening))]
    public static class ParachuteOpeningPatch
    {
        [HarmonyPrefix]
        public static void Prefix(ref float deltaTime)
        {
            if (AirdropDevelopmentConfig.CrateMultiplier == null)
            {
                return;
            }

            deltaTime *= AirdropDevelopmentConfig.CrateMultiplier.Value;
        }
    }

    [HarmonyPatch(typeof(ServerAirDrop), "get_ParachuteSpeed")]
    public static class ParachuteLandingSpeedPatch
    {
        [HarmonyPostfix]
        public static void Postfix(ref float __result)
        {
            if (AirdropDevelopmentConfig.CrateMultiplier == null)
            {
                return;
            }

            __result *= AirdropDevelopmentConfig.CrateMultiplier.Value;
        }
    }
}