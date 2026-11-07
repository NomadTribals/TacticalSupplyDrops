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
