# B1 + B2: handoff

Worktree: `/projects/mines-leader/.claude/worktrees/agent-aa7b7b145cfa9c208`, правки не закоммичены.
Источник: peace-data-reborn `c42ea344f`, `d6d9ae33c`, `bdf86c516`, `a1203e5d8` (только хунки B1/B2).

## Шаг 0: подтверждение

Все исходные файлы B (`OrleansUtils`, `Transactions`, `TransactionsExtensions`, `GrainTransactionHandler`,
`PostResourcesSetup`, `Program`, `StatesSetup`, `StatesCleanup`, `StatesDrop`) побайтно совпадают с A на `0a67a0b08^`.
Баги подтверждены чтением кода:

- B1:
  - `OrleansUtils.InTransaction(Func<Task>)` возвращал `Transactions.Run(action)` как `Task`, и `TransactionResult` терялся.
  - `Run<T>` не смотрел на результат `Process`. При провале коммита он возвращал значение из незакоммиченной транзакции.
    Для value-типов (`bool`, `Guid`) проверка `result == null` не срабатывает, и при исключении в action возвращался `default`.
  - `Transactions.Process` вызывал `OnSuccess` участников внутри того же `try`, что и `CommitAsync`. Исключение после
    коммита давало `RollbackAsync` + `Rollback` участников + `IsSuccess = false`, хотя данные уже записаны.
  - `GrainTransactionHandler.OnSuccess`: если `OnTransactionSuccess` у state бросал исключение, `_lock.Release()` не выполнялся.
- B2:
  - `PostResourcesSetup.Run` после 5 неудачных попыток только писал лог, и `Program` выходил с кодом 0.
    В compose у силосов стоит `condition: service_completed_successfully` (`Tools/deploy/docker-compose.yaml:122`).
  - `StatesSetup` создавал `primary key (...)` и вдобавок `CREATE INDEX ix_<table>` на те же колонки.
  - `StatesCleanup` и `StatesDrop` не трогали `side_effects_dead_letter`, хотя `SideEffectsSetup` эту таблицу создаёт.

## Что перенесено

Патчи наложены `git apply --3way` в порядке коммитов A, все применились чисто.

- `c42ea344f`: `Transactions.cs` (`Commit` + `ConfirmParticipants` после коммита), `GrainTransactionHandler.cs`
  (подтверждаются все участники, лок отпускается, затем `AggregateException`), `BackendMetrics.TransactionConfirmFailure`,
  `PostResourcesSetup.cs` (`RunWithRetries` бросает исключение после последней попытки), `Program.cs` (exit code 1).
  Тесты: `State/TransactionConfirmFailureTests.cs`, `Deploy/PostResourcesSetupTests.cs`.
- `d6d9ae33c`: `StatesSetup.cs` (без `ix_`, `DROP INDEX IF EXISTS ix_<table>` для существующих таблиц),
  `StatesCleanup.cs` (+dead letter), хунк `Fixtures/DatabaseFixture.cs` (тестовая схема без `ix_`), тест `Deploy/StatesSchemaTests.cs`.
- `bdf86c516`: `TransactionFailedException.cs` (новый), `TransactionsExtensions.cs` (`Run<T>` → `ThrowIfFailed`,
  `RunOrThrow`, `TransactionResult.ThrowIfFailed`), `OrleansUtils.InTransaction(Func<Task>)` → `RunOrThrow`,
  `StatesDrop.cs` (+dead letter), тест `Drop_RemovesEverySideEffectTable`, `State/TransactionRunResultTests.cs`.
- `a1203e5d8`: в `StatesDrop.cs` только комментарий.
- `Tests.csproj`: добавлена ссылка на `DeploySetup.csproj`.

## Шаг 2: вызовы `InTransaction` / `Run`

Теперь при провале транзакции бросают исключение: `InTransaction(Func<Task>)`, `InTransaction<T>`, `Transactions.Run<T>`.
Это `TransactionFailedException`, исходная ошибка лежит в `InnerException`.
`Transactions.Run(Func<Task>)` по-прежнему возвращает `TransactionResult`.

### Места, где код полагался на тихий провал

| Место | Было | Стало |
|---|---|---|
| `Meta/Matches/MatchFactory.cs:45, 80` (`match.Setup`) | провал Setup игнорировался: сессия создавалась, игрокам уходил `MatchSearchResultUpdate` | исключение. **Исправлено в `Matchmaking.Loop`** (см. ниже). `CreateWithBot` из `MatchmakingCommands` получает ошибку через диспетчер команд |
| `Game/GamePlay/Context/GameFlow.cs:65` (`match.Setup` на game-сервере) | провал игнорировался, матч шёл без состояния грейна | исключение. `MatchHandle.Process` его ловит и логирует, но матч не стартует и rematch-сообщение не уходит. Клиенты висят до своих таймаутов |
| `Game/GamePlay/Context/GameFlow.cs:87` (`OnComplete`, `InTransaction<T>`) | при провале коммита возвращался незакоммиченный `summary` | исключение. Снапшот конца игры не отправляется, `MatchHandle` логирует ошибку |
| `Orchestration/MetaGateway/UserFlow/AchievementCommands.cs:63` (`ClaimReward`, `bool`) | исключение в action давало `default(false)`, клиент получал "Reward is not available". При провале коммита приходил `true`/Ok, хотя награда не записана | исключение. `UserCommandsDispatcher` логирует его и шлёт клиенту ответ об ошибке |
| `Meta/Bots/BotFactory.cs:38` (`Create`, `Guid`) | исключение в action давало `Guid.Empty`. `ClusterBotsSetup` молча продолжал без бота | исключение из `OnServiceStarted` |
| `Meta/Bots/BotFactory.cs:59` (`AssignDeck`, `Run(Func<Task>)`) | `TransactionResult` отбрасывался (`Task<TransactionResult>` как `Task`) | **заменено на `RunOrThrow`** (одна строка, в A такого кода нет). `MatchFactory.CreateWithBot` больше не стартует матч, если колода бота не записалась |
| `Orchestration/MetaGateway/UserFlow/UserEntityCommands.cs:31` (`UpdateDeck`) | провал сохранения колоды отвечал клиенту Ok | исключение. Клиент получает ответ об ошибке через диспетчер |

