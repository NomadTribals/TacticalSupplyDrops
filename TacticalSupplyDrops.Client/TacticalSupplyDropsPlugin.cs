using BepInEx;
using TsdTechnologies.Api;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using SPT.Common.Http;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace TacticalSupplyDrops.Client
{
    [Serializable]
    public class StashResponse
    {
        public bool success;
        public List<StashItem> items;
    }

    [Serializable]
    public class StashItem
    {
        public string Id;
        public string TemplateId;
        public string Name;
        public string ParentId;
        public string SlotId;
        public int StackCount;
        public int BaseValue;
    }

    [Serializable]
    public class OrderBridgeResponse
    {
        public string requestId;
        public bool transportSuccess;
        public string message;
        public string serverResponse;
    }
    public class ProcurementPurchaseBridgeResponse
    {
        public string requestId;
        public bool transportSuccess;
        public string message;
        public string serverResponse;
    }

    [BepInPlugin(
    "com.nomadtribals.tacticalsupplydrops",
    "TSD Logistics",
    "3.0.0")]
    [BepInDependency(TabletRuntime.PluginGuid, "1.0.0")]
    public class TacticalSupplyDropsPlugin : BaseUnityPlugin
    {

        private string _statusMessage = "Ready to connect.";

        private List<StashItem> _stashItems =
            new List<StashItem>();

        private bool _orderInProgress;
        private bool _procurementPurchaseInProgress;
        private bool _procurementCatalogInProgress;
        private bool _stashRefreshInProgress;
        private bool _orderLookupInProgress;
        private bool _deliveryDispatchInProgress;
        private TsdGroundDelivery groundDelivery;
        private float _tabletAccessRefreshTimer;
        private readonly HashSet<string> _dispatchedOrderIds =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static readonly string OrderFolderPath =
            Path.Combine(BepInEx.Paths.ConfigPath, "TacticalSupplyDrops");

        private const string FieldTerminalTemplateId =
            TabletRuntime.TerminalTemplateId;

        private const string ActiveOrderCargoId = "TSD_ACTIVE_ORDER";

        private string TabletAccessPath =>
            Path.Combine(OrderFolderPath, "tablet-access.json");

        private string DeliveryDispatchRequestPath =>
            Path.Combine(OrderFolderPath, "delivery-dispatch-request.json");

        private string DeliveryDispatchStatusPath =>
            Path.Combine(OrderFolderPath, "delivery-dispatch-status.json");

        private string DispatchedOrdersPath =>
            Path.Combine(OrderFolderPath, "dispatched-orders.json");

        private string CargoFilePath =>
            Path.Combine(OrderFolderPath, "cargo.json");

        private string OrderRequestPath =>
            Path.Combine(OrderFolderPath, "order-request.json");

        private string OrderResponsePath =>
            Path.Combine(OrderFolderPath, "order-response.json");

        private string StashRefreshRequestPath =>
            Path.Combine(
                OrderFolderPath,
                "stash-refresh-request.json");

        private string OrderLookupRequestPath =>
            Path.Combine(
                OrderFolderPath,
                "order-lookup-request.json");

        private string OrderLookupResponsePath =>
            Path.Combine(
                OrderFolderPath,
                "order-lookup-response.json");
        private string ProcurementPurchaseRequestPath =>
    Path.Combine(
        OrderFolderPath,
        "procurement-purchase-request.json");

        private string ProcurementPurchaseResponsePath =>
            Path.Combine(
                OrderFolderPath,
                "procurement-purchase-response.json");

        private async void Start()
        {
            Logger.LogInfo(
                "Tactical Supply Drops client plugin loaded!");

            AirdropDevelopmentConfig.Initialize(Config);
            groundDelivery = new TsdGroundDelivery(Config, Logger);

            Logger.LogInfo(
                "[TSD] Config file path: " + Config.ConfigFilePath);

            Logger.LogInfo(
                "[TSD] Airdrop release configuration initialized.");

            FlareDetection.Initialize(Logger);
            AirdropDustRecolor.Initialize(Logger);
            LoadDispatchedOrders();
            UpdateTabletAccessState();

            new HarmonyLib.Harmony(
                "com.nomadtribals.tacticalsupplydrops")
                .PatchAll(typeof(FlareDetection).Assembly);

            await RequestStashData();
        }

        private async Task RequestStashData()
        {
            _statusMessage = "Connecting to stash...";

            try
            {
                string response = await RequestHandler.GetJsonAsync(
                    "/client/tactical-supply-drops/get-stash");

                StashResponse stashResponse =
                    JsonUtility.FromJson<StashResponse>(response);

                if (stashResponse == null || !stashResponse.success)
                {
                    _statusMessage = "Could not read stash data.";

                    Logger.LogError(
                        "Tactical Supply Drops: invalid stash response.");

                    return;
                }

                _stashItems = stashResponse.items ??
                    new List<StashItem>();

                SaveStashResponse(response);

                try
                {
                    File.WriteAllText(
                        Path.Combine(
                            OrderFolderPath,
                            "stash-refresh-complete.json"),
                        DateTime.UtcNow.ToString("o"));

                    Logger.LogInfo(
                        "Tactical Supply Drops notified the Overlay that fresh stash data is ready.");
                }
                catch (Exception ex)
                {
                    Logger.LogError(
                        "Tactical Supply Drops could not notify the Overlay of the stash refresh: " +
                        ex);
                }

                _statusMessage =
                    "Stash data received successfully!";

                Logger.LogInfo(
                    "Tactical Supply Drops received " +
                    _stashItems.Count + " stash items.");
            }
            catch (Exception ex)
            {
                Logger.LogError(
                    "Tactical Supply Drops: stash request failed: " + ex);

                _statusMessage = "Could not retrieve stash data.";
            }
        }

        private void SaveStashResponse(string response)
        {
            try
            {
                Directory.CreateDirectory(OrderFolderPath);

                string filePath = Path.Combine(
                    OrderFolderPath,
                    "stash.json");
                Logger.LogInfo("TSD STASH FILE PATH: " + filePath);
                File.WriteAllText(
    filePath,
    Newtonsoft.Json.Linq.JToken.Parse(response).ToString(Newtonsoft.Json.Formatting.Indented)
);

                Logger.LogInfo(
                    "Tactical Supply Drops saved stash data to: " +
                    filePath);
            }
            catch (Exception ex)
            {
                Logger.LogError(
                    "Tactical Supply Drops could not save stash data: " + ex);
            }
        }

        private void Update()
        {
            groundDelivery?.Update();

            if (ProcurementProfileRefresh.Initialize(out string error))
            {
                ProcurementProfileRefresh.ApplyPending(
                    message => Logger.LogInfo(message),
                    message => Logger.LogWarning(message));
            }
            else if (!string.IsNullOrEmpty(error))
            {
                Logger.LogError(
                    "TSD profile refresh initialization failed: " + error);
            }

            CheckForOrderRequest();
            CheckForProcurementPurchaseRequest();
            CheckForProcurementCatalogRequest();
            CheckForOrderLookupRequest();
            CheckForStashRefreshRequest();
            CheckForDeliveryDispatchRequest();

            _tabletAccessRefreshTimer += Time.unscaledDeltaTime;
            if (_tabletAccessRefreshTimer >= 1f)
            {
                _tabletAccessRefreshTimer = 0f;
                UpdateTabletAccessState();
            }
        }
        private bool TryGetRaidPlayer(
            out ClientGameWorld gameWorld,
            out Player player)
        {
            gameWorld = null;
            player = null;

            try
            {
                if (!Singleton<GameWorld>.Instantiated)
                {
                    return false;
                }

                // Tarkov registers the active world under GameWorld, not its derived type.
                gameWorld = Singleton<GameWorld>.Instance as ClientGameWorld;
                player = gameWorld?.MainPlayer;
                return gameWorld != null && player != null && player.IsYourPlayer;
            }
            catch (Exception ex)
            {
                Logger.LogDebug("[TSD] Raid player is not ready: " + ex.Message);
                gameWorld = null;
                player = null;
                return false;
            }
        }

        private bool HasFieldTerminal(Player player)
        {
            if (player?.Inventory?.AllRealPlayerItems == null)
            {
                return false;
            }

            foreach (Item item in player.Inventory.AllRealPlayerItems)
            {
                if (item != null && string.Equals(
                    item.StringTemplateId,
                    FieldTerminalTemplateId,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private void UpdateTabletAccessState()
        {
            try
            {
                bool inRaid = false;
                bool hasTerminal = false;
                string nickname = null;
                string deliveryMethod = null;
                string raidTime = null;

                try
                {
                    var device=TabletRuntime.Context;
                    inRaid=device.InRaid;hasTerminal=device.IsFresh&&device.HasTablet;
                    nickname=device.Nickname;raidTime=device.RaidTime;
                    TryGetRaidPlayer(out ClientGameWorld currentWorld,out Player player);
                    if (inRaid) deliveryMethod = groundDelivery != null && groundDelivery.CanServe(currentWorld) ? "courier" : "aircraft";
                }
                catch (Exception ex)
                {
                    Logger.LogDebug(
                        "[TSD] Could not read field terminal ownership: " +
                        ex.Message);
                }

                Directory.CreateDirectory(OrderFolderPath);
                string temporaryPath = TabletAccessPath + ".tmp";
                File.WriteAllText(
                    temporaryPath,
                    JsonUtility.ToJson(new TabletAccessState
                    {
                        hasAccess = hasTerminal,
                        inRaid = inRaid,
                        nickname = nickname,
                        deliveryMethod = deliveryMethod,
                        raidTime = raidTime,
                        updatedUtc = DateTime.UtcNow.ToString("o")
                    }));

                if (File.Exists(TabletAccessPath))
                {
                    File.Delete(TabletAccessPath);
                }

                File.Move(temporaryPath, TabletAccessPath);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(
                    "[TSD] Could not refresh field terminal access state: " +
                    ex.Message);
            }
        }

        private void CheckForDeliveryDispatchRequest()
        {
            if (_deliveryDispatchInProgress ||
                !File.Exists(DeliveryDispatchRequestPath))
            {
                return;
            }

            try
            {
                string requestJson =
                    File.ReadAllText(DeliveryDispatchRequestPath);
                File.Delete(DeliveryDispatchRequestPath);

                DeliveryDispatchRequest request =
                    JsonUtility.FromJson<DeliveryDispatchRequest>(requestJson);

                if (request == null ||
                    string.IsNullOrWhiteSpace(request.RequestId) ||
                    string.IsNullOrWhiteSpace(request.OrderId))
                {
                    WriteDeliveryStatus(new DeliveryDispatchStatus
                    {
                        kind = "error",
                        message = "INVALID DELIVERY REQUEST",
                        completed = true
                    });
                    return;
                }

                StartCoroutine(ProcessDeliveryDispatch(request));
            }
            catch (Exception ex)
            {
                Logger.LogError(
                    "[TSD] Could not read the delivery dispatch request: " + ex);
            }
        }

        private IEnumerator ProcessDeliveryDispatch(
            DeliveryDispatchRequest request)
        {
            _deliveryDispatchInProgress = true;
            try
            {
                if (!TryGetRaidPlayer(out ClientGameWorld gameWorld, out Player player))
                {
                    WriteDeliveryStatus(new DeliveryDispatchStatus
                    {
                        requestId = request.RequestId,
                        orderId = request.OrderId,
                        kind = "error",
                        message = "ENTER RAID BEFORE DISPATCHING CARGO",
                        completed = true
                    });
                    yield break;
                }

                if (!HasFieldTerminal(player))
                {
                    WriteDeliveryStatus(new DeliveryDispatchStatus
                    {
                        requestId = request.RequestId,
                        orderId = request.OrderId,
                        kind = "error",
                        message = "TSD FIELD TERMINAL NOT DETECTED",
                        completed = true
                    });
                    yield break;
                }

                if (!File.Exists(CargoFilePath))
                {
                    WriteDeliveryStatus(new DeliveryDispatchStatus
                    {
                        requestId = request.RequestId,
                        orderId = request.OrderId,
                        kind = "error",
                        message = "NO VERIFIED CARGO ORDER IS READY",
                        completed = true
                    });
                    yield break;
                }

                string manifestSnapshot = File.ReadAllText(CargoFilePath);
                CargoEnvelope cargoEnvelope = JsonUtility.FromJson<CargoEnvelope>(manifestSnapshot);
                if (cargoEnvelope == null ||
                    cargoEnvelope.cargo == null ||
                    cargoEnvelope.cargo.container == null ||
                    cargoEnvelope.cargo.container.Length == 0 ||
                    !string.Equals(
                        cargoEnvelope.orderId,
                        request.OrderId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    WriteDeliveryStatus(new DeliveryDispatchStatus
                    {
                        requestId = request.RequestId,
                        orderId = request.OrderId,
                        kind = "error",
                        message = "CARGO MANIFEST COULD NOT BE VERIFIED",
                        completed = true
                    });
                    yield break;
                }

                if (_dispatchedOrderIds.Contains(request.OrderId))
                {
                    WriteDeliveryStatus(new DeliveryDispatchStatus
                    {
                        requestId = request.RequestId,
                        orderId = request.OrderId,
                        kind = "success",
                        message = "THIS ORDER HAS ALREADY BEEN DISPATCHED",
                        completed = true
                    });
                    yield break;
                }

                var dispatchWorld = gameWorld;
                bool groundRoute = groundDelivery != null && groundDelivery.CanServe(gameWorld);
                Logger.LogInfo("[TSD] Delivery route for " + gameWorld.LocationId + ": " + (groundRoute ? "ground courier" : "aircraft"));
                string[] stages = groundRoute ? new[]
                {
                    "AUTHENTICATING FIELD TERMINAL",
                    "READING SECURE CARGO MANIFEST",
                    "TRIANGULATING TERMINAL POSITION",
                    "LOCAL COURIER RELAY ACKNOWLEDGED",
                    "ENCRYPTING GROUND HANDOFF PACKET"
                } : new[]
                {
                    "AUTHENTICATING FIELD TERMINAL",
                    "READING SECURE CARGO MANIFEST",
                    "TRIANGULATING GPS POSITION",
                    "CALCULATING NEAREST SAFE DROP CORRIDOR",
                    "ENCRYPTING DISPATCH PACKET"
                };

                for (int index = 0; index < stages.Length; index++)
                {
                    if (!TryGetRaidPlayer(out gameWorld, out player) ||
                        !HasFieldTerminal(player))
                    {
                        WriteDeliveryStatus(new DeliveryDispatchStatus
                        {
                            requestId = request.RequestId,
                            orderId = request.OrderId,
                            kind = "error",
                            sequence = index + 1,
                            message = "FIELD TERMINAL LINK LOST",
                            completed = true
                        });
                        yield break;
                    }

                    WriteDeliveryStatus(new DeliveryDispatchStatus
                    {
                        requestId = request.RequestId,
                        orderId = request.OrderId,
                        kind = "stage",
                        sequence = index + 1,
                        message = stages[index]
                    });

                    yield return new WaitForSecondsRealtime(.62f);
                }

                if (!TryGetRaidPlayer(out gameWorld, out player) ||
                    !HasFieldTerminal(player))
                {
                    WriteDeliveryStatus(new DeliveryDispatchStatus
                    {
                        requestId = request.RequestId,
                        orderId = request.OrderId,
                        kind = "error",
                        sequence = stages.Length + 1,
                        message = "FIELD TERMINAL LINK LOST",
                        completed = true
                    });
                    yield break;
                }

                if (groundRoute)
                {
                    groundDelivery.SetStatus(dispatchWorld, request.OrderId, "pending", "Courier dispatched. A secure handoff location is being arranged.");
                    WriteDeliveryStatus(new DeliveryDispatchStatus
                    {
                        requestId = request.RequestId, orderId = request.OrderId, kind = "stage",
                        sequence = stages.Length + 1, message = "GROUND COURIER DISPATCHED — AWAIT HANDOFF"
                    });
                    float handoffTime = Time.unscaledTime + groundDelivery.DelaySeconds;
                    while (Time.unscaledTime < handoffTime)
                    {
                        if (!TryGetRaidPlayer(out gameWorld, out player) || gameWorld != dispatchWorld || !HasFieldTerminal(player))
                        {
                            GroundDispatchFailed(request, dispatchWorld, stages.Length + 2, "FIELD TERMINAL LINK LOST — ORDER STILL READY");
                            yield break;
                        }
                        yield return new WaitForSecondsRealtime(.25f);
                    }
                    bool requestOpen = true;
                    Func<bool> access = () => requestOpen && TryGetRaidPlayer(out ClientGameWorld currentWorld, out Player currentPlayer)
                        && currentWorld == dispatchWorld && HasFieldTerminal(currentPlayer);
                    Task<Vector3> placement = groundDelivery.Deliver(dispatchWorld, request.OrderId, manifestSnapshot, access);
                    try
                    {
                        while (!placement.IsCompleted)
                        {
                            if (!access())
                            {
                                GroundDispatchFailed(request, dispatchWorld, stages.Length + 2, "FIELD TERMINAL LINK LOST — ORDER STILL READY");
                                placement.ContinueWith(faulted => { var observed = faulted.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
                                yield break;
                            }
                            yield return null;
                        }
                        if (placement.IsCanceled)
                        {
                            GroundDispatchFailed(request, dispatchWorld, stages.Length + 2, "COURIER CANCELLED — ORDER STILL READY");
                            yield break;
                        }
                        if (placement.IsFaulted)
                        {
                            var error = placement.Exception.GetBaseException();
                            Logger.LogWarning("[TSD] Ground dispatch was not completed: " + error);
                            string reason = error is InvalidOperationException ? error.Message : "Ground delivery could not be completed. Your order remains ready to retry.";
                            GroundDispatchFailed(request, dispatchWorld, stages.Length + 2, reason);
                            yield break;
                        }
                        Vector3 groundPoint = placement.Result;
                        _dispatchedOrderIds.Add(request.OrderId);
                        SaveDispatchedOrders();
                        WriteDeliveryStatus(new DeliveryDispatchStatus
                        {
                            requestId = request.RequestId, orderId = request.OrderId, kind = "success",
                            sequence = stages.Length + 2, message = "COURIER CARGO READY FOR COLLECTION",
                            completed = true, dropPoint = groundPoint.ToString()
                        });
                        yield break;
                    }
                    finally { requestOpen = false; }
                }

                AirdropDustRecolor.BeginTsdDropPointCapture();
                gameWorld.InitAirdrop(
                    ActiveOrderCargoId,
                    true,
                    player.Position);

                if (!AirdropDustRecolor.EndTsdDropPointCapture(
                    out Vector3 selectedDropPoint))
                {
                    WriteDeliveryStatus(new DeliveryDispatchStatus
                    {
                        requestId = request.RequestId,
                        orderId = request.OrderId,
                        kind = "error",
                        sequence = stages.Length + 1,
                        message = "NO SAFE AIRDROP POINT AVAILABLE HERE",
                        completed = true
                    });
                    yield break;
                }

                _dispatchedOrderIds.Add(request.OrderId);
                SaveDispatchedOrders();

                Logger.LogInfo(
                    "[TSD] Dispatched cargo order " + request.OrderId +
                    " from player position " + player.Position +
                    " to nearby airdrop point " + selectedDropPoint + ".");

                WriteDeliveryStatus(new DeliveryDispatchStatus
                {
                    requestId = request.RequestId,
                    orderId = request.OrderId,
                    kind = "success",
                    sequence = stages.Length + 1,
                    message = "CARGO ON THE WAY",
                    completed = true,
                    dropPoint = selectedDropPoint.ToString()
                });
            }
            finally
            {
                _deliveryDispatchInProgress = false;
            }
        }

        private void GroundDispatchFailed(DeliveryDispatchRequest request, ClientGameWorld world, int sequence, string reason)
        {
            groundDelivery.SetStatus(world, request.OrderId, "unavailable", reason);
            WriteDeliveryStatus(new DeliveryDispatchStatus
            {
                requestId = request.RequestId, orderId = request.OrderId, kind = "error",
                sequence = sequence, message = reason, completed = true
            });
        }

        private void OnApplicationQuit() { groundDelivery?.Shutdown(); }

        private void WriteDeliveryStatus(DeliveryDispatchStatus status)
        {
            try
            {
                Directory.CreateDirectory(OrderFolderPath);
                string temporaryPath = DeliveryDispatchStatusPath + ".tmp";
                File.WriteAllText(temporaryPath, JsonUtility.ToJson(status));
                if (File.Exists(DeliveryDispatchStatusPath))
                {
                    File.Delete(DeliveryDispatchStatusPath);
                }
                File.Move(temporaryPath, DeliveryDispatchStatusPath);
            }
            catch (Exception ex)
            {
                Logger.LogError("[TSD] Could not save delivery status: " + ex);
            }
        }

        private void LoadDispatchedOrders()
        {
            try
            {
                if (!File.Exists(DispatchedOrdersPath)) return;
                string[] ids = JsonUtility.FromJson<DispatchedOrdersEnvelope>(
                    File.ReadAllText(DispatchedOrdersPath))?.orderIds;
                if (ids == null) return;
                foreach (string id in ids)
                {
                    if (!string.IsNullOrWhiteSpace(id))
                        _dispatchedOrderIds.Add(id);
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning("[TSD] Could not load dispatched order history: " + ex.Message);
            }
        }

        private void SaveDispatchedOrders()
        {
            try
            {
                Directory.CreateDirectory(OrderFolderPath);
                File.WriteAllText(
                    DispatchedOrdersPath,
                    JsonUtility.ToJson(new DispatchedOrdersEnvelope
                    {
                        orderIds = new List<string>(_dispatchedOrderIds).ToArray()
                    }));
            }
            catch (Exception ex)
            {
                Logger.LogError("[TSD] Could not save dispatched order history: " + ex);
            }
        }

        private void CheckForOrderLookupRequest()
        {
            if (_orderLookupInProgress)
            {
                return;
            }

            if (!File.Exists(OrderLookupRequestPath))
            {
                return;
            }

            try
            {
                string requestJson =
                    File.ReadAllText(OrderLookupRequestPath);

                if (string.IsNullOrWhiteSpace(requestJson))
                {
                    File.Delete(OrderLookupRequestPath);
                    return;
                }

                // Delete the signal before starting the lookup
                // to prevent repeated requests.
                File.Delete(OrderLookupRequestPath);

                Logger.LogInfo(
                    "Tactical Supply Drops detected an order lookup request.");

                _ = ProcessOrderLookupRequestAsync(requestJson);
            }
            catch (Exception ex)
            {
                Logger.LogError(
                    "Tactical Supply Drops could not read the order lookup request: " +
                    ex);
            }
        }

        private void CheckForOrderRequest()
        {
            if (_orderInProgress)
            {
                return;
            }

            if (!File.Exists(OrderRequestPath))
            {
                return;
            }

            Logger.LogInfo(
                "Tactical Supply Drops detected an order request file.");

            try
            {
                string requestJson =
                    File.ReadAllText(OrderRequestPath);

                if (string.IsNullOrWhiteSpace(requestJson))
                {
                    return;
                }

                // Remove the request before sending it so it cannot
                // accidentally be submitted repeatedly.
                File.Delete(OrderRequestPath);

                _ = ProcessOrderRequestAsync(requestJson);
            }
            catch (Exception ex)
            {
                Logger.LogError(
                    "Tactical Supply Drops could not read the order request: " +
                    ex);
            }
        }
        private void CheckForProcurementCatalogRequest()
        {
            string path = Path.Combine(OrderFolderPath, "procurement-catalog-request.json");
            if (_procurementCatalogInProgress || !File.Exists(path)) return;
            try
            {
                string json = File.ReadAllText(path);
                File.Delete(path);
                _ = RefreshProcurementCatalogAsync(json);
            }
            catch (Exception ex) { Logger.LogWarning("TSD catalog request could not be read: " + ex.Message); }
        }

        private async Task RefreshProcurementCatalogAsync(string request)
        {
            _procurementCatalogInProgress = true;
            string response;
            try
            {
                response = await RequestHandler.PostJsonAsync("/client/tactical-supply-drops/trader-catalog", request);
                Newtonsoft.Json.Linq.JObject.Parse(response);
            }
            catch (Exception ex)
            {
                Logger.LogWarning("TSD trader catalog failed: " + ex.Message);
                response = "{\"success\":false,\"message\":\"Trader stock is unavailable. Try refresh.\"}";
            }
            try
            {
                Directory.CreateDirectory(OrderFolderPath);
                string path = Path.Combine(OrderFolderPath, "procurement-catalog-response.json");
                File.WriteAllText(path + ".tmp", response);
                if (File.Exists(path)) File.Delete(path);
                File.Move(path + ".tmp", path);
            }
            catch (Exception ex) { Logger.LogWarning("TSD catalog response could not be saved: " + ex.Message); }
            finally { _procurementCatalogInProgress = false; }
        }

        private void CheckForProcurementPurchaseRequest()
        {
            if (_procurementPurchaseInProgress)
            {
                return;
            }

            if (!File.Exists(ProcurementPurchaseRequestPath))
            {
                return;
            }

            Logger.LogInfo(
                "Tactical Supply Drops detected a procurement purchase request.");

            try
            {
                string requestJson =
                    File.ReadAllText(ProcurementPurchaseRequestPath);

                if (string.IsNullOrWhiteSpace(requestJson))
                {
                    return;
                }

                // Remove the request before sending it so it cannot
                // accidentally be submitted repeatedly.
                File.Delete(ProcurementPurchaseRequestPath);

                _ = ProcessProcurementPurchaseRequestAsync(requestJson);
            }
            catch (Exception ex)
            {
                Logger.LogError(
                    "Tactical Supply Drops could not read the procurement purchase request: " +
                    ex);
            }
        }
        private void CheckForStashRefreshRequest()
        {
            if (_stashRefreshInProgress)
            {
                return;
            }

            if (!File.Exists(StashRefreshRequestPath))
            {
                return;
            }

            try
            {
                // Delete the signal before starting the refresh so it
                // cannot trigger repeatedly on subsequent Update calls.
                File.Delete(StashRefreshRequestPath);

                Logger.LogInfo(
                    "Tactical Supply Drops detected a stash refresh request.");

                _ = ProcessStashRefreshRequestAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError(
                    "Tactical Supply Drops could not process the stash " +
                    "refresh request: " + ex);
            }
        }
        private async Task ProcessOrderLookupRequestAsync(string requestJson)
        {
            if (_orderLookupInProgress)
            {
                return;
            }

            _orderLookupInProgress = true;

            try
            {
                Logger.LogInfo(
                    "Tactical Supply Drops is retrieving the accepted order.");

                string serverResponse =
                    await SupplyDropOrderBridge.LookupOrderAsync(requestJson);

                Directory.CreateDirectory(OrderFolderPath);

                File.WriteAllText(
                    OrderLookupResponsePath,
                    serverResponse);

                Logger.LogInfo(
                    "Tactical Supply Drops saved the order lookup response to: " +
                    OrderLookupResponsePath);
            }
            catch (Exception ex)
            {
                Logger.LogError(
                    "Tactical Supply Drops order lookup failed: " + ex);
            }
            finally
            {
                _orderLookupInProgress = false;
            }
        }

        private async Task ProcessStashRefreshRequestAsync()
        {
            if (_stashRefreshInProgress)
            {
                return;
            }

            _stashRefreshInProgress = true;

            try
            {
                Logger.LogInfo(
                    "Tactical Supply Drops is refreshing stash data.");

                await RequestStashData();

                Logger.LogInfo(
                    "Tactical Supply Drops stash refresh request completed.");
            }
            catch (Exception ex)
            {
                Logger.LogError(
                    "Tactical Supply Drops stash refresh failed: " + ex);
            }
            finally
            {
                _stashRefreshInProgress = false;
            }
        }

        private async Task ProcessOrderRequestAsync(string requestJson)
        {
            _orderInProgress = true;
            Logger.LogInfo(
    "TSD DEBUG: ProcessOrderRequestAsync has started.");

            string requestId = "UNKNOWN";

            try
            {
                OrderRequestEnvelope envelope =
                    JsonUtility.FromJson<OrderRequestEnvelope>(requestJson);

                if (envelope != null &&
                    envelope.data != null &&
                    envelope.data.Length > 0 &&
                    !string.IsNullOrWhiteSpace(envelope.data[0].RequestId))
                {
                    requestId = envelope.data[0].RequestId;
                }

                Logger.LogInfo(
                    "Tactical Supply Drops submitting order request: " +
                    requestId);

                string serverResponse =
     await SupplyDropOrderBridge.SubmitOrderAsync(requestJson);

                Logger.LogInfo(
    "TSD DEBUG: About to queue cargo order response.");

                ProcurementProfileRefresh.QueueResponse(serverResponse);

                Logger.LogInfo(
                    "TSD DEBUG: Cargo order response queued successfully.");

                Logger.LogInfo(
                    "Tactical Supply Drops raw server response: " +
                    serverResponse);

                WriteOrderResponse(new OrderBridgeResponse
                {
                    requestId = requestId,
                    transportSuccess = true,
                    message = "Server response received.",
                    serverResponse = serverResponse
                });

                Logger.LogInfo(
                    "Tactical Supply Drops received a Server response for: " +
                    requestId);
            }
            catch (Exception ex)
            {
                Logger.LogError(
                    "Tactical Supply Drops order submission failed: " + ex);

                WriteOrderResponse(new OrderBridgeResponse
                {
                    requestId = requestId,
                    transportSuccess = false,
                    message = ex.Message,
                    serverResponse = ""
                });
            }
            finally
            {
                _orderInProgress = false;
            }
        }
        private async Task ProcessProcurementPurchaseRequestAsync(
    string requestJson)
        {
            _procurementPurchaseInProgress = true;

            string requestId = "UNKNOWN";

            try
            {
                ProcurementPurchaseRequestEnvelope envelope =
                    JsonUtility.FromJson<ProcurementPurchaseRequestEnvelope>(
                        requestJson);

                if (envelope != null &&
     envelope.data != null &&
     envelope.data.Length > 0 &&
     !string.IsNullOrWhiteSpace(envelope.data[0].RequestId))
                {
                    requestId = envelope.data[0].RequestId;
                }

                Logger.LogInfo(
                    "Tactical Supply Drops submitting procurement purchase: " +
                    requestId);

                string serverResponse =
                    await SupplyDropOrderBridge.PurchaseCartridgesAsync(
                        requestJson);
                ProcurementProfileRefresh.QueueResponse(serverResponse);

                Logger.LogInfo(
                    "TSD queued the procurement response for EFT profile refresh.");

                Logger.LogInfo(
                    "Tactical Supply Drops procurement server response: " +
                    serverResponse);

                WriteProcurementPurchaseResponse(
                    new ProcurementPurchaseBridgeResponse
                    {
                        requestId = requestId,
                        transportSuccess = true,
                        message = "Server response received.",
                        serverResponse = serverResponse
                    });

                Logger.LogInfo(
                    "Tactical Supply Drops received a procurement response for: " +
                    requestId);
            }
            catch (Exception ex)
            {
                Logger.LogError(
                    "Tactical Supply Drops procurement submission failed: " + ex);

                WriteProcurementPurchaseResponse(
                    new ProcurementPurchaseBridgeResponse
                    {
                        requestId = requestId,
                        transportSuccess = false,
                        message = ex.Message,
                        serverResponse = ""
                    });
            }
            finally
            {
                _procurementPurchaseInProgress = false;
            }
        }

        private void WriteOrderResponse(OrderBridgeResponse response)
        {
            try
            {
                Directory.CreateDirectory(OrderFolderPath);

                string temporaryPath = OrderResponsePath + ".tmp";

                string json = JsonUtility.ToJson(response);

                File.WriteAllText(temporaryPath, json);

                if (File.Exists(OrderResponsePath))
                {
                    File.Delete(OrderResponsePath);
                }

                File.Move(temporaryPath, OrderResponsePath);

                Logger.LogInfo(
                    "Tactical Supply Drops saved order response.");
            }
            catch (Exception ex)
            {
                Logger.LogError(
                    "Tactical Supply Drops could not save the order response: " +
                    ex);
            }
        }
        private void WriteProcurementPurchaseResponse(
    ProcurementPurchaseBridgeResponse response)
        {
            try
            {
                Directory.CreateDirectory(OrderFolderPath);

                string temporaryPath =
                    ProcurementPurchaseResponsePath + ".tmp";

                string json = JsonUtility.ToJson(response);

                File.WriteAllText(temporaryPath, json);

                if (File.Exists(ProcurementPurchaseResponsePath))
                {
                    File.Delete(ProcurementPurchaseResponsePath);
                }

                File.Move(
                    temporaryPath,
                    ProcurementPurchaseResponsePath);

                Logger.LogInfo(
                    "Tactical Supply Drops saved procurement purchase response.");
            }
            catch (Exception ex)
            {
                Logger.LogError(
                    "Tactical Supply Drops could not save the procurement response: " +
                    ex);
            }
        }

        [Serializable]
        private class OrderRequestEnvelope
        {
            public OrderRequestInfo[] data;
        }
        private class ProcurementPurchaseRequestEnvelope
        {
            public ProcurementPurchaseRequestData[] data;
        }

        private class ProcurementPurchaseRequestData
        {
            public string Action;
            public string RequestId;
            public int Quantity;
        }

        [Serializable]
        private class TabletAccessState
        {
            public bool hasAccess;
            public bool inRaid;
            public string nickname;
            public string deliveryMethod;
            public string raidTime;
            public string updatedUtc;
        }

        [Serializable]
        private class DeliveryDispatchRequest
        {
            public string RequestId;
            public string OrderId;
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

        [Serializable]
        private class CargoEnvelope
        {
            public string orderId;
            public CargoPayload cargo;
        }

        [Serializable]
        private class CargoPayload
        {
            public CargoItem[] container;
        }

        [Serializable]
        private class CargoItem
        {
            public string _id;
            public string _tpl;
        }

        [Serializable]
        private class DispatchedOrdersEnvelope
        {
            public string[] orderIds;
        }

        [Serializable]
        private class OrderRequestInfo
        {
            public string RequestId;
        }
    }
}
