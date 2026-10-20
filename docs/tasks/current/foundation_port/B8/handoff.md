# B8 + B9. DeployCleanup, ClusterFeatures: единый писатель — handoff

Статус: review. Правки не закоммичены (worktree `agent-ab54fcb7851c49f05`, поверх main `d693e152`).
Источник: A `bee588ac6` (F6.5), `42a971126` (ReadAllKeys), `d6d9ae33c` (F2.7).

## Шаг 0: подтверждение

`DeployCleanup.cs`, `StateStorage.cs`, `DirectStorage.cs`, `DeployHealthChecker.cs` в B равны A `0a67a0b08^`
(в `DeployCleanup` нет только `LobbyRegistryState`). Временный тест на старом коде, оба упали:

- **B8:** `DeployCleanup.Run` при строке `cluster_features` в формате прода (`AddressableStateValue`) падает с
  `JsonSerializationException` в `StateSerializer.TryDeserialize` ← `DirectStorage.ReadAll` ← `Collect<ClusterFeaturesState>`.
  Cleanup в B не работал вообще, как только у ClusterFeatures появлялась строка. Список состояний захардкожен.
- **B9:** два экземпляра `ClusterFeatures` одного деплоя, параллельно `SetSideEffectsEnabled(true)` и
  `SetAcceptingConnections(true)`: после перезагрузки `SideEffectsEnabled == false` — read-modify-write из локальной
  копии теряет чужой флаг. Кроме того (чтением): `DeployHealthChecker` пишет `AcceptingConnections=false` при
  устаревшем heartbeat и `true` при смене эпохи, перетирая переключатель оператора в консоли; провал
  `DeployContext.Set` глушится общим `catch` (замечание B5).

## Что сделано

B8:
- `Cluster/Deploy/IDeployScopedState.cs` (новый): маркер состояния, ключуемого deployId.
- `DeployCleanup.cs`: список из `IGrainStatesRegistry` (`DeployScopedStates`, проверка Guid-ключа и direct-состояния),
  чтение только ключей. Финальная версия A `42a971126`.
- `StateStorage.cs`, `DirectStorage.cs`: `ReadAllKeys` (патч `42a971126`, лёг чисто).
- Маркер на `DeployState`, `ServiceDiscoveryStorageState`, `ClusterFeaturesState`.

B9:
- `ClusterFeatures.cs` (вручную, с флагами B `MatchmakingEnabled`, `SnapshotDiffGuardEnabled`): все изменения идут
  патчем `ClusterFeaturesPatch` через грейн `ClusterFeaturesWriter` (ключ deployId, нереентерабельный, читает строку
  на каждый вызов, `UpdateDate` строго растёт). `SetValue` бросает `NotSupportedException`. Новый флаг
  `CoordinatorUnhealthy` ([Id(4)]) и `IsAcceptingConnections = AcceptingConnections && !CoordinatorUnhealthy`.
  Мёртвая ветка `else` в `OnDeployChanged` удалена.
- `DeployHealthChecker.cs`: файл A `d6d9ae33c`. Публичный `Poll`; интервал и порог из `CoordinatorHealthOptions`;
  устаревший heartbeat ставит `CoordinatorUnhealthy`, свежий снимает (любой сервис); смена эпохи больше не открывает
  соединения; провал `DeployContext.Set` пробрасывается и роняет хост; провалы pipe/push логируются.
- `CoordinatorHealthOptions.cs`: `HealthPollInterval` (оставлен B5).
- `BackendConnectionMiddleware.cs`: проверка через `IsAcceptingConnections`.
- `Features.razor`: предупреждение при `CoordinatorUnhealthy`.

Тесты: `Tools/Tests/Cluster/DeployCleanupTests.cs`, `ClusterFeaturesWriterTests.cs`, `DeployHealthCheckerTests.cs`.

## Отличия от A

- `DeployCleanupTests`: нет `LobbyRegistryState`, `LiveSessionsData`, `ConnectedUsersLiveData` (в B их нет или они не
  зарегистрированы как GrainState), нет теста строковых ServiceTag (F6.4 не переносился) и теста
  `CreateDefaultStateIdentity` (не перенесён в B6). Проверки количества удалённых ослаблены до "старые удалены,
  текущий остался": БД общая для коллекции, там бывают строки других тестов. В `Run_OnlyCurrentDeploy` сначала
  холостой прогон по той же причине.
- `ClusterFeaturesWriterTests`: в параллельный тест добавлены флаги B (Matchmaking, SnapshotDiffGuard).
- `DeployHealthCheckerTests`: без изменений.
- `FeaturesStatusWidget.razor` не трогал (в A тоже не менялся).

## Доработка: строки DeploymentState в cleanup (по просьбе оркестратора)

Источник: A `bdf86c516` (`DeploymentState.CreateDefaultStateIdentity`) и `42a971126` (атрибуты `GrainState` на live data).
- `DeploymentState.cs`: `CreateStateIdentity` вызывает новый публичный статический `CreateDefaultStateIdentity`.
- `MatchmakingLiveData`, `LiveMatchesData`, `ConnectedUsersLiveData`: `[GrainState(Table = "cluster",
  State = "deployment_state_<Type>", Key = Guid)]`, `IDirectStateValue`, `IDeployScopedState`, `Version => 0`.
  Поля и формат не менялись. Slices из A (per-service live data) не переносились.
- `DeployCleanupTests`: три типа в списке deploy-scoped, тест `DeploymentStates_RegisteredName_IsTheDeploymentStateIdentity`
  (имя в реестре совпадает с тем, что пишет DeploymentState), строки трёх типов в `Run_RemovesOld...`.

## Остаётся

- Деплой: старые строки `cluster_features` без поля `CoordinatorUnhealthy` читаются как `false`, миграция не нужна.

## Проверка

- `dotnet build backend/backend.slnx`: 0 ошибок.
- Новые и связанные тесты (`DeployCleanupTests` 5/5 после доработки, `ClusterFeaturesWriterTests`, `DeployHealthCheckerTests`,
  `ClusterFeaturesStateSerializationTests`): 14/14.
- Полный прогон (после доработки): 1189/1191, падают 2 `Tests.Meta.UserDeckTests` (известные, B13). Пропущенных нет (Docker есть).
