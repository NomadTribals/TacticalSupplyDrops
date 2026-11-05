using BepInEx;
using SPT.Common.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    "nomadtribals-TacticalSupplyDrops",
    "1.0.0")]
    public class TacticalSupplyDropsPlugin : BaseUnityPlugin
    {
        private bool _windowOpen = false;

        private Rect _windowRect =
            new Rect(100, 100, 900, 650);

        private string _statusMessage = "Ready to connect.";
        private string _searchText = "";

        private List<StashItem> _stashItems =
            new List<StashItem>();

        private HashSet<string> _selectedItemIds =
            new HashSet<string>();

        private Vector2 _scrollPosition = Vector2.zero;

        private GUIStyle _titleStyle;
        private GUIStyle _itemNameStyle;
        private GUIStyle _itemDetailsStyle;
        private GUIStyle _sectionStyle;

        private bool _orderInProgress;
        private bool _procurementPurchaseInProgress;
        private bool _stashRefreshInProgress;
        private bool _orderLookupInProgress;

        private const string OrderFolderPath =
            @"F:\Games\SPT\BepInEx\config\TacticalSupplyDrops";

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

            Logger.LogInfo(
                "[TSD] Config file path: " + Config.ConfigFilePath);

            Logger.LogInfo(
                "[TSD] Airdrop release configuration initialized.");

            FlareDetection.Initialize(Logger);
            AirdropDustRecolor.Initialize(Logger);

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

                _selectedItemIds.Clear();

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
            if (Input.GetKeyDown(KeyCode.F10))
            {
                _windowOpen = !_windowOpen;
            }

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
            CheckForOrderLookupRequest();
            CheckForStashRefreshRequest();
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
        private class OrderRequestInfo
        {
            public string RequestId;
        }
        private void OnGUI()
        {
            if (!_windowOpen)
            {
                return;
            }

            CreateStyles();

            _windowRect = GUILayout.Window(
                123456,
                _windowRect,
                DrawSupplyDropWindow,
                "TACTICAL SUPPLY DROPS");

            Event currentEvent = Event.current;

            if (currentEvent != null &&
                _windowRect.Contains(currentEvent.mousePosition))
            {
                if (currentEvent.type == EventType.MouseDown ||
                    currentEvent.type == EventType.MouseUp ||
                    currentEvent.type == EventType.MouseDrag ||
                    currentEvent.type == EventType.ScrollWheel)
                {
                    currentEvent.Use();
                }
            }
        }

        private void CreateStyles()
        {
            if (_titleStyle != null)
            {
                return;
            }

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            _sectionStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold
            };

            _itemNameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold
            };

            _itemDetailsStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14
            };
        }

        private void DrawSupplyDropWindow(int windowId)
        {
            GUILayout.Space(10);

            GUILayout.Label(
                "TACTICAL SUPPLY DROPS",
                _titleStyle);

            GUILayout.Label(
                "Select items from your stash for delivery.",
                _itemDetailsStyle);

            GUILayout.Space(10);

            GUILayout.Label(
                "Connection: " + _statusMessage,
                _itemDetailsStyle);

            GUILayout.Space(15);

            GUILayout.BeginHorizontal();

            GUILayout.Label(
                "AVAILABLE STASH ITEMS (" +
                _stashItems.Count + ")",
                _sectionStyle);

            GUILayout.FlexibleSpace();

            GUILayout.Label(
                "SELECTED: " + _selectedItemIds.Count,
                _sectionStyle);

            GUILayout.EndHorizontal();

            GUILayout.Space(8);

            GUILayout.BeginHorizontal();

            GUILayout.Label(
                "SEARCH:",
                _itemDetailsStyle,
                GUILayout.Width(65));

            _searchText = GUILayout.TextField(
                _searchText ?? "",
                GUILayout.Height(30));

            if (GUILayout.Button(
                "Clear Search",
                GUILayout.Width(110),
                GUILayout.Height(30)))
            {
                _searchText = "";
            }

            GUILayout.EndHorizontal();

            List<StashItem> visibleItems = _stashItems
                .Where(item => item != null)
                .Where(item =>
                    string.IsNullOrWhiteSpace(_searchText) ||
                    (!string.IsNullOrEmpty(item.Name) &&
                     item.Name.IndexOf(
                         _searchText,
                         StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (!string.IsNullOrEmpty(item.TemplateId) &&
                     item.TemplateId.IndexOf(
                         _searchText,
                         StringComparison.OrdinalIgnoreCase) >= 0))
                .OrderBy(
                    item => item.Name ?? "",
                    StringComparer.OrdinalIgnoreCase)
                .ToList();

            GUILayout.Space(8);

            GUILayout.Label(
                "MATCHING ITEMS: " + visibleItems.Count,
                _itemDetailsStyle);

            _scrollPosition = GUILayout.BeginScrollView(
                _scrollPosition,
                GUILayout.Height(390));

            if (visibleItems.Count == 0)
            {
                GUILayout.Label(
                    "No matching items found.",
                    _itemDetailsStyle);
            }

            foreach (StashItem item in visibleItems)
            {
                GUILayout.BeginHorizontal("box");

                bool hasId = !string.IsNullOrEmpty(item.Id);

                bool selected = hasId &&
                    _selectedItemIds.Contains(item.Id);

                GUI.enabled = hasId;

                bool newSelected = GUILayout.Toggle(
                    selected,
                    "",
                    GUILayout.Width(30),
                    GUILayout.Height(45));

                GUI.enabled = true;

                if (hasId && newSelected != selected)
                {
                    if (newSelected)
                    {
                        _selectedItemIds.Add(item.Id);
                    }
                    else
                    {
                        _selectedItemIds.Remove(item.Id);
                    }
                }

                GUILayout.BeginVertical();

                string displayName =
                    string.IsNullOrEmpty(item.Name)
                        ? "(Item name not received)"
                        : item.Name;

                GUILayout.Label(
                    displayName,
                    _itemNameStyle);

                GUILayout.Label(
                    "Quantity: " + item.StackCount +
                    "     Base value: " + item.BaseValue +
                    " roubles",
                    _itemDetailsStyle);

                GUILayout.EndVertical();

                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();

            GUILayout.Space(10);

            GUILayout.BeginHorizontal();

            if (GUILayout.Button(
                "Refresh Stash Data",
                GUILayout.Height(45)))
            {
                _ = RequestStashData();
            }

            if (GUILayout.Button(
                "Clear Selection",
                GUILayout.Height(45)))
            {
                _selectedItemIds.Clear();
            }

            if (GUILayout.Button(
                "Close",
                GUILayout.Height(45)))
            {
                _windowOpen = false;
            }

            GUILayout.EndHorizontal();

            GUI.DragWindow(
                new Rect(0, 0, 10000, 30));
        }
    }
}