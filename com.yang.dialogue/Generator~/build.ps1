$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$project = Join-Path $PSScriptRoot 'Dialogue.SourceGenerator.csproj'
$analyzers = Join-Path $root 'Analyzers'
$dll = Join-Path $PSScriptRoot 'bin/Release/netstandard2.0/Dialogue.SourceGenerator.dll'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw '.NET SDK was not found. Install a .NET SDK and reopen the terminal (dotnet --list-sdks).'
}

$sdkList = @(dotnet --list-sdks)
if ($LASTEXITCODE -ne 0 -or $sdkList.Count -eq 0) {
    throw 'The dotnet command is available but no .NET SDK is installed. Install the SDK, not only the runtime.'
}

Write-Host 'Building Dialogue.SourceGenerator with:'
$sdkList | ForEach-Object { Write-Host "  $_" }
& dotnet restore $project
if ($LASTEXITCODE -ne 0) { throw 'NuGet restore failed. Check the errors above (network, proxy, or NuGet sources).' }

& dotnet build $project --configuration Release --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Source generator compilation failed. Check the compiler errors above.' }

if (-not (Test-Path -LiteralPath $dll)) {
    throw "Build completed but the generated DLL is missing: $dll"
}

New-Item -ItemType Directory -Force -Path $analyzers | Out-Null
Copy-Item -LiteralPath $dll -Destination (Join-Path $analyzers 'Dialogue.SourceGenerator.dll') -Force
$meta = @'
fileFormatVersion: 2
guid: 8fd745a0f82746829dc7f8a6b3bb471a
labels:
- RoslynAnalyzer
PluginImporter:
  externalObjects: {}
  serializedVersion: 2
  iconMap: {}
  executionOrder: {}
  defineConstraints: []
  isPreloaded: 0
  isOverridable: 0
  isExplicitlyReferenced: 0
  validateReferences: 1
  platformData:
  - first:
      Any:
    second:
      enabled: 0
      settings: {}
  - first:
      Editor: Editor
    second:
      enabled: 0
      settings:
        DefaultValueInitialized: true
'@
[System.IO.File]::WriteAllText((Join-Path $analyzers 'Dialogue.SourceGenerator.dll.meta'), $meta, [System.Text.UTF8Encoding]::new($false))
Write-Host "Created: $analyzers/Dialogue.SourceGenerator.dll"
