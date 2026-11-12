
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Bootstrap;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using TsdTechnologies.Api;
using System.Reflection;

namespace TacticalSupplyDrops.Overlay
{
    [Serializable]
    public class OverlayStashResponse
    {
        public bool success;
        public List<OverlayStashItem> items;
    }

    [Serializable]
    public class OverlayStashItem
    {
        public string Id;
        public string TemplateId;
        public string Name;
        public string ParentId;
        public string SlotId;
        public int StackCount;
        public int BaseValue;
        public int Width;
        public int Height;
        public string IconUrl;
        public string Tag;
        public string Category;
    }

    [BepInPlugin("com.nomadtribals.tacticalsupplydrops.overlay","TSD Logistics - Tablet App","3.1.0")]
    [BepInDependency("com.nomadtribals.tacticalsupplydrops")]
    [BepInDependency("com.nomadtribals.tsdtechnologies.overlay", "1.0.0")]
    public class TacticalSupplyDropsOverlayPlugin:BaseUnityPlugin
    {
        private ITabletAppHost overlay;
        private ConfigFile unifiedConfig;
        private ConfigEntry<string> lastTrader,procurementSearchText;
        private float responseCheckTimer;
        private string lastDeliveryStatusJson="",lastTabletAccessJson="",lastGroundCargoLocatorJson="";
        private static readonly string GroundCargoLocatorPath=Path.Combine(Paths.ConfigPath,"TacticalSupplyDrops","ground-cargo-locator.json");
        [Serializable]private class PreferenceChange{public string key,value;}
        [Serializable]private class Preferences{public string trader,search;}
        private static readonly string ProcurementCatalogRequestPath =
            Path.Combine(BepInEx.Paths.ConfigPath, "TacticalSupplyDrops", "procurement-catalog-request.json");
        private static readonly string ProcurementCatalogResponsePath =
            Path.Combine(BepInEx.Paths.ConfigPath, "TacticalSupplyDrops", "procurement-catalog-response.json");

        private static readonly string StashFilePath =
            Path.Combine(BepInEx.Paths.ConfigPath, "TacticalSupplyDrops", "stash.json");
        private static readonly string StashRefreshCompletePath =
            Path.Combine(BepInEx.Paths.ConfigPath, "TacticalSupplyDrops", "stash-refresh-complete.json");
        private static readonly string StashRefreshRequestPath =
            Path.Combine(BepInEx.Paths.ConfigPath, "TacticalSupplyDrops", "stash-refresh-request.json");
        private static readonly string OrderLookupRequestPath =
            Path.Combine(BepInEx.Paths.ConfigPath, "TacticalSupplyDrops", "order-lookup-request.json");
        private static readonly string OrderFolderPath =
            Path.Combine(BepInEx.Paths.ConfigPath, "TacticalSupplyDrops");

        private static readonly string TabletAccessPath =
            Path.Combine(BepInEx.Paths.ConfigPath, "TacticalSupplyDrops", "tablet-access.json");

        private static readonly string DeliveryDispatchRequestPath =
            Path.Combine(BepInEx.Paths.ConfigPath, "TacticalSupplyDrops", "delivery-dispatch-request.json");

        private static readonly string DeliveryDispatchStatusPath =
            Path.Combine(BepInEx.Paths.ConfigPath, "TacticalSupplyDrops", "delivery-dispatch-status.json");

        private static readonly string OrderLookupResponsePath =
            Path.Combine(BepInEx.Paths.ConfigPath, "TacticalSupplyDrops", "order-lookup-response.json");

        private static readonly string CargoFilePath =
            Path.Combine(BepInEx.Paths.ConfigPath, "TacticalSupplyDrops", "cargo.json");

        private static readonly string OrderRequestPath =
            Path.Combine(BepInEx.Paths.ConfigPath, "TacticalSupplyDrops", "order-request.json");

        private static readonly string OrderResponsePath =
            Path.Combine(BepInEx.Paths.ConfigPath, "TacticalSupplyDrops", "order-response.json");

