## Отображение текущих модификаторов игрока — Рабочие заметки

### Статус: В работе (исправление runtime ошибок)

### Выполнено
- [x] Shared-контракты: IModifierOverview, DurationalModifierOverview, PlayerModifiersState, PlayerSnapshotRecord.ModifierUpdate
- [x] Backend ядро: Modifiers с Dictionary<Guid, IModifierSource>, IRoundAction.Tick(), RoundActionService, ModifierRoundAction
- [x] Backend карты: все Buff/Resource/CrossBoard карты переписаны на DurationModifierSource + ModifierRoundAction
- [x] Backend команды: OpenCellCommand с RemoveOne для Shield
- [x] Backend тесты: все 633 теста проходят (616 обновленных + 17 новых)
- [x] Client синхронизация: PlayerModifiers с ViewableList overviews, PlayerModifierSnapshotHandler
- [x] Client UI скрипты: ModifierDescriptionsConfig, PlayerModifierEntryView, PlayerModifierTooltipView, PlayerModifiersView, ModifierEntryPrefabDefinition
- [x] Client .csproj: новые файлы добавлены в GamePlay.csproj
- [x] ModifierDescriptions.json: создан с 15 ключами для всех карт
- [x] ModifierDescriptionsConfig обновлён: Entry теперь имеет поле Icon (Sprite)
- [x] PlayerModifiersView.GetIconForKey теперь читает иконку из конфига
- [x] Сцена Game_Overlay: через execute_code созданы GameObject'ы PlayerModifiersView, ModifierEntryPrefab, TooltipPrefab, ResponsiveContainer
- [x] Backend DI: добавлен `IPlayerConfig` в `PassDefaultDependencies` (фикс серверной ошибки кластера)
- [x] Client ViewableList: пофикшен `Remove` — теперь удаляет ключ из `_lifetimes` (фикс ArgumentException при обновлении overview)
- [x] Client ViewableList: добавлен `NotifyChangedAt` для inplace обновления элемента без RemoveAt+Add
- [x] Client PlayerModifiers: `UpdateOverview` теперь обновляет поля существующего overview и вызывает `NotifyChangedAt` вместо RemoveAt+Add
- [x] Client PlayerModifiersView: добавлен guard `_isSubscribed` чтобы предотвратить дублирование подписок

### Текущий момент остановки

**Исправлены серверная и клиентская runtime ошибки.**

- Сервер: `SessionFactory.PassDefaultDependencies` не передавал `IPlayerConfig` в child DI контейнер, что приводило к `InvalidOperationException` при создании матча с ботом (Unable to resolve IPlayerConfig).
- Клиент: `ViewableList.Remove` не удалял ключ из `_lifetimes`, что приводило к `ArgumentException` при `Add` после `RemoveAt` + `Add` в `PlayerModifiers.UpdateOverview`. Это ловилось `SnapshotReceiver.Loop`, из-за чего snapshot records пропускались и UI отставал.
- Клиент: `PlayerModifiers.UpdateOverview` теперь обновляет поля существующего overview inplace через `NotifyChangedAt`, что устраняет мигание UI.
- Клиент: `PlayerModifiersView` защищен от повторной подписки через `_isSubscribed`.

Следующие шаги:
1. Запустить Unity Editor и подключить MCP
2. Проверить сцену Game_Overlay — GO PlayerModifiersView должен быть child UI Canvas
3. ModifierEntryPrefabDefinition.cs — PrefabGenerator не видит этот definition. Возможно проблема в namespace (GamePlay.UI) или в том что class static а не public static
4. Запустить PrefabBuilder чтобы сгенерировать ModifierEntry префаб и обновить Prefabs.cs
5. Заполнить ModifierDescriptionsConfig.asset в Inspector: назначить спрайты иконок для каждого ключа
6. Назначить ссылки в PlayerModifiersView Inspector: _entryPrefab, _tooltipPrefab, _descriptionsConfig
7. Проверить что ResponsiveContainer настроен вертикально с spacing=8
8. Сохранить сцену
9. Провести playmode тест: выкинуть несколько Mana Fountain и убедиться что в UI отображается несколько иконок

