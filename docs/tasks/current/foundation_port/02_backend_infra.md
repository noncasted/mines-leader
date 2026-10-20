# 02. Backend-инфраструктура (B1-B13)

A = `/projects/peace-data-reborn`, B = этот репозиторий. Пути без префикса даны в B от `backend/`.

Инфраструктурные файлы B **побайтно совпадают** с A на старте этапа (`0a67a0b08^`); проверено diff-ом 2026-10-20.
Список: `OrleansUtils`, `Transactions*`, `GrainTransactionHandler`, `StateCollection*`, `DurableQueue`, `MessagePipe`, `RuntimeChannel`,
`DeployIdentity`, `DeployContext`, `DeploymentState`, `DeployHealthChecker`, `FileLoggerProvider`, `TelemetryPaths`, `MartenSetupExtensions`,
`StatesLookupGenerator`, `ServiceTag`, `StatesSetup`, `PostResourcesSetup`, `MetricsSnapshotService`.
Для них фикс переносится патчем коммита:

```bash
git -C /projects/peace-data-reborn diff <hash>^ <hash> -- <paths> | git -C /projects/mines-leader apply --3way
```

Патчи накладывать в порядке коммитов A. Хунки игровой специфики A, которой нет в B (Telegram, оружие, лобби A), выбросить.
Если файл в B отличается (`ClusterFeatures.cs`, `docker-compose.yaml`, `BackendConnectionMiddleware.cs`, `Aspire/Program.cs`,
`ProjectsSetupExtensions.cs`), переносить только хунки фикса вручную.

## B1. Транзакции: результат не теряется, ошибки после коммита

- **Приоритет:** P0. **Волна:** 1.
- **Источник в A:**
  - `bdf86c516` (F2.37): `Infrastructure/Orleans/Utils/OrleansUtils.cs`, `Transactions/TransactionsExtensions.cs`, `TransactionFailedException.cs` (новый), тест `Tools/Tests/State/TransactionRunResultTests.cs`;
  - `c42ea344f` (F2.2): `Transactions/Transactions.cs`, `GrainTransactionHandler.cs`, тест `Tools/Tests/State/TransactionConfirmFailureTests.cs`.
- **Проблема:**
  - `InTransaction(Func<Task>)` отбрасывает `TransactionResult`.
  - `Run<T>` возвращает значение из незакоммиченной транзакции.
  - Сбой `OnSuccess` после `CommitAsync` превращается в failure, и вызывающий код повторяет операцию.
  - Исключение участника не отпускает лок грейна.
- **Шаги:**
  1. Наложить патчи.
  2. Пройти все вызовы `InTransaction`/`Run` в B (`grep -rn "InTransaction\|\.Run<" backend --include=*.cs`): ошибки больше не глотаются; места, где вызывающий код полагался на тихий провал, вынести в handoff.
  3. Перенести тесты.
- **Готово:** тесты зеленые; полный `dotnet test` зеленый.

## B2. Мигратор: код выхода, схема, dead letters

- **Приоритет:** P0. **Волна:** 1.
- **Источник в A:**
  - `c42ea344f` (F2.1): `Tools/DeploySetup/PostResourcesSetup.cs`, `Program.cs`, тест `Tools/Tests/Deploy/PostResourcesSetupTests.cs`;
  - `d6d9ae33c` (F2.10): `StatesSetup.cs`, `StatesCleanup.cs`, тест `Deploy/StatesSchemaTests.cs`;
  - `bdf86c516` и `a1203e5d8`: `StatesDrop.cs`.
- **Проблема:**
  - После исчерпания попыток мигратор выходит с кодом 0. В compose стоит `service_completed_successfully`, поэтому силосы стартуют на сломанной схеме.
  - Создается дубль PK-индекса.
  - Cleanup и drop не трогают dead letters.
- **Готово:** при недоступной БД мигратор выходит с ненулевым кодом; тесты зеленые.

## B3. StateCollection: подписка до загрузки, fail-fast, потокобезопасность