        private static readonly string ProcurementPurchaseRequestPath =
            Path.Combine(BepInEx.Paths.ConfigPath, "TacticalSupplyDrops", "procurement-purchase-request.json");

        private static readonly string ProcurementPurchaseResponsePath =
            Path.Combine(BepInEx.Paths.ConfigPath, "TacticalSupplyDrops", "procurement-purchase-response.json");

        private void Awake(){
            unifiedConfig=Chainloader.PluginInfos["com.nomadtribals.tacticalsupplydrops"].Instance.Config;
            var migration=new TsdUnifiedConfig(unifiedConfig,Config.ConfigFilePath);
            lastTrader=migration.Bind("Procurement","LastTrader","","Last selected trader.");procurementSearchText=migration.Bind("Procurement","Search","","Last Procurement search.");
            TabletRuntime.Register(new TabletApplication{Id="tsd.logistics",Name="TSD Logistics",IconSvg=Resource("LogisticsIcon.svg"),Document=Resource("LogisticsApp.html"),Channels=new[]{"stash","tsd-preferences","tsd-courier","tsd-drone"},LogPage="log",Attach=Attach});
            Logger.LogInfo("TSD Logistics app registered with Field OS.");
        }
        private static string Resource(string name){using(var stream=typeof(TacticalSupplyDropsOverlayPlugin).Assembly.GetManifestResourceStream("TacticalSupplyDrops.Overlay."+name))using(var reader=new StreamReader(stream))return reader.ReadToEnd();}
        private void Attach(ITabletAppHost host){overlay=host;host.ChannelMessage+=HandleChannel;host.VisibilityChanged+=shown=>{if(shown){Directory.CreateDirectory(OrderFolderPath);File.WriteAllText(StashRefreshRequestPath,"refresh");SendStashData();}else host.ExecuteScript("window.tsd.setTabletVisible(false);");};}
        private void Update(){responseCheckTimer+=Time.unscaledDeltaTime;if(responseCheckTimer<.25f)return;responseCheckTimer=0;if(overlay==null)return;CheckOrderResponse();CheckProcurementPurchaseResponse();CheckProcurementCatalogResponse();CheckForStashRefreshComplete();CheckForOrderLookupComplete();CheckDeliveryDispatchStatus();CheckTabletAccessState();CheckGroundCargoLocator();}
        private void HandleChannel(string channel,string payload){
            switch(channel){
                case "tsd-ready":lastGroundCargoLocatorJson="";lastDeliveryStatusJson="";overlay.Post("tsd-preferences",JsonUtility.ToJson(new Preferences{trader=lastTrader.Value,search=procurementSearchText.Value}));CheckGroundCargoLocator();SendTabletAccessState();SendActiveCargo();CheckDeliveryDispatchStatus();SendStashData();break;
                case "tsd-submit-order":SaveOrderRequest(payload);break;
                case "tsd-dispatch-delivery":SaveDeliveryDispatchRequest(payload);break;
                case "tsd-procurement-purchase":SaveProcurementPurchaseRequest(payload);break;
                case "tsd-procurement-refresh":Directory.CreateDirectory(OrderFolderPath);File.WriteAllText(ProcurementCatalogRequestPath,payload);break;
                case "tsd-refresh-stash":Directory.CreateDirectory(OrderFolderPath);File.WriteAllText(StashRefreshRequestPath,"refresh");break;
                case "tsd-lookup-order":if(!string.IsNullOrWhiteSpace(payload)){Directory.CreateDirectory(OrderFolderPath);File.WriteAllText(OrderLookupRequestPath,payload);}break;
                case "tsd-test":overlay.ExecuteScript("window.tsd.onResponseReceived('TSD communication confirmed.');");break;
                case "tsd-preference-set":if(payload!=null&&payload.Length<=4096){var value=JsonUtility.FromJson<PreferenceChange>(payload);if(value?.value!=null){if(value.key=="tsd-last-trader"&&value.value.Length<=128)lastTrader.Value=value.value;else if(value.key=="tsd-procurement-search"&&value.value.Length<=512)procurementSearchText.Value=value.value;}}break;
            }
        }
        private bool HasFreshTabletAccess(out bool inRaid)
        {
            var state=TabletRuntime.Context;inRaid=state.InRaid;
            return state.IsFresh&&state.HasTablet;
        }

