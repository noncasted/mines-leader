## Задача: TransactionOptions — вынос таймаутов транзакций в конфиг

### Цель
Вынести хардкоженные константы из `GrainTransactionHandler` (`3s` таймаут ожидания лока и `30s` grace period) в `TransactionOptions`, который хранится в БД и загружается через тот же механизм, что `SideEffectsOptions` / `DurableQueueOptions`.

### Шаги реализации

1. Создать `TransactionOptions` + `ITransactionConfig` — `backend/Infrastructure/Orleans/Transactions/TransactionOptions.cs` [новый файл — добавить в `Infrastructure.csproj`]
2. Добавить `TransactionConfigState` — `backend/Cluster/Configs/InfrastructureConfigsState.cs`
3. Зарегистрировать `AddAddressableState<TransactionConfigState>().As<ITransactionConfig>()` — `backend/Cluster/Configs/ConfigsExtensions.cs`
4. Добавить `StatesLookup.TransactionConfig` + добавить в `All` — `backend/Common/Lookups/StatesLookup.cs`
5. Добавить `Add<TransactionOptions>(StatesLookup.TransactionConfig)` — `backend/Orchestration/Extensions/ProjectsSetupExtensions.cs`
6. Добавить `ITransactionConfig` в конструктор и `InitializeConfig("config.transaction", ...)` — `backend/Cluster/Configs/ClusterConfigsSetup.cs`
7. Инжектировать `ITransactionConfig` в `GrainTransactionHandler`, заменить магические числа на значения из опций — `backend/Infrastructure/Orleans/Transactions/GrainTransactionHandler.cs`
8. Создать дефолтный `config.transaction.json` в папке рядом с остальными конфигами

### Ключевые файлы

| Файл | Роль в задаче |
|------|---------------|
| `backend/Infrastructure/Orleans/Transactions/GrainTransactionHandler.cs` | Содержит `3f` и `30` — заменяем на поля из опций |
| `backend/Infrastructure/Orleans/SideEffects/SideEffectsOptions.cs` | Эталонный образец для нового `TransactionOptions` |
| `backend/Cluster/Configs/InfrastructureConfigsState.cs` | Сюда добавляем `TransactionConfigState` |
| `backend/Cluster/Configs/ConfigsExtensions.cs` | Регистрация через `AddAddressableState` |
| `backend/Cluster/Configs/ClusterConfigsSetup.cs` | Загрузка JSON при старте кластера |
| `backend/Common/Lookups/StatesLookup.cs` | Добавить `TransactionConfig` entry + в `All` |
| `backend/Orchestration/Extensions/ProjectsSetupExtensions.cs` | `Add<TransactionOptions>(StatesLookup.TransactionConfig)` |

### Документация к прочтению
- `rules/ORLEANS_STATE.md` — `IStateValue`, `[GenerateSerializer]`, `[Id(N)]` не нужны здесь (это не grain state, а `AddressableState`), но шаги добавления в `StatesLookup` идентичны

### Риски
- `GrainTransactionHandler` — это `IGrainExtension`, не обычный grain. Инжекция через конструктор стандартная (как у обычных грейнов), но убедись что `ITransactionConfig` резолвится в DI до того как грейны стартуют — это гарантирует `ICoordinatorSetupCompleted` в `ClusterConfigsSetup`, который вызывается раньше.
- `IAddressableState<T>` — это `ViewableProperty<T>`, значение может быть `null` до первой загрузки. В `GrainTransactionHandler` нужно читать через `Value` с fallback на дефолты (либо использовать `?.Value ?? defaultValue`).
