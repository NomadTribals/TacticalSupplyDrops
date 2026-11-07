
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
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

        private static readonly HashSet<int> TaggedAirdrops =
            new HashSet<int>();


        private static bool _capturingDropPoint;
        private static Vector3? _capturedDropPoint;
        private static Vector3? _tsdDropPoint;
        private static DateTime _tsdDropPointExpiresUtc;

        public static void Initialize(ManualLogSource logger)
        {
            _logger = logger;
        }

        public static void BeginTsdDropPointCapture()
        {
            _capturedDropPoint = null;
            _capturingDropPoint = true;
        }

        public static bool EndTsdDropPointCapture(out Vector3 dropPoint)
        {
            _capturingDropPoint = false;
            if (!_capturedDropPoint.HasValue)
            {
                dropPoint = Vector3.zero;
                return false;
            }

            dropPoint = _capturedDropPoint.Value;
            _tsdDropPoint = dropPoint;
            _tsdDropPointExpiresUtc = DateTime.UtcNow.AddMinutes(30);
            return true;
        }

        public static void CaptureSelectedDropPoint(GameObject point)
        {
            if (!_capturingDropPoint || point == null)
            {
                return;
            }

            _capturedDropPoint = point.transform.position;
        }

        [HarmonyPostfix]
        public static void ManualUpdatePostfix(
            ClientAirDrop __instance)
        {
            if (__instance == null)
            {
                return;
            }

            int instanceId = RuntimeHelpers.GetHashCode(__instance);
            if (!TaggedAirdrops.Contains(instanceId))
            {
                if (!_tsdDropPoint.HasValue ||
                    DateTime.UtcNow > _tsdDropPointExpiresUtc ||
                    HorizontalDistanceSquared(
                        __instance._destinationPosition,
                        _tsdDropPoint.Value) > 144f)
                {
                    return;
                }

                TaggedAirdrops.Add(instanceId);
                _logger?.LogInfo(
                    "[TSD] Identified the TSD airdrop at its selected " +
                    "drop point; applying orange smoke.");
            }

            try
            {
                RecolorEffect(__instance._airdropFlare);
                RecolorEffect(__instance._airdropDust);
            }
            catch (Exception ex)
            {
                _logger?.LogError("[TSD] Could not recolor TSD smoke: " + ex);
            }
        }

        public static void ReleaseTsdDrop(ClientAirDrop drop)
        {
            if (drop == null) return;
            TaggedAirdrops.Remove(RuntimeHelpers.GetHashCode(drop));
            if (drop._airdropFlare != null)
                drop._airdropFlare.GetComponent<TsdSmokeTint>()?.Restore();
            if (drop._airdropDust != null)
                drop._airdropDust.GetComponent<TsdSmokeTint>()?.Restore();
        }

        private static float HorizontalDistanceSquared(
            Vector3 first,
            Vector3 second)
        {
            float x = first.x - second.x;
            float z = first.z - second.z;
            return x * x + z * z;
        }

        private static void RecolorEffect(GameObject effectObject)
        {
            if (effectObject == null)
            {
                return;
            }

            TsdSmokeTint tint = effectObject.GetComponent<TsdSmokeTint>();
            if (tint == null) tint = effectObject.AddComponent<TsdSmokeTint>();
            if (tint.Apply())
            {
                _logger?.LogInfo(
                    "[TSD] Applied orange material and particle colors to a TSD smoke effect.");
            }
        }
    }

    [HarmonyPatch(
        typeof(AirdropManager),
        nameof(AirdropManager.TakeAirdropPoint))]
    public static class TsdAirdropPointCapture
    {
        [HarmonyPostfix]
        public static void Postfix(GameObject __result)
        {
            AirdropDustRecolor.CaptureSelectedDropPoint(__result);
        }
    }
    [HarmonyPatch(typeof(ClientAirDrop), nameof(ClientAirDrop.ReturnToPool))]
    public static class TsdAirdropSmokePoolReset
    {
        [HarmonyPrefix]
        public static void Prefix(ClientAirDrop __instance)
        {
            AirdropDustRecolor.ReleaseTsdDrop(__instance);
        }
    }
}
