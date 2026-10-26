## Задача: миграция на исправления фреймворка после ревью 2026-10-26

### Цель
- Обновить сабмодуль `tools/framework` до коммита с исправлениями ревью и починить сборку игры.
- Привести код игры к изменённому API и поведению фреймворка (список ниже, по файлам).
- Проверить в Play и в тестах, что поведение игры не изменилось там, где оно не должно меняться.

### Контекст
- Полный список исправлений и пометки BREAKING: `tools/framework/docs/changes/2026-10-26-review-fixes.md`.
  Читать его перед началом: там же раздел «Не исправлено».
- Предусловие: изменения фреймворка закоммичены и запушены. На момент создания задачи они лежат в рабочем дереве
  `/projects/framework` незакоммиченными.
- Owner-проверки команд сессии не добавлялись: авторитет хоста сейчас не нужен.
- Протокол каналов Orleans получил новые поля (эпоха), интерфейсы грейнов изменились (`IDurableQueue.Push`,
  `IGrainTransactionHandler.Seal`): выкатывать backend целиком, не rolling-апдейтом смешанных версий.

### Шаги реализации

**1. Обновить сабмодуль и собрать**
  1.1. `git submodule update --remote tools/framework` (или на конкретный коммит), закоммитить указатель.
  1.2. Собрать backend (`dotnet build`), открыть Unity и дождаться компиляции; ошибки ниже ожидаемы.

**2. Ожидания updater завершаются отменой (самое важное)**
`RunDelay`, `RunUpdateAction`, `RunFixedAction` теперь бросают `OperationCanceledException`, когда lifetime кончается
раньше. Код после `await`, который раньше выполнялся «по завершении», теперь не выполнится. Для каждого места решить:
нужен ли код после `await` при отмене (тогда `try/finally` или `.SuppressCancellationThrow()`), или отмена правильна.
  2.1. `SetSpawning(false)` после ожидания — `client/Assets/GamePlay/Cards/Entities/States/Local/Spawn/CardLocalSpawn.cs:55`
  2.2. то же для удалённой карты — `client/Assets/GamePlay/Cards/Entities/States/Remote/Spawn/CardRemoteSpawn.cs:50`
  2.3. код после drag — `client/Assets/GamePlay/Cards/Entities/States/Local/Drag/CardLocalDrag.cs:127`
  2.4. `Show` возвращает результат после ожидания — `client/Assets/GamePlay/Cards/Entities/Components/DropAreas/CardDropArea.cs:53`
  2.5. восстановление `PointerHandler` после drag колоды — `client/Assets/Menu/Decks/MenuDecks.cs:160`
  2.6. — `client/Assets/GamePlay/Cards/Entities/Actions/Random/GameRandom.cs:117`
  2.7. проверка `IsTerminated` после `RunDelay` стала мёртвой (отмена бросает раньше) — `client/Assets/GamePlay/Services/FloatingText/GameFloatingText.cs:85`
  2.8. возвращают задачу вызывающему, проверить вызывающих —
       `client/Assets/GamePlay/Cards/Entities/States/Dropped/CardDropped.cs:39`,
       `client/Assets/GamePlay/Cards/Entities/States/Local/Drop/CardDropMotion.cs:41`,
       `client/Assets/GamePlay/Cards/Entities/States/Local/Stash/CardStashMotion.cs:29`
  2.9. без `await` (`CardLocalIdle.cs:55`, `CardRemoteIdle.cs:40`, `CardLocalDrag.cs:75`, `MenuPlay.cs:167`) — не затронуты,
       но `.Forget()` теперь получит отмену: убедиться, что она не логируется как ошибка.
  2.10. Timed `RunUpdateAction(lifetime, time, …)` теперь считает время updater: при паузе или замедлении updater
       анимации карт растягиваются (раньше шли по `Time.time`).

**3. Сессии backend**
  3.1. `await session.AllUsersConnected.WaitInvoke(session.Lifetime)` → `await session.AllUsersConnected` —
       `backend/Game/Global/SessionFactory.cs:188`, `backend/Game/Global/SessionFactory.cs:294`
  3.2. Владелец сессии больше не бот: бот создаётся до человека (`SessionFactory.cs:291`), проверить логику, которая
       опирается на `SessionOwner` (кто «ведёт» сессию в матче с ботом).
  3.3. `PlayersCount` листингов не считает ботов и дубли соединений — проверить UI/логику, которая на это опиралась.
  3.4. `OnSessionCreated` завершается после выполнения setup в очереди сессии: порядок
       `await serviceFactory.OnSessionCreated(...)` → `session.Run()` в `SessionFactory.cs` сохранить.

