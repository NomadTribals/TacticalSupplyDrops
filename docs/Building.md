# Building from source

Use Visual Studio with the .NET desktop build tools and .NET 10 SDK. Supply your own SPT 4.1.6 installation with Anvil WebOverlay 1.12.0 or newer. Game and third-party binaries are not redistributed here.

Build Release with Visual Studio MSBuild, passing `/p:SptRoot="X:\your\SPT"` and `/restore`. Use the solution at the repository root. Release builds do not install into the game. The download ZIP contains the matching runtime files and custom asset bundles.

The TsdTechnologies projects in this repository are the matching 1.0.0 source snapshot used to build the addon. The independent tablet project and app API are maintained at https://github.com/NomadTribals/TSD-Technologies. Install the tablet separately; the Logistics ZIP does not redistribute its runtime DLLs.
