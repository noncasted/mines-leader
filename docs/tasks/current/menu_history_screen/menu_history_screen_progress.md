## Страница истории матчей — Рабочие заметки

### Статус: В работе

### Заметки

### Backend
- Shared: в `SharedBackendUser` добавлены `MatchHistoryRequest/Response`, `MatchDetailsRequest/Response` и зарегистрированы в `Register()`.
- Backend: создан `backend/Orchestration/MetaGateway/UserFlow/MatchCommands.cs` с двумя `UserCommand`:
  `GetHistory` — берёт `IUserMatchHistory.GetBlock(count)` через `_orleans.Transactions.Run`, сортирует по дате desc;
  `GetDetails` — `IMatch.GetState()`, разбор `ParticipantDecks` на свои/чужие, `RatingChange` из `RatingChanges[userId]`,
  `ProgressionChange` — из `ProgressionOptions.WinExperience`/`LossExperience` по победителю.
- `AddMatchCommands` зарегистрирован в `MetaGateway/Program.cs` после `AddLootCommands`.
- MetaGateway собирается без ошибок, 74 warning — все предсуществующие нульабилити.

### Client
- `RatingProjection` зарегистрирован в `MetaServicesExtensions.AddMetaServices()` (раньше не был).
- В `BackendEndpoints` добавлены `GetMatchHistory(count)` и `GetMatchDetails(matchId)` через `Connection.Writer.WriteRequest`.
- Создан экран:
  - `client/Assets/Menu/UI/History/MenuHistory.uxml` — layout по макету (stats-row сверху, content-row снизу: слева `history-list` ScrollView, справа `details-panel` с `opponent-area`/`match-info-row`/`own-area`, плюс `loading-overlay` и `details-empty` overlay).
  - `client/Assets/Menu/UI/History/MenuHistory.uss` — стили: красный `.opponent-area` (120,36,44), синий `.own-area` (38,62,128), `.history-entry` с hover/selected состояниями, `.loading-bar-fill` для индикатора.
  - `client/Assets/Menu/Screens/History/MenuHistory.cs` — MonoBehaviour по паттерну `MenuProgression`: Listen на RatingProjection/ProgressionProjection обновляет stat-value, OnEntered грузит первую страницу через `_backend.GetMatchHistory(30)`, клик по матчу вызывает `_backend.GetMatchDetails` с показом loading-overlay и заполнением слотов карт через `CardElement.SetCard`.
  - `.meta` файлы на новые файлы и папки.
- В `MenuBottomBar.uxml` добавлена кнопка `btn-history` между `btn-progression` и `btn-cards`.
- `MenuNavigation` инжектит `IMenuHistory` и биндит `btn-history` на `ProcessChild(_stateMachine.Base, _historyScreen)`.
- В `client/Menu.csproj` добавлены три новых Include (`MenuHistory.cs`, `MenuHistory.uxml`, `MenuHistory.uss`).

### Scene (Menu.unity)
- Добавлен новый root GameObject `History` с компонентами UIDocument (PanelSettings = `MenuPanelSettings.asset` guid `5ba6d3...`, Source Asset = новый `MenuHistory.uxml`) и MonoBehaviour `MenuHistory` (guid `9dd70cab...`), `_cardTemplate` = `MenuCard.uxml`.
- fileID начиная с 2100000000 — не пересекается с существующими (max ~964327958).
- Ссылка добавлена в `SceneRoots.m_Roots` в конец.

### Сборка
- `client/Menu.csproj` — 0 errors, 0 warnings.
- `backend/Orchestration/MetaGateway/MetaGateway.csproj` — 0 errors, 9 warnings (все предсуществующие NU1506).

### Runtime fix: SceneServicesFactory._services
- Причина первого VContainerException "No such registration of type: Menu.Screens.IMenuHistory":
  `SceneServicesFactory` хранит ISceneService-компоненты в сериализованном массиве `_services`
  (обновляется в Editor через `OnReload`). Добавление GameObject в сцену недостаточно — нужно
  добавить MonoBehaviour fileID в `_services:` рядом с остальными MenuProgression/MenuDecks/etc.
- В `Menu.unity` в блок `SceneServicesFactory._services` добавлена запись `{fileID: 2100000003}`
  (MenuHistory MonoBehaviour). Теперь Create() вызывается и IMenuHistory регистрируется в scope.
- **Важный ури: все последующие экраны в `Menu.unity` требуют обновления `SceneServicesFactory._services`,
  иначе DI не увидит MonoBehaviour**.


