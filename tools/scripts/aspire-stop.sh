#!/bin/bash
# Stop the Aspire cluster of this checkout. Other games on the framework run processes with the same names
# (Aspire.AppHost, Silo, MetaGateway, ...), so only the executables built under backend/Orchestration of this
# checkout are stopped; their `dotnet run` parents exit with them.
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
pkill -f "^$ROOT/backend/Orchestration/[^/]+/bin/" 2>/dev/null
echo "Aspire stopped"