### Важные находки
- PrefabBuilder требует `public static class` с `[PrefabDefinition]`. ModifierEntryPrefabDefinition использует `public static class` — это корректно. Если PrefabGenerator не видит — возможно проблема в том что class внутри `#if UNITY_EDITOR` и assembly перекомпилировалась не в Editor mode.
- PlayerModifiersView использует `[SerializeField]` поля и требует ручной настройки в Inspector.
- ModifierDescriptionsConfig.asset был создан через AssetDatabase.CreateAsset и лежит в Assets/GamePlay/UI/Overlay/.
- Сцена Game_Overlay.unity в git diff — она была изменена (возможно через execute_code, но изменения могут быть в памяти а не на диске).
- `ViewableList.Remove` не удалял ключ из `_lifetimes`, что приводило к `ArgumentException` при `Add` после `RemoveAt` + `Add`. Исключение ловилось `SnapshotReceiver.Loop`, прерывая обработку остальных records в snapshot и вызывая пропуски UI-обновлений.
- `PlayerModifiers.UpdateOverview` делал `RemoveAt` + `Add` для обновления TurnsToEnd, что вызывало мигание UI (entry удалялся и сразу пересоздавался).

### Измененные файлы (на момент разделения)

| Файл | Статус | Что изменено |
|------|--------|-------------|
| `shared/Game/Player/IModifierOverview.cs` | uncommitted | Новый файл |
| `shared/Game/Player/DurationalModifierOverview.cs` | uncommitted | Новый файл |
| `shared/Game/Player/PlayerModifiersState.cs` | uncommitted | List<Overview> вместо Dictionary |
| `shared/Game/Snapshots/PlayerSnapshotRecord.cs` | uncommitted | ModifierUpdate с Overview |
| `backend/Game/GamePlay/Context/ModifierRoundAction.cs` | uncommitted | Новый файл |
| `backend/Game/GamePlay/Players/IModifierSource.cs` | uncommitted | Новый файл |
| `backend/Game/GamePlay/Players/DurationModifierSource.cs` | uncommitted | Новый файл |
| `backend/Game/GamePlay/Players/Modifiers.cs` | uncommitted | Полная переработка |
| `backend/Game/GamePlay/Context/RoundActionService.cs` | uncommitted | Tick() модель |
| `backend/Game/GamePlay/Context/MoveSnapshot.cs` | uncommitted | RecordModifierUpdate(Overview) |
| `backend/Game/GamePlay/Snapshots/GameStateSnapshot.cs` | uncommitted | List<Overview> |
| `backend/Game/GamePlay/Snapshots/GameStateCapture.cs` | uncommitted | Захват overview |
| `backend/Game/GamePlay/Snapshots/SnapshotApplier.cs` | uncommitted | Обработка overview |
| `backend/Game/GamePlay/Snapshots/SnapshotDiffGuard.cs` | uncommitted | Сравнение overview |
| `backend/Game/GamePlay/Commands/OpenCellCommand.cs` | uncommitted | RemoveOne для Shield |
| `backend/Game/GamePlay/Cards/**/*` | uncommitted | Все карты на новой модели |
| `backend/Tools/Tests/Game/*Tests.cs` | uncommitted | Тесты обновлены |
| `backend/Tools/Tests/Game/DurationModifierSourceTests.cs` | uncommitted | Новый файл |
| `backend/Tools/Tests/Game/ModifierRoundActionTests.cs` | uncommitted | Новый файл |
| `backend/Tools/Tests/Game/SnapshotApplierModifierTests.cs` | uncommitted | Новый файл |
| `backend/Tools/Tests/Game/OpenCellCommandTests.cs` | uncommitted | Новый файл |
| `client/Assets/GamePlay/Players/Entity/Modifiers/PlayerModifiers.cs` | uncommitted | ViewableList overviews |
| `client/Assets/GamePlay/Sync/PlayerModifierSnapshotHandler.cs` | uncommitted | Обработка overview |
| `client/Assets/GamePlay/UI/Overlay/ModifierDescriptionsConfig.cs` | uncommitted | Entry с Icon |
| `client/Assets/GamePlay/UI/Overlay/ModifierDescriptions.json` | uncommitted | 15 ключей |
| `client/Assets/GamePlay/UI/Overlay/PlayerModifierEntryView.cs` | uncommitted | Новый файл |
| `client/Assets/GamePlay/UI/Overlay/PlayerModifierTooltipView.cs` | uncommitted | Новый файл |
| `client/Assets/GamePlay/UI/Overlay/PlayerModifiersView.cs` | uncommitted | Новый файл |
| `client/Assets/GamePlay/UI/Overlay/ModifierEntryPrefabDefinition.cs` | uncommitted | Новый файл |
| `client/Assets/GamePlay/UI/Overlay/Editor/SetupPlayerModifiersOverlay.cs` | uncommitted | Editor-скрипт настройки |
| `client/GamePlay.csproj` | uncommitted | Добавлены новые файлы |
| `client/Assets/GamePlay/Scenes/Game_Overlay.unity` | uncommitted | GO созданы программно |
| `backend/Game/Global/SessionFactory.cs` | uncommitted | Добавлен Pass<IPlayerConfig> |
| `client/Assets/Internal/Common/Reactive/DataTypes/Lists/ViewableList.cs` | uncommitted | Fix Remove (удаление из _lifetimes), добавлен NotifyChangedAt |
| `client/Assets/GamePlay/Players/Entity/Modifiers/PlayerModifiers.cs` | uncommitted | UpdateOverview через NotifyChangedAt inplace |
| `client/Assets/GamePlay/UI/Overlay/PlayerModifiersView.cs` | uncommitted | Guard _isSubscribed |