**4. Камера**
  4.1. `SetCamera(camera)` → `SetCamera(lifetime, camera)`: в `Enable()` lifetime нет, передать lifetime scope
       игровой сцены (например, получить его в `OnSetup` и сохранить) — `client/Assets/GamePlay/Services/GameCamera.cs:66`
  4.2. `ScreenToWorld` без текущей камеры теперь бросает (раньше `Vector3.zero`): убедиться, что ввод не опрашивается
       до установки камеры — `client/Assets/GamePlay/Services/GameInput.cs:60`

**5. Тесты backend игры (копии тестов фреймворка)**
  5.1. Конструктор `DeployCleanup` получил `IMessaging`, cleanup отказывается работать при чужом deploy id —
       `backend/Tools/Tests/Cluster/DeployCleanupTests.cs:121`
  5.2. Сообщение ошибки `DurableQueue` изменилось — `backend/Tools/Tests/Messaging/DurableQueueTests.cs:130`,
       `backend/Tools/Tests/Messaging/DurableQueueSubscriberTests.cs:60`
  5.3. Тесты, которые дублируют тесты фреймворка (каналы, транзакции, сессии), сверить с
       `tools/framework/backend/Tests`; где копия устарела, удалить её в пользу тестов фреймворка.

**6. Проверка поведения**
  6.1. Сцены: `FindOrLoadSceneWithServices` в моке больше не берёт объект из другой сцены и бросает —
       `client/Assets/GamePlay/Loop/GamePlayScopeExtensions.cs:123-126`, `client/Assets/Menu/Common/MenuScopeExtensions.cs:59-60`.
       Запустить каждую сцену напрямую (мок).
  6.2. DI: запустить игру полностью. Контейнер теперь бросает при сборке scope, если регистрацию не видит генератор
       (через интерфейс, в цикле, два `RegisterComponent(this)` одного типа на сцене) или устарел
       `ContainerGraph.AssetMap.cs`. В GamePlay 7 зависимостей от альтернатив теперь идут через `RequireAlternative`.
  6.3. Кнопки, подписанные через `ListenClick` до первого показа экрана, раньше молча не работали — теперь работают.
       Проверить, что не появились двойные обработчики там, где это обходили вручную.
  6.4. `SocketUrl` меняет только схему URL и бросает на не-http — `client/Assets/Meta/Connection/MetaBackend.cs:72`
  6.5. `Framework.json` обязателен (у проекта есть, `client/ProjectSettings/Framework.json`); генерация каталогов и
       bindings теперь бросает на дубли и зарезервированные имена — запустить Regenerate All.
  6.6. Синхронизация сетевых свойств и фейды музыки идут по реальному времени: пауза игры их не останавливает.

### Ключевые файлы

| Файл | Роль в задаче |
|------|---------------|
| `tools/framework/docs/changes/2026-10-26-review-fixes.md` | полный список изменений фреймворка |
| `client/Assets/GamePlay/Cards/Entities/States/**` | ожидания updater с кодом после `await` |
| `backend/Game/Global/SessionFactory.cs` | `AllUsersConnected`, порядок создания сессии, бот |
| `client/Assets/GamePlay/Services/GameCamera.cs` | `SetCamera(lifetime, camera)` |
| `backend/Tools/Tests/**` | копии тестов фреймворка под новый API |

### Документация к прочтению
- `.claude/docs/COMMON_LIFETIMES.md` — отмена ожиданий по lifetime, `SuppressCancellationThrow`
- `.claude/docs/COMMON_CONTAINER.md` — новая runtime-проверка регистраций и диагностики CINGR009-012
- `.claude/docs/COMMON_ORLEANS.md` — выкатка изменённых интерфейсов грейнов
- `.claude/docs/GAMEPLAY.md` — сессии с ботом и владелец сессии

### Риски
- Пункт 2 меняет поведение молча: компиляция пройдёт, но флаги вроде `SetSpawning(false)` перестанут сбрасываться
  при прерванной анимации. Пройти каждое место руками.
- Смешанные версии backend при выкатке: каналы и DurableQueue несовместимы со старыми силосами.
- `ViewableList` / `ViewableDictionary` больше не `List` / `Dictionary`: код, передающий их туда, где ждут `List<T>`,
  не скомпилируется (в игре таких мест не найдено).
