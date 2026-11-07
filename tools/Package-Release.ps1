param(
    [Parameter(Mandatory = $true)][string]$SptRoot,
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\release\2.0.0')
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot
$version = '2.0.0'
$SptRoot = (Resolve-Path -LiteralPath $SptRoot).Path
if (!(Test-Path -LiteralPath (Join-Path $SptRoot 'EscapeFromTarkov.exe'))) { throw 'SptRoot must point to the main SPT installation folder.' }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force $OutputDirectory | Out-Null
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$msbuild = & $vswhere -latest -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if (!$msbuild) { throw 'Visual Studio MSBuild was not found.' }
foreach ($component in @('Server', 'Client')) {
    & dotnet build (Join-Path $projectRoot "TacticalSupplyDrops.$component\TacticalSupplyDrops.$component.csproj") --configuration Release "-p:SptRoot=$SptRoot" --verbosity quiet
    if ($LASTEXITCODE -ne 0) { throw "$component build failed." }
}
& $msbuild (Join-Path $projectRoot 'TacticalSupplyDrops.Overlay\TacticalSupplyDrops.Overlay.csproj') /p:Configuration=Release "/p:SptRoot=$SptRoot" /v:quiet /nologo
if ($LASTEXITCODE -ne 0) { throw 'Overlay build failed.' }
$stage = Join-Path ([IO.Path]::GetTempPath()) ('TSD-Release-' + [Guid]::NewGuid().ToString('N'))
$clientFolder = Join-Path $stage 'BepInEx\plugins\TacticalSupplyDrops'
$serverFolder = Join-Path $stage 'SPT_Runtime\user\mods\TacticalSupplyDrops'
New-Item -ItemType Directory -Force $clientFolder, $serverFolder | Out-Null
Copy-Item -LiteralPath (Join-Path $projectRoot 'TacticalSupplyDrops.Client\bin\Release\netstandard2.1\TacticalSupplyDrops.Client.dll') -Destination $clientFolder
Copy-Item -LiteralPath (Join-Path $projectRoot 'TacticalSupplyDrops.Overlay\bin\Release\TacticalSupplyDrops.Overlay.dll') -Destination $clientFolder
Copy-Item -LiteralPath (Join-Path $projectRoot 'TacticalSupplyDrops.Server\bin\Release\net10.0\TacticalSupplyDrops.Server.dll') -Destination $serverFolder
Copy-Item -LiteralPath (Join-Path $projectRoot 'TacticalSupplyDrops.Server\bundles.json') -Destination $serverFolder
$manifest = Get-Content -Raw (Join-Path $serverFolder 'bundles.json') | ConvertFrom-Json
foreach ($entry in $manifest.manifest) {
    $relative = $entry.key.Replace('/', [IO.Path]::DirectorySeparatorChar)
    if ($relative.Contains('..') -or [IO.Path]::IsPathRooted($relative)) { throw 'Invalid bundle path in manifest.' }
    $source = Join-Path $projectRoot ('TacticalSupplyDrops.Server\bundles\' + $relative)
    $destination = Join-Path $serverFolder ('bundles\' + $relative)
    New-Item -ItemType Directory -Force (Split-Path $destination) | Out-Null
    Copy-Item -LiteralPath $source -Destination $destination
}
foreach ($document in @('README.md', 'CHANGELOG.md', 'LICENSE.md')) {
    Copy-Item -LiteralPath (Join-Path $projectRoot $document) -Destination $stage
}
$archive = Join-Path $OutputDirectory "TacticalSupplyDrops-$version.zip"
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $archive -Force
$hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $([IO.Path]::GetFileName($archive))" | Set-Content -LiteralPath (Join-Path $OutputDirectory 'SHA256SUMS.txt') -Encoding ascii
Write-Output "Created $archive"
