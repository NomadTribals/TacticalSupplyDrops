# TSD Field Terminal asset source

The shipped 2.0.0 tablet bundle was built with Unity 2022.3 LTS and WTT-SDK-2022, then verified in SPT. The tested binary is stored at `TacticalSupplyDrops.Server/bundles/assets/content/items/equipment/tsd_field_terminal/tsd_field_terminal.bundle`.

The `Assets/TsdFieldTerminal` folder contains the tablet model, materials, and the runtime loot prefab. `BuildTsdFieldTerminal.cs` generates the model. `BuildTsdTablet.cs` builds the runtime loot prefab with the required `AssetPoolObject`, `PreviewPivot`, and collider components.

To rebuild, import these assets into WTT-SDK-2022 using Unity 2022.3 LTS, add `TSD_WTT_SDK` to the project's scripting define symbols, and choose **TSD > Build Repaired Tablet Bundle**. The output is written to that Unity project's `Build/` directory. Copy the resulting bundle to the matching path under `TacticalSupplyDrops.Server/bundles/` before packaging a new release. The SDK and game assemblies are separate development dependencies and are not redistributed here.

Generating the model alone does not create a valid runtime loot bundle. Use the SDK build step and validate the result in the game before replacing the tested bundle.
