FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app

EXPOSE 8080
EXPOSE 8443

ENV ASPNETCORE_URLS=http://+:8080

ENV ConnectionStrings__postgres=${DB_CONNECTION_STRING}
ENV ConnectionStrings__otel=${OTEL_CONNECTION_STRING}
ENV BUILD_URL=$BUILD_URL
ENV SERVICE_NAME=BackendGateway
ENV SERVICE_PUBLIC_URL=${SERVICE_PUBLIC_URL}

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release

WORKDIR /src

COPY backend/ .
COPY shared/ .

RUN dotnet restore "Backend/Gateway/BackendGateway/BackendGateway.csproj"

COPY . .
WORKDIR "/src/backend/Backend/Gateway/BackendGateway"

RUN dotnet build "BackendGateway.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "BackendGateway.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Backend.Gateway.BackendGateway.dll"]
