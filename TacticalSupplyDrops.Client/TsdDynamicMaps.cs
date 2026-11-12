using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using EFT.SynchronizableObjects;
using HarmonyLib;

namespace TacticalSupplyDrops.Client
{
    // Optional adapter. No Dynamic Maps assembly reference or game landing event is required.
    internal static class TsdDynamicMaps
    {
        private static readonly HashSet<AirdropSynchronizableObject> Active = new HashSet<AirdropSynchronizableObject>();
        private static readonly Dictionary<AirdropSynchronizableObject,string> Labels = new Dictionary<AirdropSynchronizableObject,string>();
        private static readonly List<WeakReference> Providers = new List<WeakReference>();
        private static bool checkedCompatibility;
        private static bool ready;
        private static FieldInfo dropsField, landedField, markersField;
        private static MethodInfo removeMarker;
        private static ManualLogSource logger;
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static bool Initialize(ManualLogSource log)
        {
            if (checkedCompatibility) return ready;
            checkedCompatibility = true; logger = log;
            if (!Chainloader.PluginInfos.TryGetValue("com.mpstark.dynamicmaps", out var plugin)) return false;
            try
            {
                var assembly = plugin.Instance.GetType().Assembly;
                var patch = assembly.GetType("DynamicMaps.Patches.AirdropBoxOnBoxLandPatch", true);
                var provider = assembly.GetType("DynamicMaps.DynamicMarkers.AirdropMarkerProvider", true);
                dropsField = patch.GetField("Airdrops", Static);
                landedField = patch.GetField("OnAirdropLanded", Static);
                markersField = provider.GetField("_airdropMarkers", Instance);
                removeMarker = provider.GetMethod("TryRemoveMarker", Instance);
                var add = provider.GetMethod("TryAddMarker", Instance);
                if (dropsField == null || landedField == null || markersField == null || removeMarker == null || add == null
                    || !(dropsField.GetValue(null) is IList)) throw new InvalidOperationException("Airdrop marker API differs from the supported adapter.");
                new Harmony("com.nomadtribals.tsd.dynamicmaps").Patch(add,
                    postfix: new HarmonyMethod(typeof(TsdDynamicMaps).GetMethod(nameof(AfterAddMarker), Static)));
                ready = true;
                logger.LogInfo("[TSD] Optional Dynamic Maps courier marker adapter ready (" + plugin.Metadata.Version + ").");
            }
            catch (Exception ex) { logger.LogWarning("[TSD] Dynamic Maps adapter unavailable; tablet/beacon remain available: " + ex.Message); }
            return ready;
        }

        internal static void Register(AirdropSynchronizableObject crate, ManualLogSource log, string label = "TSD Courier Cargo")
        {
            if (crate == null || !Initialize(log) || !Active.Add(crate)) return;
            Labels[crate] = string.IsNullOrWhiteSpace(label) ? "TSD Courier Cargo" : label;
            try
            {
                var drops = (IList)dropsField.GetValue(null);
                if (!drops.Contains(crate)) drops.Add(crate);
                // Notify only Dynamic Maps subscribers, as its own landing patch does.
                var handlers = landedField.GetValue(null) as Delegate;
                if (handlers != null)
                    foreach (var handler in handlers.GetInvocationList())
                        try { handler.DynamicInvoke(crate); }
                        catch (Exception ex) { logger.LogDebug("Courier map refresh deferred: " + ex.Message); }
                logger.LogInfo("[TSD] " + Labels[crate] + " registered with Dynamic Maps airdrop markers.");
            }
            catch (Exception ex) { Remove(crate); logger.LogWarning("[TSD] Courier marker unavailable: " + ex.Message); }
        }

        private static void AfterAddMarker(object __instance, AirdropSynchronizableObject __0)
        {
            if (!Active.Contains(__0)) return;
            try
            {
                if (!Providers.Exists(reference => ReferenceEquals(reference.Target, __instance))) Providers.Add(new WeakReference(__instance));
                var markers = markersField.GetValue(__instance) as IDictionary;
                var marker = markers != null && markers.Contains(__0) ? markers[__0] : null;
                if (marker == null) return; // Honor Dynamic Maps visibility/intelligence settings.
                string text = Labels.TryGetValue(__0, out var caption) ? caption : "TSD Courier Cargo";
                marker.GetType().GetProperty("Text", Instance)?.SetValue(marker, text);
                var label = marker.GetType().GetProperty("Label", Instance)?.GetValue(marker);
                label?.GetType().GetProperty("text", Instance)?.SetValue(label, text);
            }
            catch (Exception ex) { logger?.LogDebug("Courier map label deferred: " + ex.Message); }
        }

        internal static void Remove(AirdropSynchronizableObject crate)
        {
            if (!ready || ReferenceEquals(crate, null) || !Active.Remove(crate)) return;
            Labels.Remove(crate);
            try { (dropsField.GetValue(null) as IList)?.Remove(crate); }
            catch (Exception ex) { logger?.LogDebug("Courier map list cleanup: " + ex.Message); }
            foreach (var reference in Providers.ToArray())
            {
                if (!reference.IsAlive) { Providers.Remove(reference); continue; }
                try { removeMarker.Invoke(reference.Target, new object[] { crate }); }
                catch (Exception ex) { logger?.LogDebug("Courier marker cleanup deferred: " + ex.Message); }
            }
        }
    }
}
