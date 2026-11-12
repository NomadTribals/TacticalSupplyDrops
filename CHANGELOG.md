# Tactical Supply Drops Logistics 3.1.0

- New **Drone Delivery** option for small loads of ammunition, medical supplies, food and drinks. A branded TSD drone approaches from a distance, lowers a compact equipment crate on a winch, releases it and departs.
- Drone cargo uses a 4 × 3 inventory with a 5 kg limit. Unsuitable items remain available through Standard Delivery.
- Delivery fees now use cargo weight instead of item value: Standard starts at ₽5,000 + ₽1,000/kg; Drone starts at ₽2,000 + ₽500/kg. Fence reputation adjustments still apply.
- Ammunition can be selected from the combined stash total, including rounds stored inside ammunition cases.
- Multiple confirmed cargo orders can share a delivery, subject to capacity. Each cargo order has its own confirmation and delivery fee.
- Factory and Labs offer a courier conversion for paid drone orders, crediting the drone fee toward the courier total before confirming the difference.
- Drone cargo includes location guidance and an optional Dynamic Maps marker after release. Dynamic Maps is not required.
- Branded drone and crate visuals, navigation lights, a handoff spotlight and positional rotor audio complete the delivery.

Special thanks to [Kedzie](https://sp-mod.com/user/5154/kedzie) for suggesting a smaller drone delivery option.

Requires TSD Technologies 1.0.0 or newer within 1.x and its dependencies. No Technologies update is required for Logistics 3.1.0.

# TSD Logistics 3.0.0

Tactical Supply Drops now runs as a separate Logistics app for TSD Technologies. Cargo, Procurement, Delivery and the activity log remain together, with aircraft/courier delivery, branded crates, beacon guidance and optional Dynamic Maps markers.

Requires TSD Technologies 1.x, Anvil WebOverlay 1.12.0 or newer, and SPT 4.1.6. Fika compatibility is unverified. Follow the clean-install guide and install both ZIPs into the SPT game root. Do not combine this setup with standalone Tactical Supply Drops 2.1.0.

Prefer the original standalone interface? Version 2.1.0 remains available and requires Anvil, but not TSD Technologies.

Special thanks to Anvil (maschine34675), author of Anvil WebOverlay, for the framework, direct support and improvements that made the tablet possible.

---

# Tactical Supply Drops 2.1.0 — Field OS 0.4.0

- Add ground courier deliveries on Factory day/night, Labs and both Ground Zero variants.
- Use hidden, clear, reachable handoff spots at least 40 metres from the player, with a wider map search and retry using the same accepted order when placement fails.
- Add tablet distance, direction and floor guidance, a flashing orange beacon and adjustable proximity beeper (up to 100% in F12).
- Add optional Dynamic Maps airdrop-icon markers for courier cargo, removed after collection or raid end. Dynamic Maps is not required.
- Add matching TSD charcoal/orange branding and gold insignia to ground and aircraft cargo; name TSD shipments TSD Cargo Crate.
- Add a personalised boot welcome, operator name in the interface and registered operator in Device Info.
- Add a data activity LED during orders, purchases, trader stock loading and delivery dispatch.
- Show the selected delivery route and personalised courier-ready confirmation.
- Remember the last Procurement trader and search.
- Combine all F12 settings under Tactical Supply Drops, preserving the existing tablet key, colours and window settings.

Requires SPT 4.1.6 and Anvil WebOverlay 1.11.0. Optional courier map integration was checked with Dynamic Maps 1.2.1. Fika compatibility remains unconfirmed.

Close the game and server, then extract the ZIP into the main SPT folder next to EscapeFromTarkov.exe. Replace the included TSD files and retain your configuration/order data. Both the courier visual bundle and the Field Terminal bundle are included. Do not install development GroundProbe plugins.

---

# Tactical Supply Drops 2.0.1 — Field OS 0.3.0

- Add configurable interface accent colors, with presets, custom hex colors, and a restore-default option on the tablet Config tab.
- Save the appearance choice in the plugin configuration; the F12 Appearance setting supports the same hex color.
- Update Device Info to TSD Field OS 0.3.0.
- Remove the obsolete flare-delay setting and its old flare timing patch.
- Fix Cargo and Delivery content extending below the tablet screen in smaller windows.
- Add vertical and horizontal scrolling when page content exceeds the available space.
- Keep Cargo inventory and manifest lists independently scrollable, with room to reach the order controls.
- Keep tablet navigation accessible when the available width is reduced.
- Preserve the existing purchase, payment, and delivery behavior.

Requires SPT 4.1.6 and Anvil WebOverlay 1.11.0. Close the game and server, then extract the ZIP into the main SPT folder and replace the included TSD files. Keep existing configuration and order data.

---

# Tactical Supply Drops 2.0.0

- New TSD Field Terminal tablet model, inventory icon, and tablet interface.
- Access the tablet from the main menus and during raids; dispatch deliveries during raids while carrying the terminal.
- Request delivery through the tablet without purchasing or firing a flare.
- Browse stock from installed vanilla and custom traders through separate Procurement sections.
- Native trader prices, unlocks, loyalty, stock, and purchase limits apply.
- Stage purchased supplies in the next cargo draft and review the delivery fee before confirming the manifest.
- Display separate RUB, USD, and EUR balances with explicit currency labels on trader prices.
- Guide repeat deliveries to the new cargo manifest instead of leaving the previous dispatched order as the only action.
- Set the terminal's base price to 75,000 roubles at Mechanic loyalty level 1.
- Add dispatch messages and orange smoke on TSD cargo crates.
- Fix terminal inventory placement and item icon loading.
- Resolve installation-specific file paths and use each profile's own stash root.

Requires SPT 4.1.6 and Anvil WebOverlay 1.11.0. See README.md for installation, upgrading, and the supported barter workflow.
