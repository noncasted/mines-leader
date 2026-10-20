# R1. Backend Lifetime/Reactive — handoff

Статус: review. Правки не закоммичены (worktree `agent-ad475a66608ab63d8`).

## Шаг 0: подтверждение

- `backend/Common/Reactive` в B побайтно совпадает с A `0a67a0b08^` (`diff -r` пустой).
- Тесты A (`LifetimeTests`, `LifetimeBoundsTests`, `ReactiveCollectionsTests`, 41 тест) прогнаны на старом коде B:
  **25 падают**, из них 2 — stack overflow (рекурсия перебора `ViewableDictionary` при TSource == TView).
  Подтверждены все пункты списка фиксов:

| Фикс | Падающий тест на старом коде |
|---|---|
| потокобезопасный Lifetime | `Terminate_RacingWithListenAndToken_LosesNothing` (797 из 800) |
| `Listen(null)` падает сразу | `Listen_Null_Throws`, `TerminatedLifetime_ListenNull_Throws` |
| Token после Terminate отменён | `Token_RequestedAfterTerminate_IsCancelled` |
| утечка в Intersect | `Intersect_TerminatedDirectly_LeavesBothSources`, `Intersect_WithTerminatedSource_...` |
| TerminatedLifetime | `TerminatedLifetime_Listen_InvokesImmediately`, `_Token_IsCancelled`, `_Advise_DoesNotDeliver`, `_Child_IsTerminated` |
| bounding во View | `PropertyView_*`, `PropertyViewNotNull_*`, `ListView_EntryLifetime_EndsWithSubscriber_*` |
| Dictionary: рекурсия, Remove, Dispose | `DictionaryView_EntryLifetime_*` и `Dictionary_Advise_SeesExistingAndAdded` (stack overflow), `Dictionary_RemovedKeyAddedAgain_*`, `Dictionary_Dispose_EndsEveryEntryLifetime` |
| List: Remove, Clear, после Dispose | `List_RemovedEntryAddedAgain_*`, `List_Clear_EmptiesAndEndsEveryEntry`, `List_AfterDispose_AddReturnsTerminatedLifetime_ListenRunsAtOnce` |
| равный Set не уведомляет | `Property_SetEqual_DoesNotNotify_KeepsValueLifetime` |
| EventSource по записи, Advise(null) | `EventSource_SameHandlerTwice_EndOfLaterLifetimeKeepsOrder`, `EventSource_AdviseNull_Throws` |

## Что сделано

Патчи A `1f2a53f69`, `4eaf0cb1c`, `3bdde2d39`, `16297eeeb` (в порядке коммитов) по путям `backend/Common/Reactive` и
`backend/Tools/Tests/Reactive` наложены `git apply --3way` без конфликтов. Результат побайтно равен A HEAD по обоим путям.

Изменённые файлы:
- `backend/Common/Reactive/Lifetimes/Lifetime.cs`, `LifetimeExtensions.cs`, `TerminatedLifetime.cs`
- `backend/Common/Reactive/Events/EventSource.cs`, `LifetimedValue.cs`, `LifetimedValueExtensions.cs`
- `backend/Common/Reactive/DataTypes/Dictionaries/ViewableDictionary.cs`, `ViewableDictionaryExtensions.cs`
- `backend/Common/Reactive/DataTypes/Lists/ViewableList.cs`, `ViewableListExtensions.cs`
- новые: `backend/Tools/Tests/Reactive/LifetimeTests.cs`, `LifetimeBoundsTests.cs`, `ReactiveCollectionsTests.cs`

Вызывающий код бэкенда не менялся: аудит не нашёл мест, где нужен фикс (ниже).

## Аудит View-bounding и смены поведения

Поиск: `.View(`, `ViewNotNull(`, `AdviseTrue/False(`, `TerminatedLifetime`, `.Intersect(`, наследники `LifetimedValue`
(`ViewableProperty`, `DeploymentState`, `AddressableState`), `ViewableList`/`ViewableDictionary` во всём `backend`
(кроме самого Reactive и его тестов).

