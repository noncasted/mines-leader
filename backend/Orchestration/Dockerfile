# syntax=docker/dockerfile:1.7-labs
#
# Shared multi-stage Dockerfile for the five production .NET services.
# Optimised for Coolify/BuildKit:
#   - restore stage only copies .csproj / .slnx / Directory.*.props so that a code edit
#     does not invalidate the NuGet cache layer.
#   - publish-all stage publishes every service sequentially inside one stage — msbuild
#     incremental output is reused for shared projects, so each subsequent publish only
#     compiles its own entry-point csproj.
#   - runtime stages just copy their service's publish output, selected via ASSEMBLY_NAME.
#   - the monolith (all roles in one process, Orchestration/Monolith) has its own publish and runtime stages,
#     built only with `target: monolith` (docker-compose.monolith.yaml); the default target stays the services one.

ARG DOTNET_VERSION=10.0
# Pinned rather than floating on `latest`: 10.0.202 shipped a regression that dropped
# wwwroot/_framework/blazor.web.js from Web SDK projects published after the first one
# in publish-all (Blazor console got 404 on its own framework JS). 10.0.400 is verified
# clean for that, and its Roslyn (5.9) matches Microsoft.CodeAnalysis.CSharp in
# tools/framework/Directory.Packages.props — an older SDK silently disables Generators.dll
# (CS9057) and the build then fails on the code it should have generated.
ARG DOTNET_SDK_TAG=10.0.400

# Stage 1: restore. Cache key = project/graph metadata only.
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_SDK_TAG} AS restore
WORKDIR /src

# `--parents` preserves source directory layout so restore paths match the full repo.
COPY --parents \
    backend/backend.slnx \
    **/Directory.*.props \
    **/*.csproj \
    ./

RUN dotnet restore backend/Orchestration/Services/Silo/Silo.Service.csproj \
 && dotnet restore backend/Orchestration/Services/Coordinator/Coordinator.Service.csproj \
 && dotnet restore backend/Orchestration/Services/MetaGateway/MetaGateway.Service.csproj \
 && dotnet restore backend/Orchestration/Services/GameGateway/GameGateway.Service.csproj \
 && dotnet restore backend/Orchestration/Services/ConsoleGateway/ConsoleGateway.Service.csproj \
 && dotnet restore backend/Orchestration/Monolith/Monolith.csproj

# Stage 2: publish every service inside a single RUN so msbuild's incremental compilation
# reuses shared project outputs across services.
FROM restore AS publish-all
ARG CONFIGURATION=Release

COPY --link . .

RUN set -e; \
    for pair in \
      "backend/Orchestration/Services/Silo/Silo.Service.csproj:Silo" \
      "backend/Orchestration/Services/Coordinator/Coordinator.Service.csproj:Coordinator" \
      "backend/Orchestration/Services/MetaGateway/MetaGateway.Service.csproj:MetaGateway" \
      "backend/Orchestration/Services/GameGateway/GameGateway.Service.csproj:GameGateway" \
      "backend/Orchestration/Services/ConsoleGateway/ConsoleGateway.Service.csproj:ConsoleGateway"; do \
      proj=${pair%:*}; name=${pair##*:}; \
      # Wipe StaticWebAssets incremental tracking so each publish actually
      # copies wwwroot/_framework/* into its output. Without this, only the
      # first Web SDK service in the loop gets blazor.web.js/dotnet.js on disk
      # and every later one ends up with a manifest pointing at missing files
      # (404 at runtime in the Blazor console).
      find /src -path "*/obj/*" \( -name "staticwebassets*.json" -o -name "*.StaticWebAssets.Up2Date" \) -delete 2>/dev/null || true; \
      dotnet publish "$proj" \
        -c "${CONFIGURATION}" \
        -o "/app/out/${name}" \
        --no-restore \
        /p:UseAppHost=false; \
    done

# Monolith: one publish (the static web assets of the console go into the manifest of Monolith itself, so the
# wipe of the services loop is not needed), then a runtime that starts Monolith.dll with every role. Ports, content
# roots and the addresses of the console's peers come from the compose (Monolith:Hosts:<Tag>:Port).
FROM restore AS publish-monolith
ARG CONFIGURATION=Release

COPY --link . .

RUN dotnet publish backend/Orchestration/Monolith/Monolith.csproj \
      -c "${CONFIGURATION}" \
      -o /app/out/Monolith \
      --no-restore \
      /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS monolith

RUN apt-get update && \
    apt-get install -y --no-install-recommends curl && \
    rm -rf /var/lib/apt/lists/*

WORKDIR /app

COPY --from=publish-monolith /app/out/Monolith/ .

ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true
# The base image binds 8080 by default; each host gets its own --urls from the runner.
ENV ASPNETCORE_HTTP_PORTS=

# exec form without a shell: SIGTERM reaches the runner, which stops every host and exits 0.
ENTRYPOINT ["dotnet", "Monolith.dll"]

# Stage 3: lean runtime. Compose selects service via build-arg ASSEMBLY_NAME.
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS runtime
ARG ASSEMBLY_NAME

RUN apt-get update && \
    apt-get install -y --no-install-recommends curl && \
    rm -rf /var/lib/apt/lists/*

WORKDIR /app

RUN test -n "${ASSEMBLY_NAME}" || (echo "ASSEMBLY_NAME build-arg is required" && exit 1)

COPY --from=publish-all /app/out/${ASSEMBLY_NAME}/ .

ENV ASPNETCORE_URLS=http://0.0.0.0:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true

EXPOSE 8080

ENV TARGET_DLL=${ASSEMBLY_NAME}.dll
ENTRYPOINT ["sh", "-c", "exec dotnet $TARGET_DLL"]
