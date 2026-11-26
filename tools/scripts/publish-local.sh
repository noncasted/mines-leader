#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/../.."

rm -rf publish
mkdir -p publish

publish() {
    local name=$1
    local csproj=$2
    echo "==> Publishing $name"
    dotnet publish "$csproj" -c Release -o "publish/$name" /p:UseAppHost=false --nologo -v q
}

publish Silo           backend/Orchestration/Services/Silo/Silo.Service.csproj
publish Coordinator    backend/Orchestration/Services/Coordinator/Coordinator.Service.csproj
publish MetaGateway    backend/Orchestration/Services/MetaGateway/MetaGateway.Service.csproj
publish GameGateway    backend/Orchestration/Services/GameGateway/GameGateway.Service.csproj
publish ConsoleGateway backend/Orchestration/Services/ConsoleGateway/ConsoleGateway.Service.csproj
publish Monolith       backend/Orchestration/Monolith/Monolith.csproj
publish Aspire.AppHost backend/Orchestration/Aspire/Aspire.csproj

echo
echo "Done. Artefacts in ./publish/"
du -sh publish/*
