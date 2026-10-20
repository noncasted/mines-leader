# B5 + B6 + B7. Coordinator, деплой-эпохи, ClusterId — handoff

Статус: review. Правки не закоммичены (worktree `agent-a9e02c5bcecb51956`).
Источник: A `ed0c64258` (ClusterCoordinator), `d6d9ae33c` (DeployIdentity, CoordinatorHealthOptions, DeployContext,
DeploymentState, OrleansSetupExtensions/ClusterId), `bdf86c516` и `a1203e5d8` (только DeploymentState).

## Шаг 0: подтверждение

Файлы `DeployIdentity`, `CoordinatorHealthOptions`, `DeployContext`, `DeploymentState`, `Aspire/appsettings.json`
в B побайтно равны A `d6d9ae33c^`. `OrleansSetupExtensions` отличается только комментарием и `typeof(CardType)`.

- **B5, зомби-координатор:** `DeployIdentity.HeartbeatLoop` после 3 провалов делает `lifetime.Terminate()`. Процесс
  живёт без pipe-обработчика deploy id и heartbeat. Подтверждено чтением.
- **B5, мёртвый хук:** `ClusterCoordinator` реализует `IDeployAware`, но в `Coordinator/Program.cs` зарегистрирован
  только `.As<ILocalSetupCompleted>()`, поэтому `OnDeployChanged` никто не вызывает. Подтверждено чтением.
- **B5, EntityBuilder:** в B он **живой**. `PlayerFactory.cs:36` вызывает `_entityFactory.Create(user)`. Не удалялся.
- **B6:** тесты A, прогнанные на старом коде B, падают:
  - `DeployContextTests` 3/3: подписчики копятся на родителе, ошибка подписчика проглатывается, `IsInitialized` не сбрасывается;
  - `DeploymentStateUpdateTests` 2/2: ошибка десериализации проглатывается, конкурентные `Set` дают `Collection was modified` и старое значение поверх нового.
- **B7:** `OrleansClusterIdentityTests` на старом коде: 4 из 8 падают. Отсутствующие или пустые ClusterId/ServiceId
  не дают ошибки. Сам Orleans 10 читает секцию `Orleans:*` из конфигурации, поэтому тесты с заданными значениями
  проходят и до фикса. Прод сейчас без `Orleans__*`, значит работает на значениях Orleans по умолчанию:
  `ClusterId=default`, `ServiceId=default`.

## Что сделано

- `ClusterCoordinator.cs`: патч `ed0c64258`. Убран `IDeployAware` с мёртвым `OnDeployChanged`. B-специфичный
  `SetMatchmakingEnabled(true)` сохранён.
- `DeployIdentity.cs`: патч `d6d9ae33c`. После N провалов heartbeat вызывается `IHostApplicationLifetime.StopApplication()`.
  Интервал и порог берутся из опций.
- `CoordinatorHealthOptions.cs`: добавлены `HeartbeatInterval`, `MaxConsecutiveHeartbeatFailures`.
- `DeployContext.cs`: патч `d6d9ae33c`. Эпоха создаётся как `parentLifetime.Child()`. Ошибка подписчика завершает эпоху и пробрасывается.
- `DeploymentState.cs`: патчи `d6d9ae33c` → `bdf86c516` → `a1203e5d8`. Сброс `IsInitialized`, без глотания ошибок
  загрузки и десериализации, `_applyLock` вокруг применения значения.
- `OrleansSetupExtensions.cs`: хунки `d6d9ae33c` перенесены вручную. Добавлены `ReadClusterIdentity` (обязательные
  `Orleans:ClusterId` и `Orleans:ServiceId`) и `ClusterOptions` для клиента и silo. Localhost-кластеру передаются те же значения.
- `Aspire/Program.cs` (вручную): `SetupOrleansIdentity()` передаёт `Orleans__ClusterId/ServiceId` всем проектам.
  `Aspire/appsettings.json`: `dev`/`dev` (прежние значения по умолчанию у `UseLocalhostClustering`).