        [Serializable]
        private class GroundCargoLocatorState
        {
            public string method;
            public bool available;
            public bool testOnly;
            public string phase;
            public string message;
            public bool collected;
            public string orderId;
            public float distance;
            public string direction;
            public string level;
            public long updatedAtUnixMs;
        }

        private void CheckGroundCargoLocator()
        {
            if (overlay == null) return;
            try
            {
                GroundCargoLocatorState state = null;
                if (File.Exists(GroundCargoLocatorPath))
                    state = JsonUtility.FromJson<GroundCargoLocatorState>(File.ReadAllText(GroundCargoLocatorPath));
                if (state == null || (!state.available && state.phase != "pending" && state.phase != "unavailable") || state.updatedAtUnixMs <= 0 ||
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - state.updatedAtUnixMs > 3000)
                    state = new GroundCargoLocatorState();
                string dronePath = Path.Combine(OrderFolderPath, "drone-cargo-locator.json");
                if (File.Exists(dronePath))
                {
                    var drone = JsonUtility.FromJson<GroundCargoLocatorState>(File.ReadAllText(dronePath));
                    if (drone != null && (drone.available || drone.phase == "pending") &&
                        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - drone.updatedAtUnixMs <= 3000 &&
                        drone.updatedAtUnixMs >= state.updatedAtUnixMs) state = drone;
                }
                string json = JsonUtility.ToJson(state);
                if (json == lastGroundCargoLocatorJson) return;
                overlay.Post("tsd-courier", json, TabletPostOptions.LatestOnly);
                lastGroundCargoLocatorJson = json;
            }
            catch (Exception ex) { Logger.LogDebug("Courier locator read deferred: " + ex.Message); }
        }

        private void CheckTabletAccessState()
        {
            try
            {
                if (!File.Exists(TabletAccessPath)) return;
                string json = File.ReadAllText(TabletAccessPath);
                if (string.IsNullOrWhiteSpace(json) || json == lastTabletAccessJson)
                    return;

                lastTabletAccessJson = json;
                SendTabletAccessState();
            }
            catch (Exception ex)
            {
                Logger.LogDebug("TSD could not refresh tablet access display: " + ex.Message);
            }
        }

        private void SendTabletAccessState()
        {
            if (overlay == null) return;
            bool allowed = HasFreshTabletAccess(out bool inRaid);
            TabletAccessMessage message = new TabletAccessMessage
            {
                hasAccess = allowed,
                inRaid = allowed && inRaid
            };
            if (allowed)
            {
                try
                {
                    var state = JsonUtility.FromJson<TabletAccessState>(File.ReadAllText(TabletAccessPath));
                    message.nickname = state?.nickname;
                    message.deliveryMethod = state?.deliveryMethod;
                    message.droneUnavailable = state?.droneUnavailable ?? false;
                    message.raidTime = state?.raidTime;
                }
                catch { message.nickname = null; }
            }
            overlay.ExecuteScript(
                "window.tsd.onTabletAccessReceived && " +
                "window.tsd.onTabletAccessReceived(" +
                JsonUtility.ToJson(message) + ");");
        }

