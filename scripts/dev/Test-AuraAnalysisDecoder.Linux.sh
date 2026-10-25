#!/usr/bin/env bash
set -euo pipefail

# Invoke inside a disposable .NET SDK container with the repository mounted read-only at /input.
# Copy source only: Windows bin/obj must not enter the Linux test build.
if [[ ! -f /input/AURA.sln || ! -f /input/src/services/AURA.Analysis.Api.Tests/AURA.Analysis.Api.Tests.csproj ]]; then
    printf '%s\n' 'STOP: Mount the repository read-only at /input.' >&2
    exit 1
fi
if [[ -w /input/AURA.sln ]]; then
    printf '%s\n' 'STOP: The input mount must be read-only.' >&2
    exit 1
fi
work="$(mktemp -d /tmp/aura-analysis-decoder-XXXXXXXX)"
trap 'rm -rf -- "$work"' EXIT

tar -C /input --exclude='*/bin' --exclude='*/obj' -cf - \
    src/services/AURA.Analysis.Api src/services/AURA.Analysis.Api.Tests | tar -C "$work" -xf -
for config in Directory.Build.props Directory.Build.targets Directory.Packages.props global.json NuGet.Config nuget.config; do
    if [[ -f "/input/$config" ]]; then cp -- "/input/$config" "$work/$config"; fi
done
cd "$work"
dotnet test src/services/AURA.Analysis.Api.Tests/AURA.Analysis.Api.Tests.csproj --configuration Release --nologo
printf '%s\n' 'RESULT: Analysis xUnit checks passed on Linux, including native JPEG/PNG decoding.'
