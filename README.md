# nomadtribals — Tactical Supply Drops

**Version:** 1.0.0  
**Author:** nomadtribals  
**Supported SPT Version:** 4.1.6  
**License:** MIT

## Overview

Tactical Supply Drops is a custom supply delivery system for SPT that allows players to purchase items from their stash and have them delivered into a raid by aircraft.

Select your cargo, submit your purchase, deploy a flare, and wait for your delivery.

**Download:** [Tactical Supply Drops v1.0.0](https://github.com/NomadTribals/TacticalSupplyDrops/releases/tag/v1.0.0)

## Features

- **Custom Logistics Interface:** Access the delivery system through a dedicated interface using F9.
- **Stash Integration:** Select equipment and supplies directly from your available stash.
- **Dynamic Delivery Pricing:** Fees are calculated based on the value of your selected cargo.
- **Fence Reputation Discounts:** Your standing with Fence directly affects delivery costs. Better reputation means better rates.
- **Procurement System:** Purchase flare cartridges before deploying into a raid.
- **Aerial Supply Deliveries:** Call in an aircraft to deliver your purchased cargo directly into the raid.
- **Cargo Recovery:** Retrieve your shipment from the delivered supply container.

## Requirements

- SPT 4.1.6
- BepInEx
- **[Anvil WebOverlay 1.11.0](https://github.com/maschine34675/WebOverlay/releases)** — required dependency

Anvil WebOverlay must be installed separately. Tactical Supply Drops will not function correctly without it.

## Installation

1. Close SPT and the SPT server.
2. Download the required Anvil WebOverlay dependency and install it according to its instructions.
3. Extract the Tactical Supply Drops archive into your main SPT installation folder.
4. Allow Windows to merge the included folders with your existing installation.
5. Launch the SPT server and game.

**Important:** Extract the archive into your main SPT installation folder, not directly into `SPT_Runtime`.

## Using Tactical Supply Drops

### 1. Open the Interface

Press **F9** to open the Tactical Supply Drops interface.

### 2. Select Your Cargo

Browse your available stash items and select the items you want delivered.

### 3. Submit Your Purchase

Review your selection and submit the purchase.

### 4. Prepare Your Flare

A flare cartridge is required to signal the delivery aircraft.

If you do not already have a flare cartridge, you can purchase one through the **Procurement** tab before entering a raid. Remember to bring it with you!

### 5. Call In Your Delivery

Once inside a raid, use a flare gun to deploy your flare cartridge and designate your delivery location.

### 6. Recover Your Cargo

Watch for the arriving supply aircraft. Once it drops your shipment, locate the delivery container and open it to collect your items.