- **Приоритет:** P1. **Волна:** 2.
- **Источник в A:** `c42ea344f` (F2.5): `Infrastructure/Data/Collections/StateCollection.cs`, `StateCollectionExtensions.cs`, `StateCollectionUtils.cs`, тест `Tools/Tests/State/StateCollectionSyncTests.cs`.
- **Проблема:**
  - Загрузка идет до подписки, поэтому обновления в этом промежутке теряются.
  - `catch` глушит ошибку загрузки.
  - Словарь не потокобезопасный.
- **Готово:** тест синхронизации зеленый.

## B4. Messaging: keep-alive, изоляция наблюдателей RuntimeChannel

- **Приоритет:** P1. **Волна:** 2.
- **Источник в A:**
  - `bdf86c516` (F2.39): `Infrastructure/Messaging/Queues/DurableQueue.cs`, `Pipes/MessagePipe.cs`, `MessagingGrainExtensions.cs`, тест `Tools/Tests/Messaging/MessagingKeepAliveTests.cs`;
  - `d6d9ae33c` (F2.9): `Messaging/Channels/RuntimeChannel.cs`, тест `Messaging/RuntimeChannelObserverIsolationTests.cs`.
- **Проблема:**
  - `DelayDeactivation` вызывается внутри `OnDeactivateAsync`, где он не работает. Грейны деактивируются вместе с наблюдателями, и доставка теряется.
  - Медленный наблюдатель `RuntimeChannel` тормозит всех.
- **Готово:** тесты зеленые.

## B5. Coordinator: остановка при провале heartbeat, мертвый хук

- **Приоритет:** P1. **Волна:** 2.
- **Источник в A:**
  - `d6d9ae33c` (F2.6): `Orchestration/Coordinator/DeployIdentity.cs`, `Cluster/Deploy/CoordinatorHealthOptions.cs`, тест `Tools/Tests/Cluster/DeployIdentityTests.cs`;
  - `ed0c64258` (F0.4): `Orchestration/Coordinator/ClusterCoordinator.cs`; там же мертвый `EntityBuilder` в `Game/Session/Objects/Entities`, проверить в B.
- **Проблема:** после трех провалов heartbeat процесс остается зомби (`lifetime.Terminate()` вместо `StopApplication()`).
- **Готово:** тест зеленый, сборка проходит.

## B6. Деплой-эпохи и DeploymentState

- **Приоритет:** P1. **Волна:** 2. Делать до B8.
- **Источник в A:**
  - `d6d9ae33c` (F2.8): `Cluster/Deploy/DeployContext.cs`, `DeploymentState.cs`, тест `Tools/Tests/Cluster/DeployContextTests.cs`;
  - `bdf86c516`: `DeploymentState.cs`;
  - `a1203e5d8` (F2.40): `DeploymentState.cs`, тест `Cluster/DeploymentStateUpdateTests.cs`.
- **Проблема:**
  - Подписчики эпохи копятся на родителе.
  - Ошибка подписчика только логируется.
  - `IsInitialized` не сбрасывается.
  - `OnUpdate` применяет значение без общего лока: отсюда `Collection was modified`, и старое значение может перетереть новое.
- **Готово:** тесты зеленые.

## B7. ClusterId / ServiceId

- **Приоритет:** P1, или P0 при общем Postgres с другими проектами (вопрос Q3 в overview). **Волна:** 2.
- **Источник в A:** `d6d9ae33c` (F2.11): `Orchestration/Extensions/OrleansSetupExtensions.cs`, `Aspire/Program.cs`, `Aspire/appsettings.json`, compose, тест `Tools/Tests/Deploy/OrleansClusterIdentityTests.cs`.
- **Проблема:** ClusterId и ServiceId не заданы, Orleans берет значения по умолчанию.
- **Шаги:**
  1. Задать ClusterId/ServiceId (`mines-leader` по умолчанию) из конфигурации.
  2. Прописать в compose и Aspire.
  3. Перенести тест.
- **Деплой:** смена ClusterId на проде означает новую membership-таблицу; описать в отчете.

## B8. DeployCleanup: ключи без десериализации, список из реестра

