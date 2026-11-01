#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PROJECT="$ROOT/Generator~/Dialogue.SourceGenerator.csproj"
DLL="$ROOT/Generator~/bin/Release/netstandard2.0/Dialogue.SourceGenerator.dll"

if ! command -v dotnet >/dev/null 2>&1; then
    echo 'ERROR: .NET SDK not found. Install the SDK, not only the runtime.' >&2
    exit 1
fi
if [[ -z "$(dotnet --list-sdks)" ]]; then
    echo 'ERROR: dotnet is installed, but no .NET SDK is available.' >&2
    exit 1
fi

echo 'Restoring Source Generator dependencies...'
dotnet restore "$PROJECT"
echo 'Building Source Generator...'
dotnet build "$PROJECT" --configuration Release --no-restore

if [[ ! -f "$DLL" ]]; then
    echo "ERROR: DLL was not produced: $DLL" >&2
    exit 1
fi

mkdir -p "$ROOT/Analyzers"
cp "$DLL" "$ROOT/Analyzers/Dialogue.SourceGenerator.dll"
cat > "$ROOT/Analyzers/Dialogue.SourceGenerator.dll.meta" <<'META'
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
META

echo "Created: $ROOT/Analyzers/Dialogue.SourceGenerator.dll"
