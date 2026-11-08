# Tactical Supply Drops 2.1.0

**TSD has evolved. Welcome to the next generation of private air logistics.**

Tactical Supply Drops has modernized its operations. The original standalone logistics interface has been retired and replaced by our proprietary **TSD Field Terminal**: a rugged tablet available from **Mechanic at loyalty level 1**, with a base price of **75,000 roubles** before normal trader discounts.

Your cargo, traders, and delivery controls are now together in one device. Prepare supplies before deploying, place orders during a raid, and request delivery when you need it.

**Flare signaling is no longer required.** Through the terminal's GPS link, TSD coordinates an aircraft delivery or a secure ground courier handoff, depending on the map. Carry the tablet, confirm your cargo, and let our logistics team handle the rest.

![Tactical Supply Drops 2.0 — TSD Field Terminal](https://raw.githubusercontent.com/NomadTribals/TacticalSupplyDrops/v2.1.0/images/TSD_2.0_Field_Terminal.png)

**Version:** 2.1.0 | **SPT:** 4.1.6 | **Author:** nomadtribals | **License:** MIT

## Features

### Your own TSD Field Terminal

- A custom tablet model and inventory icon, with a dedicated TSD operating interface.
- Purchase through **Mechanic LL1** for a base price of **₽75,000**.
- Open the tablet from the menus or during a raid. Carry it in your PMC inventory to access it in the field.
- Move and resize the tablet window to suit your screen.
- Scroll vertically and horizontally when a page exceeds the available tablet space, keeping Cargo and Delivery controls reachable in smaller windows.

### Five operational tabs

- **Cargo:** Search and browse your stash, navigate containers, select quantities, and build your cargo manifest. Review and confirm the delivery fee before submitting your order. Cargo supports a maximum delivery footprint of **60 inventory cells**, with delivery pricing affected by Fence reputation.
- **Delivery:** Authenticate the field link, confirm dispatch, and follow the terminal's communications as your aircraft or courier is tasked. The screen shows the selected delivery method; failed placement can be retried using the same accepted order without another order or delivery fee. New cargo directs you to the next manifest instead of leaving you on a previously dispatched order.
- **Procurement:** Browse vanilla and installed custom traders in their own sections. Purchase supplies for your next cargo manifest while retaining normal trader prices, loyalty and quest requirements, stock, and purchase limits. The tablet remembers the last trader and search.
- **Config:** Change the tablet's open/close key, toggle interface sounds, and choose a saved interface accent color from presets or a custom hex value.
- **Device Info:** View terminal identity, firmware, ownership information, and the installed mod version.

### Tablet controls and feedback

- Dedicated **brightness** buttons adjust the display for day or night use.
- **Volume** buttons control tablet audio, including clicks, tab tones, order feedback, and background device sounds.
- A **power button** controls the tablet's power sequence, followed by a personalised welcome using your active character name. Your name also appears in the header and Device Info.
- The small **activity LED** flashes while orders, purchases, trader stock and delivery dispatch are processing.
- **Unified F12 settings:** Find Controls, Appearance, Window, Ground Courier and Airdrop Release Settings under Tactical Supply Drops. Existing tablet settings are migrated automatically.
- **Interface colors:** Choose a preset or custom hex color in Config, then apply it. The setting is also available through F12 under Appearance > InterfaceColor. Restore Gold returns the original theme.
- **Firmware:** Device Info now shows TSD Field OS 0.4.0.
- Separate **RUB, USD, and EUR** balances, with currency labels on trader prices.
- **TSD-branded cargo crates** have charcoal colouring, orange markings and the tablet's gold insignia. The loot window identifies them as TSD Cargo Crate. Air cargo retains its orange smoke.
- **Ground courier delivery** supports Factory (day/night), Labs and both Ground Zero variants. After a short dispatch delay, TSD searches for a hidden, clear and reachable handoff location, at least 40 metres horizontally from the player. If none is found, move and retry the same order.
- Courier cargo is located through **tablet distance/direction/floor guidance**, a flashing orange beacon and a proximity beeper. The beacon stops when all cargo has been removed. Its volume can be adjusted up to 100% in F12.
- **Optional Dynamic Maps integration:** Courier crates use the airdrop icon with a TSD Courier Cargo label; the marker is removed when cargo is empty or the raid ends. Dynamic Maps' own visibility settings apply. Dynamic Maps is not required.

## Cargo previews

### TSD air cargo

![TSD-branded aircraft cargo with orange smoke](https://raw.githubusercontent.com/NomadTribals/TacticalSupplyDrops/v2.1.0/images/TSD_2.1_Air_Cargo.png)

### TSD ground courier cargo

![TSD courier cargo with flashing locator beacon](https://raw.githubusercontent.com/NomadTribals/TacticalSupplyDrops/v2.1.0/images/TSD_2.1_Ground_Cargo.png)
## How It Works

1. **Get connected.** Buy the TSD Field Terminal from Mechanic. Open it with **F9 by default**, or your configured key. You can change this in Config.
2. **Source your supplies.** Use Procurement to buy from a trader, or select supplies you already own on Cargo. Purchased goods are staged in your stash and selected in the cargo draft.
3. **Confirm the manifest.** Review Cargo and submit your supply order. The trader purchase and cargo delivery fee are separate transactions: buying supplies alone does not confirm a shipment.
4. **Take the terminal into a raid.** Orders can be prepared before or during a raid. Dispatch requires an active raid and the terminal in your PMC inventory.
5. **Request delivery.** Open Delivery and confirm cargo delivery. TSD selects the delivery route for the map: ground courier on Factory, Labs and Ground Zero; aircraft on other supported airdrop maps. No flare cartridge or flare gun is needed.
6. **Recover your shipment.** For air cargo, follow the aircraft and orange smoke. For courier cargo, follow tablet guidance, look for the flashing beacon and listen for its beeper; Dynamic Maps users can also follow the cargo marker. Open the crate and collect your supplies.
7. **Order again when needed.** Review and confirm your next cargo manifest, then dispatch the new accepted order. Each accepted order can be dispatched once.

**Custom traders:** Installed traders are discovered automatically. Their own currencies and normal restrictions apply. Specialized dogtag or functional-item barters marked **USE TRADER ITEM PICKER** must be completed through the game's normal trader screen.

## Requirements

- **SPT 4.1.6**.
- **[Anvil WebOverlay 1.11.0](https://github.com/maschine34675/WebOverlay/releases)**, installed separately.
- A **TSD Field Terminal** to access the service; carry it in your PMC inventory during a raid.
- A map with a supported airdrop point, or a supported courier map: Factory, Labs or Ground Zero.
- **Dynamic Maps 1.2.1 is optional** for courier markers; delivery, tablet guidance and beacons work without it. Changes to that mod's internal marker API may affect integration.

**Fika compatibility has not been confirmed.**

## Installation

1. Close the game and SPT server.
2. Install the required **Anvil WebOverlay** dependency according to its instructions.
3. Download **TacticalSupplyDrops-2.1.0.zip** from the [GitHub release](https://github.com/NomadTribals/TacticalSupplyDrops/releases/tag/v2.1.0).
4. Extract the archive into your **main SPT installation folder**, alongside `EscapeFromTarkov.exe`.
5. Merge the included **BepInEx** and **SPT_Runtime** folders and replace the existing Tactical Supply Drops files.
6. Start the server, then launch the game.

**Do not extract the archive directly into SPT_Runtime.** Both the client plugins and server files must be installed in their included locations. The Field Terminal and courier visual bundles are included in the archive.

**Upgrading:** Back up your profile first. Keep your existing configuration and order data. Your previously configured tablet key, appearance and window settings are preserved and moved into the main TSD configuration; a fresh installation defaults to F9. The previous overlay config is retained for rollback. Do not install old prototype GroundProbe plugins with this release.

## Coming Next

TSD is continuing to expand its field logistics service. Planned updates include:

- **Screen customization:** More ways to personalize the tablet's display and appearance.
- **Branded app home screen:** A TSD background and separate Logistics/Settings apps.
- **In-game clock:** Game time displayed on the tablet.
- **Handheld tablet animation:** Investigation into raising and lowering the tablet in first person. Additional app ideas remain exploratory.

These are planned features and are **not included in version 2.1.0**. Release timing will be announced as development progresses.

## Source and build

Build the server and client projects in Release mode using the .NET SDK and the matching SPT/game references. Build the overlay with Visual Studio MSBuild and the .NET Framework 4.7.2 targeting pack. Set the `SptRoot` MSBuild property to your SPT installation folder when building outside the original directory layout (for example, `/p:SptRoot=D:\SPT`). Debug builds deploy to that installation; Release builds do not deploy automatically.

The tested tablet bundle is included under `TacticalSupplyDrops.Server/bundles/`. The tested courier bundle is under `TacticalSupplyDrops.Client/Assets/`; its matching Unity 2022.3.43f1 source project is `TacticalSupplyDrops.CourierAssetSource/`. Unity asset source notes are in `TacticalSupplyDrops.AssetSource/README.md`.
