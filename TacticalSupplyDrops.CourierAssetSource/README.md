# TSD courier visual assets

Open this project with Unity 2022.3.43f1, matching the tested game runtime. Use BuildTsdCourierAssets.Build to build the caged beacon and gold insignia badge bundle. The build reopens the bundle and validates its prefabs, materials, shaders and absence of decorative colliders. Keep the built-in Unity modules in Packages/manifest.json enabled.

Output: Build/Courier/tsd_courier_visuals.bundle. Copy the validated bundle into TacticalSupplyDrops.Client/Assets/ before release packaging. This is asset source only; players receive the built bundle and do not need Unity installed. The native crate model and gameplay logic remain supplied by the game.