        private void SendActiveCargo()
        {
            if (overlay == null) return;

            ActiveCargoMessage message = new ActiveCargoMessage();
            message.inRaid = HasFreshTabletAccess(out bool inRaid) && inRaid;

            try
            {
                if (File.Exists(CargoFilePath))
                {
                    CargoEnvelope envelope = JsonUtility.FromJson<CargoEnvelope>(
                        File.ReadAllText(CargoFilePath));
                    if (envelope != null && envelope.cargo != null &&
                        envelope.cargo.container != null && envelope.cargo.container.Length > 0 &&
                        !string.IsNullOrWhiteSpace(envelope.orderId))
                    {
                        message.available = true;
                        message.orderId = envelope.orderId;
                        message.itemCount = envelope.cargo.container.Length - 1;
                        message.deliveryMethod = envelope.cargo.deliveryMethod ?? "standard";
                        message.paidFee = envelope.cargo.order?.TotalPrice ?? 0;
                        message.courierTotalFee = envelope.cargo.courierTotalFee;
                        message.courierAdditionalFee = envelope.cargo.courierAdditionalFee;

                        if (File.Exists(DeliveryDispatchStatusPath))
                        {
                            DeliveryDispatchStatus status =
                                JsonUtility.FromJson<DeliveryDispatchStatus>(
                                    File.ReadAllText(DeliveryDispatchStatusPath));
                            message.retryable = status != null && status.completed && status.kind == "error" &&
                                string.Equals(status.orderId, message.orderId, StringComparison.OrdinalIgnoreCase);
                            message.alreadyDispatched = status != null &&
                                status.completed && status.kind == "success" &&
                                string.Equals(status.orderId, message.orderId,
                                    StringComparison.OrdinalIgnoreCase);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning("TSD could not read active cargo: " + ex.Message);
            }

            overlay.ExecuteScript(
                "window.tsd.onActiveCargoReceived && " +
                "window.tsd.onActiveCargoReceived(" +
                JsonUtility.ToJson(message) + ");");
        }

        private void CheckForOrderLookupComplete()
        {
            if (!File.Exists(OrderLookupResponsePath)) return;
            try
            {
                File.Delete(OrderLookupResponsePath);
                SendActiveCargo();
                Logger.LogInfo("TSD refreshed the available delivery manifest.");
            }
            catch (Exception ex)
            {
                Logger.LogWarning("TSD could not refresh the delivery manifest: " + ex.Message);
            }
        }

        private void CheckDeliveryDispatchStatus()
        {
            if (overlay == null || !File.Exists(DeliveryDispatchStatusPath)) return;
            try
            {
                string json = File.ReadAllText(DeliveryDispatchStatusPath);
                if (string.IsNullOrWhiteSpace(json) || json == lastDeliveryStatusJson)
                    return;

                lastDeliveryStatusJson = json;
                string escapedJson = json
                    .Replace("\\", "\\\\")
                    .Replace("'", "\\'")
                    .Replace("\r", "")
                    .Replace("\n", "");
                overlay.ExecuteScript(
                    "window.tsd.onDeliveryStatusReceived && " +
                    "window.tsd.onDeliveryStatusReceived('" +
                    escapedJson + "');");
            }
            catch (Exception ex)
            {
                Logger.LogDebug("TSD could not forward delivery status: " + ex.Message);
            }
        }

        private void SaveDeliveryDispatchRequest(string payload)
        {
            try
            {
                if (!HasFreshTabletAccess(out bool inRaid))
                {
                    Logger.LogWarning("TSD rejected dispatch: field terminal is not available.");
                    return;
                }

                if (!inRaid)
                {
                    Logger.LogWarning("TSD rejected dispatch: enter a raid first.");
                    return;
                }

                if (string.IsNullOrWhiteSpace(payload))
                    throw new InvalidOperationException("The dispatch request was empty.");

                Directory.CreateDirectory(OrderFolderPath);
                if (File.Exists(DeliveryDispatchRequestPath))
                {
                    Logger.LogWarning("TSD already has a delivery dispatch pending.");
                    return;
                }

                File.WriteAllText(DeliveryDispatchRequestPath, payload);
                Logger.LogInfo("TSD queued a secure cargo dispatch request.");
            }
            catch (Exception ex)
            {
                Logger.LogError("TSD could not queue cargo dispatch: " + ex);
            }
        }

        private void CheckForStashRefreshComplete()
        {
            if (!File.Exists(StashRefreshCompletePath))
            {
                return;
            }

            try
            {
                File.Delete(StashRefreshCompletePath);

                Logger.LogInfo(
                    "Tactical Supply Drops detected refreshed stash data.");

                SendStashData();

                if (overlay != null)
                {
                    overlay.ExecuteScript(
                        "if (window.onStashRefreshComplete) " +
                        "window.onStashRefreshComplete();"
                    );
                }


                Logger.LogInfo(
                    "Tactical Supply Drops sent refreshed stash data to F9.");
            }
            catch (Exception exception)
            {
                Logger.LogError(
                    "Tactical Supply Drops could not send refreshed stash data: " +
                    exception);
            }
        }

        private void SendStashData()
        {
            if (overlay == null)
            {
                return;
            }

            try
            {
                if (!File.Exists(StashFilePath))
                {
                    Logger.LogWarning(
                        "Tactical Supply Drops stash file was not found.");

                    return;
                }

                string json = File.ReadAllText(StashFilePath);

                overlay.Post("stash", json);
            }
            catch (Exception exception)
            {
                Logger.LogError(
                    "Could not send stash data to overlay: " + exception);
            }
        }

        private void CheckOrderResponse()
        {
            try
            {
                if (!File.Exists(OrderResponsePath))
                {
                    return;
                }

                string responseJson = File.ReadAllText(OrderResponsePath);

                if (string.IsNullOrWhiteSpace(responseJson))
                {
                    return;
                }

                // Remove the response so it is only processed once.
                File.Delete(OrderResponsePath);

                string escapedJson = responseJson
                    .Replace("\\", "\\\\")
                    .Replace("'", "\\'")
                    .Replace("\r", "")
                    .Replace("\n", "");

                if (overlay != null)
                {
                    overlay.ExecuteScript(
                        "window.tsd.onOrderResponseReceived && " +
                        "window.tsd.onOrderResponseReceived('" +
                        escapedJson + "');"
                    );
                }

                Logger.LogInfo(
                    "TSD received an order response from the Client plugin.");
            }
            catch (Exception exception)
            {
                Logger.LogError(
                    "TSD could not process the order response: " + exception);
            }
        }
        private void CheckProcurementCatalogResponse()
        {
            if (overlay == null || !File.Exists(ProcurementCatalogResponsePath)) return;
            try
            {
                string json = File.ReadAllText(ProcurementCatalogResponsePath);
                overlay.ExecuteScript("window.tsd.onProcurementCatalogReceived && window.tsd.onProcurementCatalogReceived(" + json + ");");
                File.Delete(ProcurementCatalogResponsePath);
            }
            catch (Exception ex)
            {
                Logger.LogWarning("TSD could not refresh trader stock: " + ex.Message);
            }
        }

        private void CheckProcurementPurchaseResponse()
        {
            try
            {
                if (!File.Exists(ProcurementPurchaseResponsePath))
                {
                    return;
                }

                string responseJson = File.ReadAllText(
                    ProcurementPurchaseResponsePath);

                File.Delete(ProcurementPurchaseResponsePath);

                string escapedJson = responseJson
                    .Replace("\\", "\\\\")
                    .Replace("'", "\\'")
                    .Replace("\r", "")
                    .Replace("\n", "\\n");

                if (overlay != null)
                {
                    overlay.ExecuteScript(
                        "window.tsd.onProcurementResponseReceived && " +
                        "window.tsd.onProcurementResponseReceived('" +
                        escapedJson + "');"
                    );
                }
            }
            catch (Exception exception)
            {
                Logger.LogError(
                    "TSD could not process the procurement purchase response: " +
                    exception);
            }
        }
        private void SaveOrderRequest(string payload)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(payload))
                {
                    Logger.LogWarning(
                        "TSD received an empty order request.");

                    return;
                }

                Directory.CreateDirectory(OrderFolderPath);

                if (File.Exists(OrderRequestPath))
                {
                    Logger.LogWarning(
                        "TSD already has an order request waiting for the Client.");

                    return;
                }

                File.WriteAllText(OrderRequestPath, payload);

                Logger.LogInfo(
                    "TSD saved an order request for the Client plugin.");

                if (overlay != null)
                {
                    overlay.ExecuteScript(
                        "window.tsd.onOrderStatusReceived && " +
                        "window.tsd.onOrderStatusReceived(" +
                        "'ORDER REQUEST QUEUED');"
                    );
                }
            }
            catch (Exception exception)
            {
                Logger.LogError(
                    "TSD could not save the order request: " + exception);

                if (overlay != null)
                {
                    overlay.ExecuteScript(
                        "window.tsd.onOrderStatusReceived && " +
                        "window.tsd.onOrderStatusReceived(" +
                        "'ORDER REQUEST FAILED');"
                    );
                }
            }
        }
        private void SaveProcurementPurchaseRequest(string payload)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(payload))
                {
                    Logger.LogWarning(
                        "TSD received an empty procurement purchase request.");

                    return;
                }