### Matchmaking: провал создания матча (доработка по запросу оркестратора)

- В A аналога нет. Фонового цикла матчмейкинга там нет: сессия создаётся синхронно в команде (`Meta/Sessions/SessionRequester`),
  и ошибка уходит клиенту через диспетчер.
- В B цикл вызывал `_matchFactory.Create/CreateWithBot(...).NoAwait()`. `NoAwait` бросает исключение в continuation,
  ошибка становится unobserved, а игроки уже убраны из очереди.
- Явного провала нет: у `SharedMatchmaking.MatchResult` нет failure-варианта, это смена протокола (N4/N10).
  Готовый путь возврата в очередь есть: `SearchMatch`.
- Сделано: `Matchmaking.CreateOrRequeue` оборачивает создание матча. При исключении он пишет `LogError` с исключением
  и возвращает участников в очередь своего типа через `SearchMatch`. Для бот-матча возвращается только игрок.
- Ограничения:
  - Возврат в очередь ставит новый `JoinedAt`. Если сбой постоянный, попытка повторяется раз в `MatchmakingApplyThreshold`
    (бот-матч) или сразу (PvP-пара), и каждая попытка пишет лог. Клиент при этом остаётся в поиске.
  - Если игрок отменил поиск, пока матч создавался, он вернётся в очередь. Отключённых игроков цикл убирает сам.
  - Если сбой случился после отправки `MatchSearchResultUpdate` одному из двух участников, получивший тоже вернётся в очередь.
    Это возможно только при сбое `SendOneTimeProjection` второго участника.
- Тест: `Tests.Meta.MatchmakingTests.Loop_MatchCreationFails_LogsAndReturnsPlayersToQueue`. Первый `Create` бросает исключение,
  и тест проверяет второй `Create` для той же пары и `LogError` с исключением.

### Без изменения поведения (раньше бросали NRE "Result is null", теперь `TransactionFailedException`)

- `Game/GamePlay/Players/PlayerFactory.cs:34`
- `Meta/Users/Entities/UserFactory.cs:36, 49`: `BackendConnectionMiddleware` ловит исключение и отвечает 503
- `AchievementCommands.cs:36`
- `MatchCommands.cs:38, 64, 87, 105`

У этих вызовов есть одно отличие. Раньше, если action успевал вернуть значение, а коммит падал, значение возвращалось. Теперь бросается исключение.

### Уже проверяли результат

- `SideEffectsWorker.cs:128`: builder `.Run()` + `IsSuccess`.
- Бенчмарки `Tools/Benchmarks/State/*`: `_transactions.Run` + `IsSuccess`.
- `DurableQueueTransactionalStressTest.cs:95`: `InTransaction` внутри `try/catch`, ошибка теперь считается.
- `Tools/Tests/Grains/TestGrains.cs`: проверяют `IsSuccess`.

## Отличия от A

- В `Tests.csproj` добавлена только ссылка на `DeploySetup`. Ссылка на `Coordinator` нужна тестам других задач.
- Из `StatesSchemaTests` не перенесены тесты `a1203e5d8` про benchmark-таблицы (`DeployStates`): это задача B10.
- `BotFactory.AssignDeck` → `RunOrThrow`: в A этого кода нет.
- Документация агентов из A (`.claude/docs/CLAUDE_MISTAKES.md`, `COMMON_ORLEANS.md`) не перенесена.

## Проверка

- `dotnet build backend/backend.slnx`: успешно, новых предупреждений нет.
- Новые тесты (`TransactionRunResult`, `TransactionConfirmFailure`, `PostResourcesSetup`, `StatesSchema`): 14/14. Docker доступен, ничего не пропущено.
- Полный прогон: 1013 тестов, 1011 прошли, 2 упали, 0 пропущено (Docker доступен).
  - Упали `Tests.Meta.UserDeckTests.GetSelected_AfterChangingSelectedIndex_ReturnsDifferentDeck` и `Update_AllDecksAndSelectedIndex_Persisted`.
  - Они **падают и на чистом HEAD** `9d274c39` (проверено отдельной сборкой). С B1/B2 не связаны.
  - Причина: тестовый `OrleansTestClusterFixture` не вызывает `OrleansSetupExtensions.AllowSharedTypes`, и Orleans отвергает
    `Dictionary<int, IReadOnlyList<CardType>>`. В проде allow-list настроен. Это кандидат на B13 (фикстуры).
- После правки `BotFactory` выполнена пересборка и прогон `*Bot*`, `*Match*`, `*Transaction*`: 76/76.
