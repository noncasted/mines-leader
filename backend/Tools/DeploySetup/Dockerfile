# syntax=docker/dockerfile:1.7-labs
FROM mcr.microsoft.com/dotnet/sdk:10.0.201 AS build
WORKDIR /src

# Restore layer
COPY --parents \
    backend/backend.slnx \
    **/Directory.*.props \
    **/*.csproj \
    ./

RUN dotnet restore backend/Tools/DeploySetup/DeploySetup.csproj

COPY . .

RUN dotnet publish backend/Tools/DeploySetup/DeploySetup.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish/ .

# Entrypoint runs migrations then exits
ENTRYPOINT ["sh", "-c", "exec dotnet DeploySetup.dll \"$@\"", "--"]
