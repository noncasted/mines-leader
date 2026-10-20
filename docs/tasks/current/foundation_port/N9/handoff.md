# N9. Поздние пользователи, гонки удаления — handoff

Источник: peace-data-reborn `1a6e81c99` (F2.13), только общие части. Лимит поиска и балансировщик (F2.15) не переносились. Service id через StableHash не переносился: это N8.

## Шаг 0: подтверждение

- Поздний пользователь: подтверждено тестом. `SessionRunTests.ExpectedUsers_UserJoiningAfterStart_IsHandled_OthersLearnAboutIt` на старом `Session.cs` падал по таймауту: пользователь, пришедший после старта матча с ExpectedUsers, не получал `LocalUpdate`. `HandleUser` для него не вызывался, сокет не читался и висел. Остальные 4 теста SessionRunTests проходили и на старом коде.
- Гонки удаления: подтверждено чтением кода. `SetPropertyCommand` и `EntityDestroyCommand` брали объект индексатором `Entries[id]`. Если сущность уже удалена (например, вместе с ушедшим владельцем), команда падала с `KeyNotFoundException`. Новые тесты на старом коде не компилируются: у команд изменились конструкторы.

## Изменения

- `backend/Game/Session/Root/Session.cs`: ожидание ExpectedUsers перенесено в очередь сессии, опрос раз в 100 мс заменён подпиской на `_users.View`. Таймаут JoinTimeout сохранён (в B он есть, в A его роль играет ScheduleEmptyCheck): он тоже решается в очереди, гонки со стартом нет. После старта `_users.View(Lifetime, HandleUser)`, так что пользователь, пришедший позже, тоже обрабатывается. Run завершается и при внешнем закрытии сессии до старта.
- `SessionObjects`: `IsRemoved(id)` для удалённых объектов; явная ошибка при занятом id вместо `ArgumentException` словаря.
- `SetPropertyCommand`, `EntityDestroyCommand`: запись или удаление уже удалённого объекта отбрасывается с Debug-логом. Для объекта, которого не было в сессии, и для неизвестного свойства — явное `InvalidOperationException`.
- `Entity.CreateOverview`: в overview передаётся Version свойства. Клиент B пока его не читает, правка безвредная.
- Тесты: новые `Tools/Tests/Game/SessionRunTests.cs` (5) и `Tools/Tests/Game/SessionObjectsTests.cs` (12). В `SessionCloseTests` классы `SessionWorld` и `ScriptedSocket` стали internal (их переиспользует SessionRunTests), а `ScriptedSocket` записывает отправленные контексты.

## Проверка

- `dotnet build backend/backend.slnx`: собирается.
- SessionRun, SessionObjects, SessionClose: 23/23, пять повторов SessionRun и SessionClose без падений.
- Полный прогон: 1192/1194. Падают 2 `Tests.Meta.UserDeckTests`, известная проблема B13. Пропущенных тестов нет.

## Отличия от A

- В B остался таймаут JoinTimeout с логом и закрытием сессии. A использует ScheduleEmptyCheck, которого в B нет.
- Перед стартом повторно проверяется `_users.Count`: если пользователь ушёл между подпиской и стартом, ждём следующего входа.
- Тесты SessionRun идут на реальной ExecutionQueue и ScriptedSocket из SessionCloseTests. В B нет ManualTimeProvider, TestSession и ручной очереди из A.
- Из SessionObjectsTests не перенесены тесты id сервисов (N8) и ServiceFactory. Тест сущности ушедшего владельца добавлен; session-сущностей без владельца в B нет.
