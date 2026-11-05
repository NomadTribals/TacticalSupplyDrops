
using System;
using System.Collections.Concurrent;

using EFT;
using HarmonyLib;
using Newtonsoft.Json.Linq;

namespace TacticalSupplyDrops.Client
{
    internal static class ProcurementProfileRefresh
    {
        private const string HarmonyId =
            "com.tacticalsupplydrops.profile-refresh";

        private static readonly ConcurrentQueue<string>
            PendingResponses = new ConcurrentQueue<string>();

        private static Harmony _harmony;
        private static ClientBackendSession _activeSession;

        private static bool _initializationAttempted;
        private static bool _initialized;
        private static string _initializationError;

        public static bool Initialize(out string error)
        {
            if (_initialized)
            {
                error = null;
                return true;
            }

            if (_initializationAttempted)
            {
                error = _initializationError;
                return false;
            }

            _initializationAttempted = true;

            try
            {
                var sendCommands =
                    AccessTools.Method(
                        typeof(ClientBackendSession),
                        "SendCommands");

                var sendCallback =
                    AccessTools.Method(
                        typeof(ClientBackendSession),
                        "SendCallback");

                if (sendCommands == null && sendCallback == null)
                {
                    throw new InvalidOperationException(
                        "Could not locate EFT backend methods.");
                }

                var harmony = new Harmony(HarmonyId);

                var postfix = new HarmonyMethod(
                    typeof(ProcurementProfileRefresh),
                    nameof(CaptureBackendSession));

                if (sendCommands != null)
                {
                    harmony.Patch(
                        sendCommands,
                        postfix: postfix);
                }

                if (sendCallback != null)
                {
                    harmony.Patch(
                        sendCallback,
                        postfix: postfix);
                }

                _harmony = harmony;
                _initialized = true;

                error = null;
                return true;
            }
            catch (Exception ex)
            {
                _initializationError = ex.ToString();
                error = _initializationError;
                return false;
            }
        }

        private static void CaptureBackendSession(
            object __instance)
        {
            if (__instance is ClientBackendSession session)
            {
                _activeSession = session;
            }
        }

        public static void QueueResponse(string json)
        {
            if (!string.IsNullOrWhiteSpace(json))
            {
                PendingResponses.Enqueue(json);
            }
        }

        public static int ApplyPending(
            Action<string> logInfo,
            Action<string> logError)
        {
            int appliedCount = 0;

            while (PendingResponses.TryDequeue(out string json))
            {
                try
                {
                    ClientBackendSession session = _activeSession;

                    if (session == null)
                    {
                        PendingResponses.Enqueue(json);

                        logError(
                            "TSD is waiting for EFT's live backend session.");

                        break;
                    }

                    // SPT wraps the actual profile changes inside "data".
                    JObject response = JObject.Parse(json);

                    JToken data = response["data"];

                    if (data == null)
                    {
                        logError(
                            "TSD response did not contain a data object.");

                        continue;
                    }

                    ClientBackendSession.QueueData queueData =
                        data.ToObject<ClientBackendSession.QueueData>();

                    if (queueData == null ||
                        queueData.ProfileChanges == null ||
                        queueData.ProfileChanges.Count == 0)
                    {
                        logError(
                            "TSD response data contained no profile changes.");

                        continue;
                    }

                    logInfo(
                        "TSD found " +
                        queueData.ProfileChanges.Count +
                        " EFT profile change(s).");

                    foreach (var entry in queueData.ProfileChanges)
                    {
                        if (session._profileUpdaters.TryGetValue(
                            entry.Key,
                            out var updater))
                        {
                            updater.UpdateProfile(entry.Value);

                            appliedCount++;

                            logInfo(
                                "TSD successfully applied EFT profile changes for: " +
                                entry.Key);
                        }
                        else
                        {
                            logError(
                                "TSD could not find an EFT profile updater for: " +
                                entry.Key);
                        }
                    }
                }
                catch (Exception ex)
                {
                    logError(
                        "TSD could not apply procurement profile changes: " +
                        ex);
                }
            }

            return appliedCount;
        }
    }
}