- `Tools/deploy/docker-compose.yaml` (поверх S1): в `x-service-env` добавлены
  `Orleans__ClusterId: ${ORLEANS_CLUSTER_ID:-mines-leader}` и `Orleans__ServiceId: ${ORLEANS_SERVICE_ID:-mines-leader}`.
  `.env.example`: закомментированные переменные с описанием.
- `Tests.csproj`: ссылка на `Coordinator.csproj` (нужна `DeployIdentityTests`; в A добавлена в `c42ea344f`).
- Тесты: `Cluster/DeployIdentityTests.cs`, `Cluster/DeployContextTests.cs`, `Cluster/DeploymentStateUpdateTests.cs`,
  `Deploy/OrleansClusterIdentityTests.cs`.

## Отличия от A

- `CoordinatorHealthOptions.HealthPollInterval` и правка `DeployHealthChecker` не перенесены, это B9 (F2.7).
- `DeploymentState.CreateDefaultStateIdentity` из `bdf86c516` не перенесён: он нужен только per-service live data A, в B их нет.
- В тестах `LiveSessionsData` (нет в B) заменён на `LiveMatchesData`, `Slices` → `Matches`.
- ClusterId/ServiceId по умолчанию в compose: `mines-leader` (в A `peace-data`).
- Мёртвый `EntityBuilder` не удалён: в B он используется (`PlayerFactory`).
- `DEPLOY.md` из `d6d9ae33c` не трогал (там стратегия миграций, не относится к задаче).

## Замечания для следующих задач

- `DeployHealthChecker.CheckDeployEpoch` (B) ловит любое исключение, включая провал `DeployContext.Set`, и пишет
  warning. Эпоха при этом завершена, и на следующем опросе `Set` повторится: `_deployLifetime.IsTerminated` даёт
  повтор. Процесс не падает. В A это решено в `DeployHealthChecker.Poll` (`d6d9ae33c`), задача B9.
- В `ClusterFeatures.OnDeployChanged` ветка `else` ("Failed to load state, using defaults") теперь мёртвая: база
  бросает исключение. Её уберёт B9 (ClusterFeatures переписывается).

## Проверка

- `dotnet build backend/backend.slnx`: успешно.
- Новые тесты: 14/14.
- Полный прогон: 1145/1147. Падают 2 `Tests.Meta.UserDeckTests`, они падали и до правок (B13). Пропущенных нет.

## Деплой B7 (прод жив)

- Сейчас прод работает с `ClusterId=default`, `ServiceId=default`. После деплоя без новых переменных будет `mines-leader`/`mines-leader`.
- **Membership.** Таблица `OrleansMembershipTable` та же, но строки ключуются по `DeploymentId = ClusterId`.
  Новый silo создаст новую версию членства для `mines-leader` и не увидит старый кластер `default`. Клиенты
  (coordinator, meta, game, console) с новым ClusterId не подключатся к старому silo.
- **Простой.** Нужен полный перезапуск всего стека, смешанный старый и новый набор контейнеров не работает. Обычный
  Coolify-деплой compose пересоздаёт все сервисы, и короткий простой у него есть и так (один silo, матчи в памяти).
  Отдельного окна не нужно, но частичный передеплой одного сервиса в этом выпуске недопустим.
- **Данные.** ServiceId в B ни во что не персистится: нет ADO.NET grain storage и reminders, состояния лежат в своих
  таблицах по ключам грейнов и deployId. Смена ServiceId данные не теряет.
- **Мусор.** Старые строки `DeploymentId='default'` в `orleansmembershiptable` и `orleansmembershipversiontable`
  остаются. Их можно удалить после деплоя:
  `DELETE FROM orleansmembershiptable WHERE deploymentid='default'; DELETE FROM orleansmembershipversiontable WHERE deploymentid='default';`
- **Coolify.** Обязательных новых переменных нет. Опционально `ORLEANS_CLUSTER_ID` и `ORLEANS_SERVICE_ID`. Если
  смена ClusterId сейчас нежелательна, задать оба равными `default`: кластер останется прежним.
