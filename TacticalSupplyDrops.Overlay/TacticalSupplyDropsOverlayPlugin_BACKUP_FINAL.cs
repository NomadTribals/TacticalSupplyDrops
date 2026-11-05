
using BepInEx;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using WebOverlay;

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

    [BepInPlugin(
        "com.tacticalsupplydrops.overlay",
        "Tactical Supply Drops Overlay",
        "0.2.0")]
    [BepInDependency("com.anvil.weboverlay", "1.11.0")]
    public class TacticalSupplyDropsOverlayPlugin : BaseUnityPlugin
    {
        private IWebOverlay overlay;

        private const string StashFilePath =
            @"F:\Games\SPT\BepInEx\config\TacticalSupplyDrops\stash.json";
        private const string StashRefreshCompletePath =
    @"F:\Games\SPT\BepInEx\config\TacticalSupplyDrops\stash-refresh-complete.json";
        private const string StashRefreshRequestPath =
    @"F:\Games\SPT\BepInEx\config\TacticalSupplyDrops\stash-refresh-request.json";
        private const string OrderLookupRequestPath =
    @"F:\Games\SPT\BepInEx\config\TacticalSupplyDrops\order-lookup-request.json";
        private const string OrderFolderPath =
    @"F:\Games\SPT\BepInEx\config\TacticalSupplyDrops";

        private const string OrderRequestPath =
            @"F:\Games\SPT\BepInEx\config\TacticalSupplyDrops\order-request.json";

        private const string OrderResponsePath =
            @"F:\Games\SPT\BepInEx\config\TacticalSupplyDrops\order-response.json";

        private const string ProcurementPurchaseRequestPath =
    @"F:\Games\SPT\BepInEx\config\TacticalSupplyDrops\procurement-purchase-request.json";

        private const string ProcurementPurchaseResponsePath =
            @"F:\Games\SPT\BepInEx\config\TacticalSupplyDrops\procurement-purchase-response.json";
        private void Awake()
        {
            Logger.LogInfo("Tactical Supply Drops Overlay loaded!");
        }

        private float responseCheckTimer = 0f;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F9))
            {
                ToggleOverlay();
            }

            responseCheckTimer += Time.deltaTime;

            if (responseCheckTimer >= 1f)
            {
                responseCheckTimer = 0f;
                CheckOrderResponse();
                CheckProcurementPurchaseResponse();
                CheckForStashRefreshComplete();
            }
        }

        private void ToggleOverlay()
        {
            if (overlay != null)
            {
                overlay.Toggle();
                SendStashData();
                return;
            }

            overlay = WebOverlays.Create(
                "Tactical Supply Drops",
                new OverlayOptions
                {
                    Width = 1200,
                    Height = 800,
                    FreeCursorWhileShown = true,
                    ClickThroughWhenUnfocused = true,
                    DispatchOnMainThread = true
                });

            if (overlay == null)
            {
                Logger.LogError(
                    "Could not create Tactical Supply Drops overlay.");
                return;
            }

            IWebOverlay created = overlay;

            // Receive messages sent from the F9 JavaScript interface.
            created.ChannelMessage += (channel, payload) =>
            {
                Logger.LogInfo(
                    "TSD DEBUG: Received F9 message channel: " + channel);

                if (channel == "tsd-test")
                {
                    Logger.LogInfo(
                        "TSD F9 communication test received: " + payload);

                    created.ExecuteScript(
                        "window.overlay.onResponseReceived && " +
                        "window.overlay.onResponseReceived(" +
                        "'C# response received successfully!');"
                    );
                }
                else if (channel == "tsd-submit-order")
                {
                    Logger.LogInfo(
                        "TSD F9 order submission request received.");

                    SaveOrderRequest(payload);
                }
                else if (channel == "tsd-purchase-cartridges")
                {
                    Logger.LogInfo(
                        "TSD F9 procurement purchase request received.");

                    SaveProcurementPurchaseRequest(payload);
                }
                else if (channel == "tsd-refresh-stash")
                {
                    try
                    {
                        Directory.CreateDirectory(
                            Path.GetDirectoryName(StashRefreshRequestPath));

                        File.WriteAllText(
                            StashRefreshRequestPath,
                            "refresh");

                        Logger.LogInfo(
                            "Tactical Supply Drops sent a stash refresh request to the Client plugin.");
                    }
                    catch (Exception exception)
                    {
                        Logger.LogError(
                            "Could not create the stash refresh request: " + exception);
                    }
                }
                else if (channel == "tsd-lookup-order")
                {
                    try
                    {
                        if (string.IsNullOrWhiteSpace(payload))
                        {
                            Logger.LogWarning(
                                "TSD received an empty order lookup request.");

                            return;
                        }

                        Directory.CreateDirectory(OrderFolderPath);

                        File.WriteAllText(
                            OrderLookupRequestPath,
                            payload);

                        Logger.LogInfo(
                            "TSD saved an order lookup request for the Client plugin.");
                    }
                    catch (Exception exception)
                    {
                        Logger.LogError(
                            "TSD could not save the order lookup request: " + exception);
                    }
                }
                else if (channel == "tsd-ready")
                {
                    Logger.LogInfo(
                        "TSD F9 page is ready. Sending stash data.");

                    SendStashData();
                }
            };

            created.ChannelsFailed += () =>
            {
                Logger.LogError(
                    "TSD DIAGNOSTIC: Anvil-WebOverlay channel system FAILED.");
            };

            Logger.LogInfo(
                "TSD DIAGNOSTIC: ChannelsAvailable = " +
                (created.ChannelsAvailable.HasValue
                    ? created.ChannelsAvailable.Value.ToString()
                    : "Not yet determined"));
            created.PageLoaded += () =>
            {
                Logger.LogInfo(
                    "TSD DIAGNOSTIC: F9 page loaded. ChannelsAvailable = " +
                    (created.ChannelsAvailable.HasValue
                        ? created.ChannelsAvailable.Value.ToString()
                        : "Not yet determined"));

            };
            created.Failed += () =>
            {
                Logger.LogError(
                    "Tactical Supply Drops overlay failed: " +
                    created.FailureMessage);

                created.Dispose();

                if (ReferenceEquals(overlay, created))
                {
                    overlay = null;
                }
            };

            created.LoadHtml(@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>

    <style>
        * {
            box-sizing: border-box;
        }

        :root {
            --background: #111519;
            --surface: #191f24;
            --surface-raised: #20272e;
            --surface-hover: #283139;
            --border: #303a43;
            --border-bright: #48545e;
            --text: #e5e8e9;
            --muted: #89949c;
            --dim: #59656e;
            --orange: #e0a43b;
            --orange-bright: #f1b94e;
            --orange-dark: #775322;
            --green: #75ad83;
        }

        body {
            margin: 0;
            padding: 0;
            width: 100%;
            height: 100vh;
            overflow: hidden;
            background: var(--background);
            color: var(--text);
            font-family: 'Segoe UI', Arial, sans-serif;
            font-size: 13px;
        }

        button,
        input {
            font-family: inherit;
        }

        .app {
            display: flex;
            flex-direction: column;
            width: 100%;
            height: 100%;
            padding: 20px 24px;
            gap: 16px;
        }

        /* HEADER */

        .header {
            display: flex;
            justify-content: space-between;
            align-items: center;
            min-height: 76px;
            padding-bottom: 15px;
            border-bottom: 1px solid var(--border);
        }
.main-navigation {
    display: flex;
    gap: 4px;
    padding: 0 0 12px 0;
    border-bottom: 1px solid var(--border);
}

.main-nav-button {
    background: transparent;
    border: 1px solid transparent;
    border-bottom: 2px solid transparent;
    color: var(--muted);
    padding: 12px 24px;
    font-size: 12px;
    font-weight: 700;
    letter-spacing: 1.5px;
    cursor: pointer;
    transition: all 0.15s ease;
}

.main-nav-button:hover {
    color: var(--text);
    background: var(--surface-hover);
}

.main-nav-button.active {
    color: var(--orange-bright);
    border-bottom-color: var(--orange);
    background: var(--surface);
}
.view-panel {
    flex: 1;
    min-height: 0;
    display: flex;
    flex-direction: column;
    gap: 16px;
}

.view-panel.hidden {
    display: none;
}
.procurement-product {
    display: flex;
    justify-content: space-between;
    align-items: center;
    gap: 32px;
    padding: 28px;
    background: var(--surface);
    border: 1px solid var(--border);
    border-left: 3px solid var(--orange);
.procurement-status {
    padding: 14px 20px;
    margin-top: 12px;
    background: var(--surface);
    border: 1px solid var(--border);
    border-left: 3px solid var(--orange);
    color: var(--muted);
    font-size: 11px;
    font-weight: 700;
    letter-spacing: 1px;
}
}

.procurement-product-info {
    flex: 1;
}

.procurement-product-name {
    color: var(--text);
    font-size: 15px;
    font-weight: 800;
    letter-spacing: 1.2px;
    margin-bottom: 12px;
}

.procurement-product-description {
    color: var(--muted);
    font-size: 12px;
    line-height: 1.6;
    max-width: 480px;
}

.procurement-product-price {
    color: var(--orange-bright);
    font-size: 22px;
    font-weight: 800;
    margin-top: 22px;
}

.procurement-product-price span {
    color: var(--muted);
    font-size: 10px;
    letter-spacing: 1px;
    margin-left: 8px;
}

.procurement-purchase-controls {
    width: 240px;
    display: flex;
    flex-direction: column;
    gap: 10px;
}

.procurement-purchase-controls label {
    color: var(--muted);
    font-size: 10px;
    font-weight: 700;
    letter-spacing: 1.2px;
}

.procurement-purchase-controls input {
    width: 100%;
    box-sizing: border-box;
    padding: 12px;
    background: var(--background);
    border: 1px solid var(--border);
    color: var(--text);
    font-size: 14px;
}

.procurement-total {
    color: var(--muted);
    font-size: 11px;
    font-weight: 700;
    margin: 6px 0;
}

.procurement-total span {
    color: var(--orange-bright);
    font-size: 15px;
}

.purchase-button {
    padding: 13px;
    background: linear-gradient(135deg, #e87518, #bd4d0b);
    border: 1px solid #ff9a42;
    color: #ffffff;
    font-size: 11px;
    font-weight: 900;
    letter-spacing: 1.2px;
    cursor: pointer;
    transition: background 0.2s ease, box-shadow 0.2s ease, transform 0.2s ease;
    box-shadow: 0 0 10px rgba(232, 117, 24, 0.18);
}

.purchase-button:hover:not(:disabled) {
    background: linear-gradient(135deg, #ff9235, #d85b0d);
    box-shadow: 0 0 16px rgba(255, 140, 50, 0.35);
    transform: translateY(-1px);
}

.purchase-button:active:not(:disabled) {
    transform: translateY(0);
}

.purchase-button:disabled {
    background: var(--surface-raised);
    border-color: var(--border);
    color: var(--dim);
    cursor: wait;
    box-shadow: none;
    opacity: 0.65;
}
        .brand {
            display: flex;
            align-items: center;
            gap: 15px;
        }

        .brand-mark {
    width: 55px;
    height: 55px;
    display: flex;
    align-items: center;
    justify-content: center;
    background: #29251e;
    border: 1px solid var(--orange-dark);
    color: var(--orange);
    flex-shrink: 0;
}

.brand-mark svg {
    width: 39px;
    height: 39px;
    fill: none;
    stroke: var(--orange);
    stroke-width: 1.8;
    stroke-linecap: round;
    stroke-linejoin: round;
}

        .brand-name {
            color: var(--orange);
            font-size: 25px;
            font-weight: 900;
            letter-spacing: 2px;
            line-height: 1.1;
        }

        .brand-subtitle {
            margin-top: 6px;
            color: var(--muted);
            font-size: 10px;
            font-weight: 700;
            letter-spacing: 3px;
        }

        .header-right {
            text-align: right;
        }

        .division-label {
            color: var(--muted);
            font-size: 10px;
            font-weight: 700;
            letter-spacing: 2px;
        }

        .system-status {
            display: flex;
            align-items: center;
            justify-content: flex-end;
            gap: 8px;
            margin-top: 9px;
            color: var(--green);
            font-size: 11px;
            font-weight: 800;
            letter-spacing: 1px;
        }

        .status-dot {
            width: 7px;
            height: 7px;
            border-radius: 50%;
            background: var(--green);
            box-shadow: 0 0 8px rgba(117, 173, 131, 0.5);
        }

        /* SECTION INTRO */

        .section-heading {
            display: flex;
            justify-content: space-between;
            align-items: flex-end;
        }

        .eyebrow {
            color: var(--orange);
            font-size: 10px;
            font-weight: 800;
            letter-spacing: 2.5px;
        }

        .section-title {
            margin-top: 5px;
            color: var(--text);
            font-size: 21px;
            font-weight: 800;
            letter-spacing: 1px;
        }

        .section-description {
            margin-top: 5px;
            color: var(--muted);
            font-size: 12px;
        }

        .classification {
            padding: 7px 10px;
            border: 1px solid var(--border-bright);
            color: var(--muted);
            font-size: 9px;
            font-weight: 800;
            letter-spacing: 1.5px;
        }

        /* MAIN LAYOUT */

        .workspace {
            display: grid;
            grid-template-columns: minmax(0, 1fr) 330px;
            gap: 16px;
            flex: 1;
            min-height: 0;
        }

        .inventory-panel,
        .manifest-panel {
            display: flex;
            flex-direction: column;
            min-height: 0;
            background: var(--surface);
            border: 1px solid var(--border);
        }

        .panel-header {
            display: flex;
            justify-content: space-between;
            align-items: center;
            padding: 14px 16px;
            border-bottom: 1px solid var(--border);
        }

        .panel-title {
            color: var(--text);
            font-size: 12px;
            font-weight: 900;
            letter-spacing: 1.5px;
        }

        .panel-count {
            color: var(--orange);
            font-size: 11px;
            font-weight: 800;
            letter-spacing: 1px;
        }

        /* INVENTORY TOOLBAR */

        .inventory-toolbar {
            display: flex;
            align-items: center;
            gap: 10px;
            padding: 12px 14px;
            border-bottom: 1px solid var(--border);
        }

        .search-box {
            flex: 1;
            min-width: 0;
            height: 38px;
            padding: 0 12px;
            background: #111519;
            color: var(--text);
            border: 1px solid var(--border-bright);
            border-radius: 2px;
            font-size: 12px;
        }

        .search-box:focus {
            outline: none;
            border-color: var(--orange);
        }

        .search-box::placeholder {
            color: var(--dim);
        }

        .toolbar-button {
            height: 38px;
            padding: 0 12px;
            background: var(--surface-raised);
            color: var(--orange);
            border: 1px solid var(--border-bright);
            border-radius: 2px;
            font-size: 10px;
            font-weight: 800;
            letter-spacing: 1px;
            cursor: pointer;
        }

        .toolbar-button:hover {
            background: var(--orange);
            color: #171b1e;
            border-color: var(--orange);
        }

.inventory-controls {
    display: flex;
    flex-direction: column;
    gap: 8px;
}

.inventory-sort-row {
    display: flex;
    gap: 8px;
}

.inventory-sort {
    flex: 1;
    height: 34px;
    padding: 0 10px;
    background: #111519;
    color: var(--text);
    border: 1px solid var(--border-bright);
    border-radius: 2px;
    font-size: 11px;
}

.inventory-sort:focus {
    outline: none;
    border-color: var(--orange);
}

.category-filters {
    display: flex;
    flex-wrap: wrap;
    gap: 5px;
}

.category-button {
    padding: 6px 9px;
    background: var(--surface-raised);
    color: var(--muted);
    border: 1px solid var(--border);
    border-radius: 2px;
    font-size: 9px;
    font-weight: 800;
    letter-spacing: 0.7px;
    cursor: pointer;
}

.category-button:hover {
    border-color: var(--orange-dark);
    color: var(--orange-bright);
}

.category-button.active {
    background: var(--orange-dark);
    border-color: var(--orange);
    color: #ffffff;
}

        /* INVENTORY GRID */

        .inventory-scroll {
            flex: 1;
            min-height: 0;
            overflow-y: auto;
            padding: 13px;
        }

        .inventory-grid {
            display: grid;
            grid-template-columns: repeat(2, minmax(0, 1fr));
            gap: 9px;
        }

        .item-card {
            display: flex;
            align-items: center;
            gap: 12px;
            min-width: 0;
            min-height: 105px;
            padding: 11px;
            background: var(--surface-raised);
            border: 1px solid var(--border);
            cursor: pointer;
            transition: background 0.12s, border-color 0.12s;
        }

        .item-card:hover {
            background: var(--surface-hover);
            border-color: var(--orange-dark);
        }

        .item-card.selected {
            background: #302a20;
            border: 1px solid var(--orange);
        }

        .item-image-frame {
            position: relative;
            display: flex;
            flex-shrink: 0;
            align-items: center;
            justify-content: center;
            width: 76px;
            height: 76px;
            background: #111519;
            border: 1px solid #3b454d;
        }

        .item-card.selected .item-image-frame {
            border-color: var(--orange-dark);
        }

        .item-image {
            width: 66px;
            height: 66px;
            object-fit: contain;
        }

        .image-fallback {
            color: var(--dim);
            font-size: 24px;
            font-weight: 900;
        }

        .item-information {
            flex: 1;
            min-width: 0;
        }

        .item-name {
            overflow: hidden;
            color: var(--text);
            font-size: 12px;
            font-weight: 800;
            line-height: 1.4;
            text-overflow: ellipsis;
        }

        .item-template {
            overflow: hidden;
            margin-top: 4px;
            color: var(--dim);
            font-size: 9px;
            text-overflow: ellipsis;
            white-space: nowrap;
        }

        .item-metadata {
            display: flex;
            flex-wrap: wrap;
            gap: 7px;
            margin-top: 9px;
            color: var(--muted);
            font-size: 10px;
        }

        .item-metadata strong {
            color: var(--orange);
            font-weight: 800;
        }

        .item-selection-tag {
            display: inline-block;
            margin-top: 7px;
            color: var(--orange);
            font-size: 9px;
            font-weight: 900;
            letter-spacing: 1px;
        }
        .container-user-tag {
    display: inline-block;
    margin-top: 7px;
    padding: 4px 7px;
    background: #302719;
    border: 1px solid var(--orange-dark);
    color: var(--orange-bright);
    font-size: 10px;
    font-weight: 900;
    letter-spacing: 1.5px;
}
        .empty {
            grid-column: 1 / -1;
            padding: 45px 20px;
            color: var(--muted);
            text-align: center;
            font-size: 12px;
            letter-spacing: 1px;
        }

        /* MANIFEST */

        .manifest-panel {
            background: #171d22;
        }

        .manifest-heading {
            padding: 16px;
            border-bottom: 1px solid var(--border);
        }

        .manifest-kicker {
            color: var(--orange);
            font-size: 9px;
            font-weight: 900;
            letter-spacing: 2px;
        }

        .manifest-title {
            margin-top: 7px;
            color: var(--text);
            font-size: 18px;
            font-weight: 900;
            letter-spacing: 1px;
        }

        .manifest-reference {
            margin-top: 7px;
            color: var(--muted);
            font-size: 10px;
            letter-spacing: 1px;
        }

        .manifest-list {
            flex: 1;
            min-height: 0;
            overflow-y: auto;
            padding: 10px;
        }

        .manifest-empty {
            padding: 35px 15px;
            color: var(--dim);
            text-align: center;
            font-size: 11px;
            line-height: 1.8;
        }

        .manifest-empty strong {
            display: block;
            margin-bottom: 8px;
            color: var(--muted);
            font-size: 12px;
            letter-spacing: 1px;
        }

        .manifest-item {
            padding: 11px;
            margin-bottom: 8px;
            background: var(--surface-raised);
            border: 1px solid var(--border);
        }

        .manifest-item-top {
            display: flex;
            align-items: center;
            gap: 9px;
        }

        .manifest-item-image {
            width: 42px;
            height: 42px;
            object-fit: contain;
            flex-shrink: 0;
            background: #111519;
            border: 1px solid var(--border);
        }

        .manifest-item-name {
            flex: 1;
            min-width: 0;
            color: var(--text);
            font-size: 11px;
            font-weight: 800;
            line-height: 1.4;
        }

        .manifest-remove {
            width: 24px;
            height: 24px;
            flex-shrink: 0;
            background: transparent;
            color: var(--muted);
            border: 1px solid var(--border);
            cursor: pointer;
        }

        .manifest-remove:hover {
            color: var(--orange);
            border-color: var(--orange);
        }

        .manifest-quantity-row {
            display: flex;
            justify-content: space-between;
            align-items: center;
            margin-top: 10px;
            padding-top: 9px;
            border-top: 1px solid var(--border);
        }

        .quantity-label {
            color: var(--muted);
            font-size: 9px;
            font-weight: 800;
            letter-spacing: 1px;
        }

        .quantity-controls {
            display: flex;
            align-items: center;
            gap: 6px;
        }

        .quantity-button {
            width: 25px;
            height: 25px;
            background: #303943;
            color: var(--orange);
            border: 1px solid var(--border-bright);
            font-size: 15px;
            font-weight: 900;
            cursor: pointer;
        }

        .quantity-button:hover:not(:disabled) {
            background: var(--orange);
            color: #171b1e;
        }

        .quantity-button:disabled {
            opacity: 0.3;
            cursor: default;
        }

        .quantity-input {
            width: 42px;
            height: 25px;
            padding: 2px;
            background: #111519;
            color: var(--orange);
            border: 1px solid var(--border-bright);
            text-align: center;
            font-size: 12px;
            font-weight: 900;
        }

        .quantity-input:focus {
            outline: none;
            border-color: var(--orange);
        }

        /* MANIFEST SUMMARY */

        .manifest-summary {
            padding: 14px;
            border-top: 1px solid var(--border);
            background: #13191e;
        }

        .summary-line {
            display: flex;
            justify-content: space-between;
            margin-bottom: 9px;
            color: var(--muted);
            font-size: 10px;
            font-weight: 700;
            letter-spacing: 1px;
        }

        .summary-line strong {
            color: var(--text);
            font-size: 11px;
        }

        .summary-line.total {
            margin-top: 12px;
            padding-top: 12px;
            border-top: 1px solid var(--border);
            color: var(--orange);
        }

        .summary-line.total strong {
            color: var(--orange);
            font-size: 17px;
        }

        .manifest-note {
            margin-top: 12px;
            color: var(--dim);
            font-size: 9px;
            line-height: 1.6;
        }

        .clear-selection {
            width: 100%;
            height: 34px;
            margin-top: 12px;
            background: transparent;
            color: var(--muted);
            border: 1px solid var(--border-bright);
            font-size: 9px;
            font-weight: 900;
            letter-spacing: 1.5px;
            cursor: pointer;
        }

        .clear-selection:hover {
            color: var(--orange);
            border-color: var(--orange);
        }

        /* FOOTER */

        .footer {
            display: flex;
            justify-content: space-between;
            align-items: center;
            min-height: 24px;
            color: var(--dim);
            font-size: 9px;
            font-weight: 700;
            letter-spacing: 1.2px;
        }

        .footer-accent {
            color: var(--orange-dark);
        }

        /* SCROLLBARS */

        ::-webkit-scrollbar {
            width: 7px;
        }

        ::-webkit-scrollbar-track {
            background: #111519;
        }

        ::-webkit-scrollbar-thumb {
            background: #46515a;
        }

        ::-webkit-scrollbar-thumb:hover {
            background: var(--orange-dark);
        }
    </style>
</head>

<body>
    <div class='app'>

        <header class='header'>
            <div class='brand'>
                <div class='brand-mark'>
    <svg viewBox=""0 0 48 48"" xmlns=""http://www.w3.org/2000/svg"">
        <path d=""M24 4 L28 18 L42 25 L42 29 L27 26 L26 40 L32 44 L32 46 L24 43 L16 46 L16 44 L22 40 L21 26 L6 29 L6 25 L20 18 Z""/>
        <path d=""M24 8 L24 39""/>
        <path d=""M12 24 L20 22""/>
        <path d=""M36 24 L28 22""/>
    </svg>
</div>

                <div>
                    <div class='brand-name'>
                        TACTICAL SUPPLY DROPS
                    </div>

                    <div class='brand-subtitle'>
                        PRIVATE AIR LOGISTICS
                    </div>
                </div>
            </div>

            <div class='header-right'>
                <div class='division-label'>
                    AIR LOGISTICS DIVISION
                </div>

                <div class='system-status'>
                    <span class='status-dot'></span>
                    <span id='system-status-text'>
                        AWAITING STASH DATA
                    </span>
                </div>
            </div>
        </header>
        <nav class='main-navigation'>
            <button class='main-nav-button active' data-view='cargo'>
                CARGO
            </button>

            <button class='main-nav-button' data-view='procurement'>
                PROCUREMENT
            </button>

            <button class='main-nav-button' data-view='config'>
                CONFIG
            </button>
        </nav>
        <div id='cargo-view' class='view-panel'>
        <section class='section-heading'>
            <div>
                <div class='eyebrow'>
                    LOGISTICS OPERATIONS // 01
                </div>

                <div class='section-title'>
                    STASH REQUISITION
                </div>

                <div class='section-description'>
                    Select cargo from your available inventory for a future delivery order.
                </div>
            </div>

            <div class='classification'>
                PRIVATE LOGISTICS NETWORK
            </div>
        </section>

        <main class='workspace'>

            <section class='inventory-panel'>

                <div class='panel-header'>
                    <span class='panel-title'>
                        AVAILABLE CARGO
                    </span>

                    <span class='panel-count' id='item-count'>
                        0 ITEMS
                    </span>
                </div>

              <div class='inventory-toolbar'>
    <input
        id='search'
        class='search-box'
        type='text'
        placeholder='Search cargo by name or item ID...'>

    <button
        id='clear-search'
        class='toolbar-button'
        type='button'>
        RESET
    </button>

    <button
        id='refresh-stash'
        class='toolbar-button'
        type='button'>
        ↻ REFRESH
    </button>
</div>

     <div class='inventory-controls'>

         <div class='inventory-sort-row'>
             <select id='inventory-sort' class='inventory-sort'>
                 <option value='name-asc'>SORT: NAME A–Z</option>
                 <option value='name-desc'>SORT: NAME Z–A</option>
                 <option value='value-desc'>SORT: HIGHEST VALUE</option>
                 <option value='value-asc'>SORT: LOWEST VALUE</option>
             </select>
         </div>

         <div class='category-filters'>
    <button class='category-button active' data-category='ALL'>ALL</button>
    <button class='category-button' data-category='AMMO'>AMMO</button>
    <button class='category-button' data-category='MEDICAL'>MEDICAL</button>
    <button class='category-button' data-category='FOOD'>FOOD & DRINKS</button>
    <button class='category-button' data-category='WEAPONS'>WEAPONS</button>
    <button class='category-button' data-category='ARMOR'>ARMOR</button>
    <button class='category-button' data-category='CONTAINERS'>CONTAINERS</button>
    <button class='category-button' data-category='OTHER'>OTHER</button>
</div>

     </div>

                <div class='inventory-scroll'>
                    <div id='item-list' class='inventory-grid'>
                        <div class='empty'>
                            AWAITING INVENTORY DATA
                        </div>
                    </div>
                </div>

            </section>

            <aside class='manifest-panel'>

                <div class='manifest-heading'>
                    <div class='manifest-kicker'>
                        CARGO DOCUMENT // DRAFT
                    </div>

                    <div class='manifest-title'>
                        FLIGHT MANIFEST
                    </div>

                    <div class='manifest-reference'>
                        REQUISITION ID: <span id='manifest-reference'>PENDING</span>
                    </div>
                </div>

                <div id='manifest-list' class='manifest-list'>
                    <div class='manifest-empty'>
                        <strong>NO CARGO ASSIGNED</strong>
                        Select items from the available cargo inventory
                        to begin assembling your requisition.
                    </div>
                </div>

                <div class='manifest-summary'>

                    <div class='summary-line'>
                        <span>SELECTED ITEM TYPES</span>
                        <strong id='selected-count'>0</strong>
                    </div>

                    <div class='summary-line'>
                        <span>TOTAL CARGO UNITS</span>
                        <strong id='selected-units'>0</strong>
                    </div>

                    <div class='summary-line total'>
                        <span>EST. BASE VALUE</span>
                        <strong id='selected-value'>₽0</strong>
                    </div>

                    <div class='manifest-note'>
    This is a draft requisition summary.
</div>

<div id='order-status' class='manifest-note'>
    Orders are ready for connection.
</div>

<button id='submit-order' class='clear-selection'>
    SUBMIT SUPPLY ORDER
</button>

                    <button
                        id='clear-selection'
                        class='clear-selection'
                        type='button'>
                        CLEAR MANIFEST
                    </button>

                </div>

            </aside>

                </main>

        </div>
        <div id='procurement-view' class='view-panel hidden'>
            <section class='section-heading'>
                <div>
                    <div class='eyebrow'>
                        LOGISTICS OPERATIONS // 02
                    </div>

                    <div class='section-title'>
                        PROCUREMENT
                    </div>

                    <div class='section-description'>
                        Acquire specialist equipment through Tactical Supply Drops.
                    </div>
                </div>

                <div class='classification'>
                    PRIVATE LOGISTICS NETWORK
                </div>
            </section>

            <section class='inventory-panel'>
                <div class='panel-header'>
                    <span class='panel-title'>
                        AVAILABLE SUPPLIES
                    </span>
                </div>

                                <div class='procurement-product'>
                    <div class='procurement-product-info'>
                        <div class='procurement-product-name'>
                            TSD ORANGE SIGNAL CARTRIDGE
                        </div>

                        <div class='procurement-product-description'>
                            Specialized 26x75mm orange signal cartridge for Tactical Supply Drops operations.
                        </div>

                        <div class='procurement-product-price'>
                            ₽15,000 <span>PER UNIT</span>
                        </div>
                    </div>

                    <div class='procurement-purchase-controls'>
                        <label for='cartridge-quantity'>
                            QUANTITY
                        </label>

                        <input
                            id='cartridge-quantity'
                            type='number'
                            min='1'
                            step='1'
                            value='1'
                        />

                        <div class='procurement-total'>
                            TOTAL: <span id='cartridge-total'>₽15,000</span>
                        </div>

                        <button
    id='purchase-cartridges'
    class='purchase-button'
    type='button'
>
    PURCHASE CARTRIDGES
</button>
            </div>
        </div>

        <div
            id='procurement-status'
            class='procurement-status'
        >
            READY FOR PROCUREMENT
        </div>

    </section>
</div>
        <div id='config-view' class='view-panel hidden'>
            <section class='section-heading'>
                <div>
                    <div class='eyebrow'>
                        LOGISTICS OPERATIONS // 03
                    </div>

                    <div class='section-title'>
                        CONFIGURATION
                    </div>

                    <div class='section-description'>
                        Manage your Tactical Supply Drops interface preferences.
                    </div>
                </div>

                <div class='classification'>
                    PRIVATE LOGISTICS NETWORK
                </div>
            </section>

            <section class='inventory-panel'>
                <div class='panel-header'>
                    <span class='panel-title'>
                        OPERATIONAL PREFERENCES
                    </span>
                </div>

                <div style='padding: 24px; color: var(--muted);'>
                    CONFIGURATION MODULE INITIALIZING
                </div>
            </section>
        </div>

        <footer class='footer'>
            <span>
                <span class='footer-accent'>TSD</span>
                // PRIVATE AIR LOGISTICS NETWORK
            </span>

            <span>
                REQUISITION TERMINAL // DEVELOPMENT BUILD 0.2
            </span>
        </footer>

    </div>

    
<script>
    let stashItems = [];
// Main interface navigation
const mainNavButtons = document.querySelectorAll('.main-nav-button');

const mainViews = {
    cargo: document.getElementById('cargo-view'),
    procurement: document.getElementById('procurement-view'),
    config: document.getElementById('config-view')
};

function switchMainView(viewName) {
    Object.entries(mainViews).forEach(([name, view]) => {
        if (view) {
            view.classList.toggle('hidden', name !== viewName);
        }
    });

    mainNavButtons.forEach(button => {
        button.classList.toggle(
            'active',
            button.dataset.view === viewName
        );
    });
}

mainNavButtons.forEach(button => {
    button.addEventListener('click', () => {
        switchMainView(button.dataset.view);
    });
});
// Procurement cartridge price calculation
const cartridgePrice = 15000;

const cartridgeQuantityInput =
    document.getElementById('cartridge-quantity');

const cartridgeTotalDisplay =
    document.getElementById('cartridge-total');

function updateCartridgeTotal() {
    const quantity = Number(cartridgeQuantityInput.value);

    if (
        !Number.isSafeInteger(quantity) ||
        quantity < 1
    ) {
        cartridgeTotalDisplay.textContent = 'INVALID QUANTITY';
        return;
    }

    const total = quantity * cartridgePrice;

    if (!Number.isSafeInteger(total)) {
        cartridgeTotalDisplay.textContent = 'INVALID QUANTITY';
        return;
    }

    cartridgeTotalDisplay.textContent =
        '₽' + total.toLocaleString('en-CA');
}

cartridgeQuantityInput.addEventListener(
    'input',
    updateCartridgeTotal
);
// Procurement purchase submission
let procurementPending = false;

const purchaseCartridgesButton =
    document.getElementById('purchase-cartridges');

const procurementStatus =
    document.getElementById('procurement-status');

function updateProcurementStatus(message) {
    if (procurementStatus) {
        procurementStatus.textContent = message;
    }
}

purchaseCartridgesButton.addEventListener('click', () => {
    if (procurementPending) {
        return;
    }

    const quantity = Number(cartridgeQuantityInput.value);

    if (!Number.isSafeInteger(quantity) || quantity < 1) {
        updateProcurementStatus('PLEASE ENTER A VALID QUANTITY');
        return;
    }

    const total = quantity * cartridgePrice;

    if (
        !Number.isSafeInteger(total) ||
        total > 2147483647
    ) {
        updateProcurementStatus('PURCHASE TOTAL EXCEEDS THE ALLOWED LIMIT');
        return;
    }

    const requestId =
        Date.now().toString() + '-' +
        Math.random().toString(36).substring(2, 10);

    const payload = {
    data: [
        {
            Action: ""TacticalSupplyDropsPurchaseCartridges"",
            RequestId: requestId,
            Quantity: quantity
        }
    ]
};

    procurementPending = true;

    purchaseCartridgesButton.disabled = true;
    purchaseCartridgesButton.textContent = 'PROCESSING...';

    updateProcurementStatus('SUBMITTING PURCHASE REQUEST...');

    window.overlay.send(
        'tsd-purchase-cartridges',
        JSON.stringify(payload)
    );
});
    let selectedItems = {};

    // Inventory navigation
    // Inventory navigation
const stashRootParentId = '664115f68b8ae85875060b39';
let currentContainerId = stashRootParentId;
let containerHistory = [];
    const statusText =
        document.getElementById('system-status-text');

    const itemList =
        document.getElementById('item-list');

    const manifestList =
        document.getElementById('manifest-list');

    const itemCount =
        document.getElementById('item-count');

    const searchBox =
        document.getElementById('search');

    const selectedCount =
        document.getElementById('selected-count');

    const selectedUnits =
        document.getElementById('selected-units');

    const selectedValue =
        document.getElementById('selected-value');

    const manifestReference =
        document.getElementById('manifest-reference');

    const clearSelectionButton =
        document.getElementById('clear-selection');
window.onStashRefreshComplete = function() {
    if (refreshStashButton) {
        refreshStashButton.disabled = false;
        refreshStashButton.textContent = '↻ REFRESH';
    }

    console.log(
        'TSD stash refresh completed.'
    );
};
const refreshStashButton =
    document.getElementById('refresh-stash');

if (refreshStashButton) {
    refreshStashButton.addEventListener('click', () => {
        if (window.overlay && window.overlay.send) {
            refreshStashButton.disabled = true;
            refreshStashButton.textContent = '↻ REFRESHING...';

            window.overlay.send(
                'tsd-refresh-stash',
                'refresh'
            );

            console.log(
                'TSD requested a stash refresh.'
            );
        }
    });
}
    // Create navigation controls above the inventory grid.
    const navigationBar = document.createElement('div');

    navigationBar.style.display = 'flex';
    navigationBar.style.alignItems = 'center';
    navigationBar.style.gap = '12px';
    navigationBar.style.padding = '10px 14px';
    navigationBar.style.background = '#151b20';
    navigationBar.style.borderBottom = '1px solid #303a43';
    navigationBar.style.minHeight = '44px';

    const backButton = document.createElement('button');

    backButton.className = 'toolbar-button';
    backButton.textContent = '← BACK';
    backButton.style.display = 'none';

    const locationLabel = document.createElement('span');

    locationLabel.style.color = '#e0a43b';
    locationLabel.style.fontSize = '10px';
    locationLabel.style.fontWeight = '800';
    locationLabel.style.letterSpacing = '1.5px';

    locationLabel.textContent = 'MAIN STASH';

    navigationBar.appendChild(backButton);
    navigationBar.appendChild(locationLabel);

    const inventoryPanel =
        document.querySelector('.inventory-panel');

    const inventoryToolbar =
        document.querySelector('.inventory-toolbar');

    inventoryPanel.insertBefore(
        navigationBar,
        inventoryToolbar.nextSibling
    );

    function cleanItemName(name) {
        return (name || '')
            .replace(/<color[^>]*>|<\/color>/gi, '')
            .replace(/\s*\[\d+\s*\/\s*\d+\]/g, '')
            .trim();
    }

    function getMaxQuantity(item) {
        return Math.max(1, Number(item.StackCount) || 1);
    }

    function formatNumber(value) {
        return Number(value || 0).toLocaleString('en-US');
    }

    function getSelectedItemCount() {
        return Object.keys(selectedItems).length;
    }

    function getSelectedUnitCount() {
        return Object.values(selectedItems).reduce(
            (total, quantity) => total + quantity,
            0
        );
    }

    function getSelectedBaseValue() {
        let total = 0;

        Object.keys(selectedItems).forEach(id => {
            const item = stashItems.find(entry => entry.Id === id);

            if (item) {
                total +=
                    (Number(item.BaseValue) || 0) *
                    selectedItems[id];
            }
        });

        return total;
    }

    function getItemById(id) {
        return stashItems.find(item => item.Id === id);
    }

    function updateStatus(message) {
        statusText.textContent = message;
    }

    // Determine whether an item contains other items.
    
function isContainer(item) {
    if (!item || !item.Id) {
        return false;
    }

    const containerId = String(item.Id);

    return stashItems.some(child =>
        String(child.ParentId) === containerId
    );
}

    

    function getContainerName(id) {
        const container = getItemById(id);

        return container
            ? cleanItemName(container.Name)
            : 'UNKNOWN CONTAINER';
    }

    // Open a container and display its contents.
    
function openContainer(item) {
    if (!item || !item.Id || !isContainer(item)) {
        return;
    }

    containerHistory.push(currentContainerId);
    currentContainerId = String(item.Id);

    searchBox.value = '';

    displayItems();
}


    // Return to the previous inventory level.
    function goBack() {
        if (containerHistory.length === 0) {
            return;
        }

        currentContainerId = containerHistory.pop();

        displayItems();
    }

    backButton.addEventListener('click', goBack);

  
function updateNavigation() {
    if (
        currentContainerId === null ||
        currentContainerId === undefined ||
        String(currentContainerId) === String(stashRootParentId)
    ) {
        backButton.style.display = 'none';
        locationLabel.textContent = 'STASH';
    } else {
        backButton.style.display = 'block';

        locationLabel.textContent =
            'CONTAINER // ' +
            getContainerName(currentContainerId).toUpperCase();
    }
}


    function toggleItemSelection(item) {
        if (orderPending) {
            return;
        }

        if (!item || !item.Id) {
            return;
        }

        if (selectedItems[item.Id] !== undefined) {
            delete selectedItems[item.Id];
        } else {
            selectedItems[item.Id] = 1;
        }

        displayItems();
    }

    function removeItemFromManifest(id) {
        if (orderPending) {
            return;
        }

        delete selectedItems[id];
        displayItems();
    }

    function changeQuantity(item, amount) {
        if (orderPending) {
            return;
        }

        const id = item.Id;

        if (!id || selectedItems[id] === undefined) {
            return;
        }

        const maximum = getMaxQuantity(item);

        selectedItems[id] = Math.min(
            maximum,
            Math.max(1, selectedItems[id] + amount)
        );

        displayItems();
    }

    function setQuantity(item, value) {
        if (orderPending) {
            return;
        }

        const id = item.Id;

        if (!id || selectedItems[id] === undefined) {
            return;
        }

        const maximum = getMaxQuantity(item);

        let requested = parseInt(value, 10);

        if (isNaN(requested)) {
            requested = 1;
        }

        selectedItems[id] = Math.min(
            maximum,
            Math.max(1, requested)
        );

        displayItems();
    }

    function reconcileSelections() {
        const validIds = {};

        stashItems.forEach(item => {
            if (item && item.Id) {
                validIds[item.Id] = item;
            }
        });

        Object.keys(selectedItems).forEach(id => {
            const item = validIds[id];

            if (!item) {
                delete selectedItems[id];
                return;
            }

            selectedItems[id] = Math.min(
                getMaxQuantity(item),
                selectedItems[id]
            );
        });
    }

    function createItemImage(item, className) {
        const frame = document.createElement('div');

        frame.className = 'item-image-frame';

        const fallback = document.createElement('span');

        fallback.className = 'image-fallback';
        fallback.textContent = 'TSD';

        frame.appendChild(fallback);

        if (item.IconUrl) {
            const image = document.createElement('img');

            image.className = className || 'item-image';
            image.src = item.IconUrl;
            image.alt = cleanItemName(item.Name);

            image.onload = () => {
                fallback.style.display = 'none';
            };

            image.onerror = () => {
                image.remove();
                fallback.style.display = 'flex';
            };

            frame.appendChild(image);
        }

        return frame;
    }

    function createInventoryCard(item, searchMode) {
        const id = item.Id;
        const selected = selectedItems[id] !== undefined;
        const container = isContainer(item);

        const card = document.createElement('div');

        card.className =
            'item-card' + (selected ? ' selected' : '');

        card.addEventListener('click', () => {
            if (container && !searchMode) {
                openContainer(item);
            } else {
                toggleItemSelection(item);
            }
        });

        card.appendChild(createItemImage(item, 'item-image'));

        const information = document.createElement('div');
        information.className = 'item-information';

        const name = document.createElement('div');
        name.className = 'item-name';
        name.textContent = cleanItemName(item.Name);

        const template = document.createElement('div');
        template.className = 'item-template';
        template.textContent = item.TemplateId || 'UNKNOWN ID';

        const metadata = document.createElement('div');
        metadata.className = 'item-metadata';

        const quantity = document.createElement('span');

        quantity.innerHTML =
            'QTY: <strong>' +
            formatNumber(item.StackCount) +
            '</strong>';

        const dimensions = document.createElement('span');

        dimensions.textContent =
            (item.Width || 1) + ' × ' + (item.Height || 1);

        metadata.appendChild(quantity);
        metadata.appendChild(dimensions);

        information.appendChild(name);
        information.appendChild(template);
        information.appendChild(metadata);

        if (searchMode && item.ParentId !== stashRootParentId) {
            const parent = getItemById(item.ParentId);

            if (parent) {
                const location = document.createElement('div');

                location.className = 'item-selection-tag';
                location.textContent =
                    'LOCATION: ' + cleanItemName(parent.Name);

                information.appendChild(location);
            }
        }

        if (container) {
            const containerTag = document.createElement('div');
    if (item.Tag && item.Tag.trim()) {
        const userTag = document.createElement('div');

        userTag.className = 'container-user-tag';
        userTag.textContent = item.Tag.toUpperCase();

        information.appendChild(userTag);
    }
            containerTag.className = 'item-selection-tag';
            containerTag.textContent =
                'CONTAINER // ' +
                stashItems.filter(
                    child => child.ParentId === item.Id
                ).length +
                ' CONTENTS';

            information.appendChild(containerTag);

            // Containers can still be selected as cargo.
            const selectButton = document.createElement('button');

            selectButton.className = 'toolbar-button';
            selectButton.type = 'button';
            selectButton.textContent = selected
                ? 'REMOVE'
                : 'SELECT';

            selectButton.style.marginTop = '8px';
            selectButton.style.height = '27px';

            selectButton.addEventListener('click', event => {
                event.stopPropagation();
                toggleItemSelection(item);
            });

            information.appendChild(selectButton);
        } else if (selected) {
            const tag = document.createElement('div');

            tag.className = 'item-selection-tag';
            tag.textContent = 'ASSIGNED TO MANIFEST';

            information.appendChild(tag);
        }

        card.appendChild(information);

        return card;
    }

 
let activeCategory = 'ALL';
let activeSort = 'name-asc';

function getItemCategory(item) {
    return item.Category || 'OTHER';
}

function sortInventoryItems(items) {
    return [...items].sort((a, b) => {
        switch (activeSort) {
            case 'name-desc':
                return cleanItemName(b.Name).localeCompare(
                    cleanItemName(a.Name)
                );

            case 'value-desc':
                return (b.BaseValue || 0) - (a.BaseValue || 0);

            case 'value-asc':
                return (a.BaseValue || 0) - (b.BaseValue || 0);

            case 'name-asc':
            default:
                return cleanItemName(a.Name).localeCompare(
                    cleanItemName(b.Name)
                );
        }
    });
}
   


function displayItems() {
    const search = searchBox.value.trim().toLowerCase();
    const searchMode = search.length > 0;
    const categoryMode = activeCategory !== 'ALL';

    let filteredItems;

    if (searchMode || categoryMode) {
        // Search the entire stash when searching or filtering.
        filteredItems = stashItems.filter(item => {
            const name =
                cleanItemName(item.Name).toLowerCase();

            const template =
                (item.TemplateId || '').toLowerCase();

            const matchesSearch =
                !searchMode ||
                name.includes(search) ||
                template.includes(search);

            const matchesCategory =
                !categoryMode ||
                getItemCategory(item) === activeCategory;

            return matchesSearch && matchesCategory;
        });
    } else {
        // Otherwise, show items inside the current container.
        filteredItems = stashItems.filter(item =>
            String(item.ParentId) === String(currentContainerId)
        );
    }

    // Apply selected sorting.
    filteredItems = sortInventoryItems(filteredItems);

    itemList.innerHTML = '';

    if (filteredItems.length === 0) {
        const empty = document.createElement('div');
        empty.className = 'empty';

        if (searchMode || categoryMode) {
            empty.textContent = 'NO MATCHING CARGO FOUND';
        } else {
            empty.textContent = 'THIS CONTAINER IS EMPTY';
        }

        itemList.appendChild(empty);
    } else {
        filteredItems.forEach(item => {
            itemList.appendChild(
                createInventoryCard(item, searchMode)
            );
        });
    }

    itemCount.textContent =
        formatNumber(filteredItems.length) +
        (searchMode ? ' MATCHES' : ' ITEMS');

    updateNavigation();
    displayManifest();
}


const sortControl = document.getElementById('inventory-sort');

if (sortControl) {
    sortControl.addEventListener('change', () => {
        activeSort = sortControl.value;
        displayItems();
    });
}

document.querySelectorAll('.category-button').forEach(button => {
    button.addEventListener('click', () => {
        activeCategory = button.dataset.category || 'ALL';

        document.querySelectorAll('.category-button').forEach(filterButton => {
            filterButton.classList.toggle(
                'active',
                filterButton === button
            );
        });

        displayItems();
    });
});



    function displayManifest() {
        manifestList.innerHTML = '';

        const selectedIds = Object.keys(selectedItems);

        if (selectedIds.length === 0) {
            const empty = document.createElement('div');

            empty.className = 'manifest-empty';

            const heading = document.createElement('strong');

            heading.textContent = 'NO CARGO ASSIGNED';

            empty.appendChild(heading);

            empty.appendChild(
                document.createTextNode(
                    'Select items from the available cargo inventory ' +
                    'to begin assembling your requisition.'
                )
            );

            manifestList.appendChild(empty);
        } else {
            selectedIds.forEach(id => {
                const item = getItemById(id);

                if (!item) {
                    return;
                }

                const entry = document.createElement('div');
                entry.className = 'manifest-item';

                const top = document.createElement('div');
                top.className = 'manifest-item-top';

                const image = createItemImage(
                    item,
                    'manifest-item-image'
                );

                image.className = 'item-image-frame';
                image.style.width = '42px';
                image.style.height = '42px';

                const name = document.createElement('div');
                name.className = 'manifest-item-name';
                name.textContent = cleanItemName(item.Name);

                const remove = document.createElement('button');

                remove.className = 'manifest-remove';
                remove.type = 'button';
                remove.textContent = '×';
                remove.title = 'Remove from manifest';

                remove.addEventListener('click', () => {
                    removeItemFromManifest(id);
                });

                top.appendChild(image);
                top.appendChild(name);
                top.appendChild(remove);

                const quantityRow = document.createElement('div');

                quantityRow.className = 'manifest-quantity-row';

                const label = document.createElement('span');

                label.className = 'quantity-label';
                label.textContent = 'QUANTITY';

                const controls = document.createElement('div');

                controls.className = 'quantity-controls';

                const minus = document.createElement('button');

                minus.className = 'quantity-button';
                minus.type = 'button';
                minus.textContent = '−';
                minus.disabled = selectedItems[id] <= 1;

                minus.addEventListener('click', () => {
                    changeQuantity(item, -1);
                });

                const input = document.createElement('input');

                input.className = 'quantity-input';
                input.type = 'number';
                input.min = '1';
                input.max = String(getMaxQuantity(item));
                input.value = String(selectedItems[id]);

                input.addEventListener('change', () => {
                    setQuantity(item, input.value);
                });

                input.addEventListener('keydown', event => {
                    if (event.key === 'Enter') {
                        input.blur();
                    }
                });

                const plus = document.createElement('button');

                plus.className = 'quantity-button';
                plus.type = 'button';
                plus.textContent = '+';

                plus.disabled =
                    selectedItems[id] >= getMaxQuantity(item);

                plus.addEventListener('click', () => {
                    changeQuantity(item, 1);
                });

                controls.appendChild(minus);
                controls.appendChild(input);
                controls.appendChild(plus);

                quantityRow.appendChild(label);
                quantityRow.appendChild(controls);

                entry.appendChild(top);
                entry.appendChild(quantityRow);

                manifestList.appendChild(entry);
            });
        }

        selectedCount.textContent =
            formatNumber(getSelectedItemCount());

        selectedUnits.textContent =
            formatNumber(getSelectedUnitCount());

        selectedValue.textContent =
            '₽' + formatNumber(getSelectedBaseValue());
    }

    
function loadStash(data) {
    if (
        !data ||
        !data.success ||
        !Array.isArray(data.items)
    ) {
        updateStatus('STASH DATA UNAVAILABLE');
        return;
    }

    stashItems = data.items;

    // Use the confirmed stash root ID.
    currentContainerId = stashRootParentId;
    containerHistory = [];

    reconcileSelections();

    updateStatus('INVENTORY SYNCHRONIZED');

    displayItems();

    manifestReference.textContent = 'DRAFT';
}


    searchBox.addEventListener('input', displayItems);

    document.getElementById('clear-search')
        .addEventListener('click', () => {
            searchBox.value = '';
            displayItems();
        });

    clearSelectionButton.addEventListener('click', () => {
        if (orderPending) {
            return;
        }

        selectedItems = {};
        displayItems();
    });

    // ORDER SUBMISSION

    let orderPending = false;

    function updateOrderStatus(message) {
        const statusElement =
            document.getElementById('order-status');

        if (statusElement) {
            statusElement.textContent = message;
        }
    }

    function submitSupplyOrder() {
        if (orderPending) {
            return;
        }

        const selectedIds = Object.keys(selectedItems);

        if (selectedIds.length === 0) {
            updateOrderStatus('SELECT AT LEAST ONE ITEM');
            return;
        }

        const requestId =
            Date.now().toString() + '-' +
            Math.random().toString(36).substring(2, 10);

        const payload = {
            data: [
                {
                    Action: ""TacticalSupplyDropsCreateOrder"",
                    RequestId: requestId,
                    SelectedItemIds: selectedIds,
                    SelectedItemQuantities: selectedItems
                }
            ]
        };

        orderPending = true;

        const submitButton =
            document.getElementById('submit-order');

        if (submitButton) {
            submitButton.disabled = true;
        }

        updateOrderStatus('SENDING ORDER REQUEST...');

        window.overlay.send(
            'tsd-submit-order',
            JSON.stringify(payload)
        );
    }

    const submitButton =
        document.getElementById('submit-order');

    if (submitButton) {
        submitButton.addEventListener(
            'click',
            submitSupplyOrder
        );
    }

    window.overlay.onOrderStatusReceived = function(message) {
        updateOrderStatus(message);

        if (message === 'ORDER REQUEST FAILED') {
            orderPending = false;

            const button =
                document.getElementById('submit-order');

            if (button) {
                button.disabled = false;
            }
        }
    };

    window.overlay.onResponseReceived = function(message) {
        console.log('TSD C# response received: ' + message);
        updateStatus('C# COMMUNICATION CONFIRMED');
    };

    window.overlay.onOrderResponseReceived = function(responseJson) {
        let response;

        try {
            response = JSON.parse(responseJson);
        } catch (error) {
            console.error(
                'TSD could not parse the order response:',
                error
            );

            updateOrderStatus('INVALID CLIENT RESPONSE');
            orderPending = false;

            const button =
                document.getElementById('submit-order');

            if (button) {
                button.disabled = false;
            }

            return;
        }

        console.log('TSD order response:', response);

        if (!response.transportSuccess) {
            updateOrderStatus(
                response.message || 'ORDER SUBMISSION FAILED'
            );
        } else {
            let serverResult;

            try {
                serverResult = JSON.parse(response.serverResponse);
            } catch (error) {
                console.error(
                    'TSD could not parse the server response:',
                    error
                );

                updateOrderStatus(
                    'SERVER RESPONSE COULD NOT BE VERIFIED'
                );

                orderPending = false;

                const button =
                    document.getElementById('submit-order');

                if (button) {
                    button.disabled = false;
                }

                return;
            }

            if (
                serverResult.err !== 0 ||
                !serverResult.data
            ) {
                console.error(
                    'TSD server returned an error:',
                    serverResult
                );

                updateOrderStatus(
                    serverResult.errmsg || 'ORDER SUBMISSION FAILED'
                );
            } else if (
                Array.isArray(serverResult.data.warnings) &&
                serverResult.data.warnings.length > 0
            ) {
                console.warn(
                    'TSD server warnings:',
                    serverResult.data.warnings
                );

                updateOrderStatus(
                    'ORDER REJECTED — CHECK SERVER WARNINGS'
                );
            } else {
                updateOrderStatus('ORDER ACCEPTED BY SERVER');

                if (window.overlay && window.overlay.send) {
                    window.overlay.send(
                        'tsd-lookup-order',
                        JSON.stringify({
                            RequestId: response.requestId
                        })
                    );

                    console.log(
                        'TSD requested cargo for order:',
                        response.requestId
                    );
                }

                selectedItems = {};
                displayItems();

                if (window.overlay && window.overlay.send) {
                    window.overlay.send(
                        'tsd-refresh-stash',
                        'refresh'
                    );
                }
            }
        }

        orderPending = false;

        const button =
            document.getElementById('submit-order');

        if (button) {
            button.disabled = false;
        }
    };
// Procurement request status callback
window.overlay.onProcurementStatusReceived = function(message) {
    updateProcurementStatus(message);

    if (message === 'PURCHASE REQUEST FAILED') {
        procurementPending = false;

        purchaseCartridgesButton.disabled = false;
        purchaseCartridgesButton.textContent = 'PURCHASE CARTRIDGES';
    }
};

// Procurement server response callback
window.overlay.onProcurementResponseReceived = function(responseJson) {
    let response;

    try {
        response = JSON.parse(responseJson);
    } catch (error) {
        console.error(
            'TSD could not parse the procurement response:',
            error
        );

        updateProcurementStatus('INVALID PROCUREMENT RESPONSE');

        procurementPending = false;
        purchaseCartridgesButton.disabled = false;
        purchaseCartridgesButton.textContent = 'PURCHASE CARTRIDGES';

        return;
    }

    console.log('TSD procurement response:', response);

    if (!response.transportSuccess) {
        updateProcurementStatus(
            response.message || 'PURCHASE SUBMISSION FAILED'
        );
    } else {
        let serverResult;

        try {
            serverResult = JSON.parse(response.serverResponse);
        } catch (error) {
            console.error(
                'TSD could not parse the procurement server response:',
                error
            );

            updateProcurementStatus(
                'SERVER RESPONSE COULD NOT BE VERIFIED'
            );

            procurementPending = false;
            purchaseCartridgesButton.disabled = false;
            purchaseCartridgesButton.textContent = 'PURCHASE CARTRIDGES';

            return;
        }

        if (
            serverResult.err !== 0 ||
            !serverResult.data
        ) {
            console.error(
                'TSD procurement server returned an error:',
                serverResult
            );

            updateProcurementStatus(
                serverResult.errmsg || 'PURCHASE FAILED'
            );
        } else if (
            Array.isArray(serverResult.data.warnings) &&
            serverResult.data.warnings.length > 0
        ) {
            console.warn(
                'TSD procurement server warnings:',
                serverResult.data.warnings
            );

            updateProcurementStatus(
                serverResult.data.warnings[0].errorMessage ||
                'PURCHASE REJECTED — CHECK SERVER WARNINGS'
            );
        } else {
            updateProcurementStatus(
                'PURCHASE COMPLETE — CHECK EFT MESSENGER'
            );
        }
    }

    procurementPending = false;

    purchaseCartridgesButton.disabled = false;
    purchaseCartridgesButton.textContent = 'PURCHASE CARTRIDGES';
};

    // RECEIVE STASH DATA

    if (window.overlay && window.overlay.on) {
        window.overlay.on('stash', json => {
            try {
                const data = typeof json === 'string'
                    ? JSON.parse(json)
                    : json;

                loadStash(data);

                if (window.overlay.send) {
                    window.overlay.send(
                        'tsd-test',
                        'F9 communication is working!'
                    );
                }
            } catch (error) {
                updateStatus('ERROR READING STASH DATA');
                console.error(error);
            }
        });

        console.log(
            'TSD DIAGNOSTIC: window.overlay =',
            typeof window.overlay
        );

        console.log(
            'TSD DIAGNOSTIC: window.overlay.send =',
            typeof window.overlay.send
        );

        if (window.overlay.send) {
            console.log(
                'TSD DIAGNOSTIC: Sending tsd-ready message.'
            );

            window.overlay.send(
                'tsd-ready',
                'ready'
            );
        } else {
            console.error(
                'TSD DIAGNOSTIC: Cannot send tsd-ready message.'
            );
        }
    }

    displayItems();
</script>

</body>
</html>
            ");

           
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
                        "window.overlay.onOrderResponseReceived && " +
                        "window.overlay.onOrderResponseReceived('" +
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
                        "window.overlay.onProcurementResponseReceived && " +
                        "window.overlay.onProcurementResponseReceived('" +
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
                        "window.overlay.onOrderStatusReceived && " +
                        "window.overlay.onOrderStatusReceived(" +
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
                        "window.overlay.onOrderStatusReceived && " +
                        "window.overlay.onOrderStatusReceived(" +
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
                        "window.overlay.onProcurementStatusReceived && " +
                        "window.overlay.onProcurementStatusReceived(" +
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
                        "window.overlay.onProcurementStatusReceived && " +
                        "window.overlay.onProcurementStatusReceived(" +
                        "'PURCHASE REQUEST FAILED');"
                    );
                }
            }
        }
        private void OnDestroy()
        {
            if (overlay != null)
            {
                overlay.Dispose();
                overlay = null;
            }
        }
    }
}
