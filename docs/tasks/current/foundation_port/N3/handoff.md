# N3: handoff

Статус: review. Правки не закоммичены (worktree `agent-aa57356bc591a5e81`, база `main` a1ae965a).

## Шаг 0: подтверждение

- `ConnectedUsers`: обычный `Dictionary`; `Lifetime.Listen` удаляет запись по `UserId`, а не по сессии. Тест-проба до фикса (`Add(first)`, `Remove(first)`, `Add(second)`, конец lifetime `first`) падал: `IsConnected` = false. Значит, при переподключении meta закрытие старой сессии удаляет новую, и матчмейкинг (`Matchmaking.Loop` → `IsConnected`) выкидывает юзера из очереди как "not connected". `Add` через `Dictionary.Add` бросал бы при гонке двух подключений.
- `UserConnectionEntryPoint`: проекции из хендшейка (`UserFactory.Resolve`) читаются до `ListenChannel`, обновление, опубликованное между чтением и подпиской, теряется (чтением кода).
- `SessionsCollection`: `Dictionary` читают middleware/поиск/эндпоинты, меняют очереди сессий; `PushLiveData` перебирает `Users` чужих сессий с любого потока. `Get` = `_entries[id]` вызывается в `SessionConnectionMiddleware` вне `try`: подключение к закрытой сессии падало `KeyNotFoundException`, клиент не получал ответа на auth (чтением кода).
- Там же: если `UserFactory.Create` бросит в `ExecutionQueue`, `completion` не завершается, middleware висит вечно.

## Что сделано

Файлы B отличаются от A, перенос вручную.

- `backend/Orchestration/MetaGateway/UserFlow/ConnectedUsers.cs` (как A `030a4862d`): `ConcurrentDictionary` + lock на изменение и push live data; `Add` возвращает заменённую сессию; `Remove` удаляет только ту же сессию; lifetime-колбэк зовёт `Remove(session)`.
- `backend/Orchestration/MetaGateway/UserFlow/UserConnectionEntryPoint.cs` (как A): `Add` → `ForceDisconnect` заменённой; `ProjectionStream` — подписка до чтения снапшота, буфер обновлений, на gap — повторный resync, при ошибке resync — disconnect. Снапшот перечитывается через `IUserProjectionsLoader` в транзакции, проекции хендшейка не отправляются.
- `backend/Game/Global/SessionsCollection.cs` (по A): `ConcurrentDictionary`, `Add` бросает на дубль, снапшоты id юзеров на сессию (live data не трогает чужие `Users`), `Get` заменён на `TryGet`.
- `backend/Orchestration/GameGateway/SessionConnectionMiddleware.cs`: `TryGet`; сессии нет → `SharedSessionAuth.Response { IsSuccess = false }` и close `PolicyViolation` (клиент `SessionConnection` бросает "Authentication failed"). Исключение в создании юзера завершает `completion`.

Тесты:
- `Tools/Tests/Meta/ConnectedUsersTests.cs`: +5 (из A + явный `Remove` старой сессии).
- `Tools/Tests/Meta/UserConnectionEntryPointTests.cs`: новый, из A (8 тестов; интеграционный, Orleans + Docker). Харнесс: у каждой сессии свой lifetime, `ForceDisconnect` его завершает. Тест переподключения проверяет, что новая сессия осталась в `ConnectedUsers` после закрытия старой.
- `Tools/Tests/Meta/MatchmakingTests.cs`: `Loop_UserReconnectedDuringSearch_StaysQueuedAndMatched` — реальный `ConnectedUsers`, юзер переподключается во время поиска, матч создаётся с ним.
- `Tools/Tests/Network/SessionConnectionMiddlewareTests.cs`: новый, auth к неизвестной сессии → ответ `IsSuccess = false` и close.

## Проверка

- `dotnet build backend/backend.slnx`: успешно (только старые предупреждения).
- Новые/изменённые классы: 30/30.
- Полный прогон: 1148, падают 2 — `UserDeckTests.Update_AllDecksAndSelectedIndex_Persisted` и `GetSelected_AfterChangingSelectedIndex_ReturnsDifferentDeck` (B13, известно). Пропущенных нет (Docker доступен).

## Отличия от A

- B сохраняет в entry point превью карт и подписки на конфиги: `InitialCardPreviews` пишется сразу после первого снапшота (не при resync по gap), конфиги — после.
- Live data в B — `IDeploymentState.SetValue`, а не `IServiceSlicedState.SetSlice`.
- `SessionsCollection`: у B свой интерфейс (`gameMode`, `LiveMatchesData` с Player1/Player2); `TryGetCreatedAt` и `TimeProvider` из A не переносились.
- Middleware: в A отказ идёт через `SessionGate`/тикеты (S4) со `SessionAuthStatus`; здесь — минимально, через `IsSuccess = false` без смены протокола.
- `ISessionLogger`/`SessionFileLogger` не удалялись (B11).
- Не трогал: `SessionEndpoints` всё ещё перебирает `session.Users` чужой сессии с потока запроса (консольные эндпоинты, вне задачи).