| Место | Вызов | Затронут? | Вывод |
|---|---|---|---|
| `Game/GamePlay/Bot/BotRunner.cs:50` | `_round.CurrentPlayer.ViewNotNull(user.Lifetime, (roundLifetime, player) => ... ExecuteTurn(roundLifetime))` | да, `roundLifetime` теперь ∩ `user.Lifetime` | Ход бота отменяется и при уходе бота, не только при смене хода. Дольше подписки lifetime не нужен. Правок нет |
| `Game/Global/SessionsCollection.cs:38` | `session.Users.View(session.Lifetime, user => ...)` | нет (перегрузка без lifetime значения) | `user.Lifetime.Listen(PushLiveData)` — lifetime самого пользователя, не View. Правок нет |
| `Game/Session/Root/Session.cs:74` | `_users.View(Lifetime, user => HandleUser(user))` | нет (перегрузка без lifetime) | Правок нет |
| `Orchestration/MetaGateway/UserFlow/UserConnectionEntryPoint.cs:84-86` | `_cardConfigs/_achievementConfig/_matchMakingConfig.View(user.Lifetime, value => ...)` | нет (перегрузка без lifetime) | Правок нет |
| `Tools/Tests/State/AddressableStateTests.cs:29,78` | `state.View(lifetime, value => ...)` | нет | Тесты зелёные |
| `Game/Session/Objects/Services/Service.cs:24` | `_lifetime = new TerminatedLifetime()` до `Setup` | да, `Listen` на нём теперь сразу вызывает callback | `SessionObjects.Add` слушает `obj.Lifetime`, но `ServiceFactory` (строки 38, 66) вызывает `Setup` до `Add`. В A из-за этого правили тест `SessionObjects_SecondObjectWithTakenId_Throws`; в B такого теста нет. Правок нет |
| `Common/Reactive/DataTypes/Lists/ViewableList.cs:31` | `Add` после `Dispose` отдаёт `TerminatedLifetime` | да | Единственный наследник — `SessionUsers.AddUser`; после `Dispose` `Remove` ничего не делает. Правок нет |
| `LastManStandingRound`, `LastManStandingTurnBasedRound`, `TimeLimitedRound` | `_currentPlayer.Set(...)` | равный `Set` больше не уведомляет | Следующий игрок всегда `players.First(t => t != _currentPlayer.Value)`, повторного `Set` того же игрока нет. Правок нет |
| `DeploymentState`, `AddressableState` | `Set(value)` | равный `Set` | Значения — классы без переопределённого `Equals`, новый объект на каждое чтение: уведомление сохраняется. Правок нет |
| `LobbyService`, `MatchListingService` (правились в A) | — | — | В B их нет; 2-аргументных `View` по `ViewableList`/`ViewableDictionary` в бэкенде B нет |
| `.Intersect(` | — | — | Вне Reactive не используется |

## Проверка

- `dotnet build backend/backend.slnx`: 0 ошибок.
- `Tests --filter-namespace Tests.Reactive`: 41/41.
- Полный прогон `backend/Tools/Tests/bin/Debug/net10.0/Tests`: 1038/1040, пропущенных нет. Падают 2 известных
  `Tests.Meta.UserDeckTests` (`Update_AllDecksAndSelectedIndex_Persisted`,
  `GetSelected_AfterChangingSelectedIndex_ReturnsDifferentDeck`), падают и на чистом HEAD — B13.

## Отличия от A

- Код Reactive и тесты — без отличий (равны A HEAD).
- Не перенесены правки вызывающего кода A (`LobbyService`, `MatchListingService`, `SessionObjectsTests`): в B нет этих
  сервисов и теста, аудит других мест не нашёл.
- Документы `.claude/docs/*` из патчей A не переносились (в B агентская документация тогда лежала в `.agents/`).
