# Tiny Town Mini Mart

A compact, playable **Unity 6.5.10f1** mini-mart management prototype. It is designed as an original colorful low-poly toy world, created entirely from Unity primitives and runtime materials—no paid assets or external packs are required.

## Playable loop

The prototype begins with a small stocked store, a tiny controllable manager character, three active customers, and a further customer-spawn loop. The player picks up a product box from storage, restocks its matching shelf, watches customers select products and queue, earns money from checkouts, and purchases three simple upgrades.

| System | Prototype behavior |
|---|---|
| Movement | WASD movement with smooth angled top-down camera following. |
| Interaction | Press **E** near a storage box to carry its product, a matching shelf to restock it, or a colored upgrade station to purchase it. |
| Customers | Toy-like customer variants enter, choose a stocked shelf, take a product, queue, pay, and leave. |
| Checkout | The first customer in line is automatically processed and adds the product sale value to the store balance. |
| Upgrades | Cyan: extra shelf; pink: customer capacity; yellow: higher sale bonus. |
| Persistence | Current money and purchased upgrades are stored in `PlayerPrefs`. |
| Visuals | Runtime-built low-poly shop, products, props, rounded characters, pastel materials, soft directional lighting, and an orthographic isometric-style camera. |

## Run the prototype

Open the project using **Unity 6000.5.10f1**. Open `Assets/Scenes/SampleScene.unity` and press **Play**. The game bootstraps from `Assets/Scripts/MiniMartPrototype.cs` automatically when Play mode starts.

> The original starter scene remains intentionally lightweight. All playable world objects are created by the runtime bootstrap so the prototype is self-contained in one source file and can be iterated rapidly.

## Controls

| Key | Action |
|---|---|
| `W` `A` `S` `D` | Move the mini-mart manager. |
| `E` | Interact with a nearby storage box, shelf, upgrade station, or checkout. |
| `Esc` | Pause or resume the prototype. |

## Core script map

`MiniMartPrototype.cs` contains modular components rather than a single monolithic update loop:

| Component | Responsibility |
|---|---|
| `MiniMartGameManager` | Runtime world setup, money, materials, customer spawns, save/load, and system registry. |
| `PlayerShopper` | WASD movement, interaction selection, product carrying, and toy-character visual. |
| `ShelfUnit` / `StorageBox` | Product inventory, visible shelf items, and supply-box interaction. |
| `CustomerAgent` | Enter, shop, queue, pay, and leave state behavior. |
| `CheckoutStation` | Customer queue movement and automatic transactions. |
| `UpgradeStation` | Purchasable shelf, capacity, and sale-bonus upgrades. |
| `CameraFollower` | Fixed-angle smooth follow camera. |
| `MiniMartUI` | Money, carry status, controls, and notifications. |

## Validation note

The repository contains the exact Unity 6.5.10f1 URP project version requested. The source has been statically checked for balanced braces and no unfinished implementation markers. Open it in Unity and enter Play mode to let the editor generate metadata, compile against the installed Unity packages, and validate the final scene interactively.