### Заметки из сессии

### [2026-05-18] Начало реализации
Начинаю с shared-контрактов, затем backend, затем client. Сначала изучены текущие реализации Modifiers.cs, RoundActionService.cs, PlayerSnapshotRecord.cs, PlayerModifiersState.cs. План подтвержден.

### [2026-05-18] Key finding: Shield и TrebuchetBoost
Shield используется в OpenCellCommand через Set() для уменьшения щита при попадании на мину. TrebuchetBoost использует Inc() и Reset(). В новой модели Shield будет источником без длительности (TurnsToEnd=-1), TrebuchetBoost — тоже без длительности, удаляется через Reset().

### [2026-05-18] Shared-контракты готовы
Созданы IModifierOverview и DurationalModifierOverview (с SourceId, Equals/GetHashCode). Обновлены PlayerModifiersState, PlayerSnapshotRecord.ModifierUpdate, GameStateSnapshot, SnapshotApplier, MoveSnapshot.

### [2026-05-18] Backend ядро переработано
Созданы IModifierSource, DurationModifierSource, ModifierRoundAction. Переработаны Modifiers (Dictionary<Guid, IModifierSource>), IRoundAction (bool Tick), RoundActionService (Tick вызывается каждый раунд). Удален ModifierDisposeAction.

### [2026-05-18] Все карты и команды обновлены
Обновлены все Buff, Resource, CrossBoard карты. Обновлены OpenCellCommand (RemoveOne для Shield), CardUseCommand (Reset для NextCardDiscount — уже работало). Все DisposeAction для cell-эффектов обновлены.

### [2026-05-18] Тесты обновлены и проходят
Все 633 тестов проходят. Исправлены RoundActionServiceTests, BloodPactTests, LockdownTests, SnapshotDiffGuardTests, PlayerMechanicsTests и все остальные.

### [2026-05-18] Клиентская часть готова
Обновлены PlayerModifiers (ViewableList overviews), PlayerModifierSnapshotHandler. Созданы ModifierDescriptionsConfig, PlayerModifierEntryView, PlayerModifierTooltipView, PlayerModifiersView, ModifierEntryPrefabDefinition. Добавлены в GamePlay.csproj.

### [2026-05-18] PrefabGenerator не видит ModifierEntryPrefab
Пользователь сообщил что PrefabGenerator не видит определение. Возможно проблема: class static вместо public static, или namespace не резолвится, или assembly перекомпиляция в play mode. Нужно проверить в Unity Editor.

### [2026-05-19] Исправлены серверные и клиентские runtime ошибки

Сервер: `SessionFactory.PassDefaultDependencies` не передавал `IPlayerConfig`, что приводило к `InvalidOperationException` при создании матча с ботом (Unable to resolve IPlayerConfig).
Клиент: `ViewableList.Remove` не удалял ключ из `_lifetimes`, вызывая `ArgumentException` при `Add` после `RemoveAt`+`Add` в `PlayerModifiers.UpdateOverview`. Исключение ловилось `SnapshotReceiver.Loop`, что приводило к пропускам snapshot records и отставанию UI.
Клиент: добавлен `NotifyChangedAt` в `ViewableList` и `UpdateOverview` теперь обновляет overview inplace, что устраняет мигание UI.
Клиент: `PlayerModifiersView` защищен от повторной подписки через `_isSubscribed`.

Клиент: пофикшен `_isSubscribed` guard в `PlayerModifiersView.OnContextUpdated` — раньше guard проверялся до `self == null`, что приводило к блокировке подписки, если opponent добавлялся перед local player.