- **Приоритет:** P2. **Волна:** 3. После B6.
- **Источник в A:**
  - `bee588ac6` (F6.5): `Cluster/Deploy/DeployCleanup.cs`, `IDeployScopedState.cs`, `DeployState.cs`, тест `Tools/Tests/Cluster/DeployCleanupTests.cs`;
  - `42a971126`: `DeployCleanup.cs`, `Infrastructure/Orleans/State/StateStorage.cs`, `Direct/DirectStorage.cs` (`ReadAllKeys`).
- **Проблема:**
  - Список состояний захардкожен.
  - `Collect<ClusterFeaturesState>` десериализует строки других типов; в A это давало `JsonSerializationException`.
- **Готово:** тест cleanup зеленый.

## B9. ClusterFeatures: единый писатель

- **Приоритет:** P2. **Волна:** 3.
- **Источник в A:** `d6d9ae33c` (F2.7): `Cluster/State/ClusterFeatures.cs`, `Cluster/Deploy/DeployHealthChecker.cs`, `Console/Infrastructure/Features/Features.razor`, тесты `Tools/Tests/Cluster/ClusterFeaturesWriterTests.cs`, `DeployHealthCheckerTests.cs`.
- **Проблема:** ClusterFeatures пишут три писателя (HealthChecker, Coordinator, Console) и перетирают друг друга. `ClusterFeatures.cs` в B отличается от A (32 строки, свои фичи B), переносить вручную.
- **Готово:** тесты зеленые; переключение фичи в консоли не откатывается.

## B10. Мелочи инфраструктуры

- **Приоритет:** P2. **Волна:** 3.
- **FileLogger** (`d6d9ae33c`, F2.12, тест `Tools/Tests/Telemetry/FileLoggerProviderTests.cs`): пишет синхронно, глотает ошибки, включен в проде.
- **pgbouncer:** в `Tools/deploy/docker-compose.yaml:62` стоит `edoburu/pgbouncer:latest`, и в `Aspire/Startup/PgBouncerFactory.cs` тоже. Закрепить версию (в A `v1.26.0-p0`, без `suspend_timeout`, `pgbouncer.ini`).
- **TelemetryPaths** (`a1203e5d8`, `bdf86c516`): не находит `.git` как файл (worktree); `MetricsSnapshotService` без `.git` молча не пишет метрики.
- **Marten** (`a1203e5d8`, тест `State/MartenSetupTests.cs`): типы ищутся сканом AppDomain, нужно брать из реестра.
- **Benchmark-таблицы только с флагом** (`a1203e5d8`, `42a971126`): в B есть `Tools/Benchmarks`. Мигратор создает их таблицы всегда, нужно создавать только при включенных бенчмарках.
- **Готово:** перенесенные тесты зеленые, Aspire стартует.

## B11. Мертвый SessionFileLogger

- **Приоритет:** P3. **Волна:** 3.
- **Источник в A:** `030a4862d`: удален мертвый `Game/Session/Logging/SessionFileLogger.cs`. В B этот файл отличается от A на 161 строку. Проверить `grep`-ом, используется ли он в B; удалять только мертвый.
- Проекции meta и `ConnectedUsers` из того же коммита (F2.14) вынесены в задачу N3 (`06_network_session.md`).

## B12. Опционально: строковые роли, инкрементальный генератор, composition root

- **Приоритет:** P3. **Волна:** 3. Только по решению оркестратора или пользователя.
- **Источник в A:**
  - `bee588ac6` (F6.4, F6.9): `ServiceTag`, `StatesLookupGenerator` на `CompilationProvider`;
  - `42a971126`, `4e5b18dc7` (F6.1, F6.8): Game composition root и регистрация по ролям.
- В B такая же структура (есть Game). Перенос большой; ценность в разделении Game и Meta по ролям сервисов.

## B13. Тесты инфраструктуры из A

- **Приоритет:** P2. **Волна:** 3.
- Сверить, что тесты A из `Tools/Tests/{State,Cluster,Messaging,Deploy,Telemetry,Reactive,Security}`, относящиеся к перенесенным фиксам, есть в B и проходят.
- Перенести общие фикстуры: `OrleansTestClusterFixture.cs` в B отличается на 17 строк, переносить хунки.
- **Готово:** полный `dotnet test` зеленый; список тестов, пропускаемых без Docker, внести в отчет.