                Directory.CreateDirectory(OrderFolderPath);

                if (File.Exists(ProcurementPurchaseRequestPath))
                {
                    Logger.LogWarning(
                        "TSD already has a procurement request waiting for the Client.");

                    return;
                }

                File.WriteAllText(
                    ProcurementPurchaseRequestPath,
                    payload);

                Logger.LogInfo(
                    "TSD saved a procurement purchase request for the Client plugin.");

                if (overlay != null)
                {
                    overlay.ExecuteScript(
                        "window.tsd.onProcurementStatusReceived && " +
                        "window.tsd.onProcurementStatusReceived(" +
                        "'PURCHASE REQUEST QUEUED');"
                    );
                }
            }
            catch (Exception exception)
            {
                Logger.LogError(
                    "TSD could not save the procurement purchase request: " +
                    exception);

                if (overlay != null)
                {
                    overlay.ExecuteScript(
                        "window.tsd.onProcurementStatusReceived && " +
                        "window.tsd.onProcurementStatusReceived(" +
                        "'PURCHASE REQUEST FAILED');"
                    );
                }
            }
        }

        [Serializable]
        private class TabletAccessState
        {
            public bool droneUnavailable;
            public bool hasAccess;
            public bool inRaid;
            public string nickname;
            public string deliveryMethod;
            public string raidTime;
            public string updatedUtc;
        }

        [Serializable]
        private class TabletAccessMessage
        {
            public bool droneUnavailable;
            public bool hasAccess;
            public bool inRaid;
            public string nickname;
            public string deliveryMethod;
            public string raidTime;
        }

        [Serializable]
        private class ActiveCargoMessage
        {
            public int courierTotalFee;
            public int courierAdditionalFee;
            public int paidFee;
            public string deliveryMethod;
            public bool available;
            public string orderId;
            public int itemCount;
            public bool alreadyDispatched;
            public bool retryable;
            public bool inRaid;
        }

        [Serializable]
        private class CargoEnvelope
        {
            public string orderId;
            public CargoPayload cargo;
        }

        [Serializable]
        private class CargoPayload
        {
            public int courierTotalFee;
            public int courierAdditionalFee;
            public CargoOrder order;
            public string deliveryMethod;
            public CargoItem[] container;
        }

        [Serializable]
        private class CargoOrder { public int TotalPrice; }

        [Serializable]
        private class CargoItem
        {
            public string _id;
            public string _tpl;
        }

        [Serializable]
        private class DeliveryDispatchStatus
        {
            public string requestId;
            public string orderId;
            public string kind;
            public int sequence;
            public string message;
            public bool completed;
            public string dropPoint;
        }

        private void OnDestroy(){TabletRuntime.Unregister("tsd.logistics");overlay=null;}
    }
}
