# Project Setup

Настройка сервисов кластера через `Common/ProjectsSetup/ProjectsSetupExtensions.cs`.

## Архитектура

- **Extension методы** на `IHostApplicationBuilder` для каждого типа сервиса
- **ServiceTag** определяет тип сервиса в кластере (Coordinator, Gateway, Game, Silo, Console)
- **AddBase()** — общая инфраструктура для всех сервисов

## Типы сервисов

| Метод | ServiceTag | Назначение |
|-------|------------|-------------|
| `SetupCoordinator()` | Coordinator | Координация кластера |
| `SetupBackendGateway()` | Gateway | Backend API, матчмейкинг |
| `SetupGameGateway()` | Game | Игровые сессии |
| `SetupSilo()` | Silo | Orleans хост |
| `SetupConsole()` | Console | Админ консоль |

## Структура Setup метода

```csharp
public IHostApplicationBuilder SetupXxx() {
    // 1. Basic services — Aspire defaults + Orleans client/silo
    builder
        .AddServiceDefaults()
        .AddOrleansClient(); // или .ConfigureSilo() для Silo

    // 2. Cluster services — общая инфраструктура
    builder
        .AddBase(ServiceTag.Xxx);

    // 3. Project services — специфичные сервисы проекта
    builder
        .AddProjectSpecificServices();

    return builder;
}
```

## AddBase() — что включает

```csharp
builder
    .AddEnvironment(serviceTag)    // IServiceEnvironment с ServiceId и Tag
    .AddStateAttributes()          // Маппинг атрибутов стейтов Orleans
    .AddServiceLoop()              // Lifecycle stages
    .AddMessaging()                // Message queues и pipes
    .AddOrleansUtils()             // Orleans утилиты
    .AddServiceDiscovery()         // Service discovery
    .AddConfigsServices()          // Конфиги
    .AddTaskScheduling()           // Планировщик задач
    .AddClusterFeatures();         // Cluster features

builder.Services.Add<DbSource>().As<IDbSource>();
```

## Использование в Program.cs

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.SetupBackendGateway();  // Один вызов настраивает всё

var app = builder.Build();
app.MapDefaultEndpoints();
app.Run();
```

## Ключевые файлы

- **Setup**: `Common/ProjectsSetup/ProjectsSetupExtensions.cs`
- **ServiceTag**: `Infrastructure/Discovery/Environment/ServiceTag.cs`
- **States**: `Common/Extensions/Orleans/States.cs`
