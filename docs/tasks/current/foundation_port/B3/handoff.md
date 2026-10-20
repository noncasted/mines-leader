# B3 + B4 handoff

Статус: review (правки в worktree, не закоммичены).

## Шаг 0: подтверждение

- Исходные версии всех файлов в B совпадают с A на родителях коммитов (`git apply --check` патчей A прошёл чисто).
- B3 (чтением кода): `StateCollection.OnLocalSetupCompleted` грузит снимок до `ListenUpdates` (окно потери обновлений), `catch` глушит ошибку загрузки, коллекция наследует `Dictionary` и пишется из колбэков очереди без синхронизации. `StateCollectionUtils.Load` тоже глушит ошибку и возвращает частичный словарь. Плюс открытая регистрация `IStateCollection<,>` -> `StateCollection<,>` давала вторую копию, которая никогда не загружается.
- B4 (падающими тестами): на старом коде из 5 новых тестов падают 4:
  `MessagingKeepAliveTests.DurableQueue_IdleCollection_KeepsObserver`, `RuntimePipe_IdleCollection_KeepsObserver`,
  `RuntimeChannelObserverIsolationTests.AttachedObserver_KeepsChannelActivation_OnCollection`, `SlowObserver_DoesNotDelayFastObserver`.
  Тест StateCollection на старом коде не компилируется (нужен новый API `Read`, `ListenUpdates(Func<TKey, Task>)`).

## Перенесено (патчи A, без ручных правок)

- `c42ea344f` (только StateCollection*): `backend/Infrastructure/Data/Collections/StateCollection.cs`, `StateCollectionExtensions.cs`, `StateCollectionUtils.cs`, тест `backend/Tools/Tests/State/StateCollectionSyncTests.cs`.
- `bdf86c516` (только messaging): `Messaging/Queues/DurableQueue.cs`, `Messaging/Pipes/MessagePipe.cs`, удалён `Messaging/MessagingGrainExtensions.cs`, тест `backend/Tools/Tests/Messaging/MessagingKeepAliveTests.cs`.
- `d6d9ae33c` (только RuntimeChannel): `Messaging/Channels/RuntimeChannel.cs`, тест `backend/Tools/Tests/Messaging/RuntimeChannelObserverIsolationTests.cs`.

## Проверка

- `dotnet build backend/backend.slnx` — ок.
- Новые тесты: 14/14.
- Полный прогон: всего 1148, упали 2 — известные `Tests.Meta.UserDeckTests` (B13), пропущено 0 (Docker доступен).

## Отличия от A

Нет: файлы побайтно равны A после соответствующих коммитов. Потребители в B (`UserCollection`, `BotCollection`) регистрируются через `AddStateCollection<...>().As<I...Collection>()` и правок не потребовали.
