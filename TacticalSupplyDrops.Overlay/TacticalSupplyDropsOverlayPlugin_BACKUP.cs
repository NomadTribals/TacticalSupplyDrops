
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
        private const string OrderFolderPath =
    @"F:\Games\SPT\BepInEx\config\TacticalSupplyDrops";

        private const string OrderRequestPath =
            @"F:\Games\SPT\BepInEx\config\TacticalSupplyDrops\order-request.json";

        private const string OrderResponsePath =
            @"F:\Games\SPT\BepInEx\config\TacticalSupplyDrops\order-response.json";
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
            font-size: 25px;
            font-weight: 900;
            letter-spacing: -2px;
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
                <div class='brand-mark'>TS</div>

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
        let selectedItems = {};

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

        function createInventoryCard(item) {
            const id = item.Id;
            const selected = selectedItems[id] !== undefined;

            const card = document.createElement('div');
            card.className = 'item-card' + (selected ? ' selected' : '');

            card.addEventListener('click', () => {
                toggleItemSelection(item);
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
            quantity.innerHTML = 'QTY: <strong>' +
                formatNumber(item.StackCount) + '</strong>';

            const dimensions = document.createElement('span');
            dimensions.textContent =
                (item.Width || 1) + ' × ' + (item.Height || 1);

            metadata.appendChild(quantity);
            metadata.appendChild(dimensions);

            information.appendChild(name);
            information.appendChild(template);
            information.appendChild(metadata);

            if (selected) {
                const tag = document.createElement('div');
                tag.className = 'item-selection-tag';
                tag.textContent = 'ASSIGNED TO MANIFEST';
                information.appendChild(tag);
            }

            card.appendChild(information);

            return card;
        }

        function displayItems() {
            const search = searchBox.value.trim().toLowerCase();

            const filteredItems = stashItems.filter(item => {
                const name = cleanItemName(item.Name).toLowerCase();
                const template = (item.TemplateId || '').toLowerCase();

                return name.includes(search) || template.includes(search);
            });

            itemList.innerHTML = '';

            if (filteredItems.length === 0) {
                const empty = document.createElement('div');
                empty.className = 'empty';
                empty.textContent = stashItems.length === 0
                    ? 'NO INVENTORY DATA AVAILABLE'
                    : 'NO MATCHING CARGO FOUND';

                itemList.appendChild(empty);
            } else {
                filteredItems.forEach(item => {
                    itemList.appendChild(createInventoryCard(item));
                });
            }

            itemCount.textContent =
                formatNumber(filteredItems.length) + ' ITEMS';

            displayManifest();
        }

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

                    if (!item) return;

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
            if (!data || !data.success || !Array.isArray(data.items)) {
                updateStatus('STASH DATA UNAVAILABLE');
                return;
            }

            stashItems = data.items;
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

// Receive responses sent from the C# plugin.
let orderPending = false;

function updateOrderStatus(message) {
    const statusElement = document.getElementById('order-status');

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

    const submitButton = document.getElementById('submit-order');

    if (submitButton) {
        submitButton.disabled = true;
    }

    updateOrderStatus('SENDING ORDER REQUEST...');

    window.overlay.send(
        'tsd-submit-order',
        JSON.stringify(payload)
    );
}

// Register the button listener after the page is ready.
const submitButton = document.getElementById('submit-order');

if (submitButton) {
    submitButton.addEventListener('click', submitSupplyOrder);
}

window.overlay.onOrderStatusReceived = function(message) {
    updateOrderStatus(message);

    if (message === 'ORDER REQUEST FAILED') {
        orderPending = false;

        const button = document.getElementById('submit-order');

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
        console.error('TSD could not parse the order response:', error);
        updateOrderStatus('INVALID CLIENT RESPONSE');
        orderPending = false;

        const button = document.getElementById('submit-order');
        if (button) button.disabled = false;

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
            console.error('TSD could not parse the server response:', error);
            updateOrderStatus('SERVER RESPONSE COULD NOT BE VERIFIED');
            orderPending = false;

            const button = document.getElementById('submit-order');
            if (button) button.disabled = false;

            return;
        }

        if (serverResult.err !== 0 || !serverResult.data) {
            console.error('TSD server returned an error:', serverResult);
            updateOrderStatus(
                serverResult.errmsg || 'ORDER SUBMISSION FAILED'
            );

        } else if (Array.isArray(serverResult.data.warnings) &&
                   serverResult.data.warnings.length > 0) {

            console.warn('TSD server warnings:', serverResult.data.warnings);
            updateOrderStatus('ORDER REJECTED — CHECK SERVER WARNINGS');

       } else if (Array.isArray(serverResult.data.warnings)) {

    updateOrderStatus('ORDER ACCEPTED BY SERVER');

    // Clear the submitted manifest.
    selectedItems = {};
    displayItems();

    // Request the updated stash from the Client plugin.
    if (window.overlay && window.overlay.send) {
        window.overlay.send(
            'tsd-refresh-stash',
            'refresh'
        );
    }
}
        } else {

            updateOrderStatus('SERVER RESPONSE COULD NOT BE VERIFIED');
        }
    }

    orderPending = false;

    const button = document.getElementById('submit-order');
    if (button) button.disabled = false;
};

        if (window.overlay && window.overlay.on) {
            window.overlay.on('stash', json => {
                try {
                    const data = typeof json === 'string'
                        ? JSON.parse(json)
                        : json;

                    loadStash(data);

                    // Send a test message to the C# plugin.
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
        }

        displayItems();
    </script>
</body>
</html>
            ");

            SendStashData();
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
