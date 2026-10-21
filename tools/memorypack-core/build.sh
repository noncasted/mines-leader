#!/usr/bin/env bash
# Собирает урезанный MemoryPack.Core и кладёт DLL в клиент вместо NuGet-версии.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
target="$root/../../client/Assets/Plugins/MemoryPack/MemoryPack.Core.1.21.4/lib/netstandard2.1/MemoryPack.Core.dll"

dotnet build "$root/MemoryPack.Core.csproj" -c Release -o "$root/obj/out"
cp "$root/obj/out/MemoryPack.Core.dll" "$target"
echo "Copied to $target"
