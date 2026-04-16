## Задача: Страница истории матчей в главном меню

### Цель

Добавить новую страницу истории матчей, доступную из главного меню.

По макету:
- Сверху страницы — значения текущего рейтинга и прогрессии (с подписью и подчёркиванием).
- Ниже слева — скролл со списком матчей (каждая строка — дата/время матча).
- Ниже справа — панель деталей выбранного матча:
  - Верхняя часть: красный фон, 5 карт соперника.
  - Средняя полоса: информация о матче (время раунда, изменение рейтинга, изменение прогрессии).
  - Нижняя часть: синий фон, 5 наших карт.
- При клике по матчу в списке детали подгружаются с сервера; до получения ответа показывать индикатор загрузки.
- В нижнюю панель главного меню добавить кнопку `history`, открывающую эту страницу.

### Контекст

- Экран встраивается в существующий MainMenu flow по тому же паттерну, что `MenuProgression`, `MenuDecks`:
  MonoBehaviour + ISceneService + IScopeSetup + IUIStateAsyncEnterHandler, активируется через
  `IUIStateMachine.ProcessChild(stateMachine.Base, _history)` из `MenuNavigation`, прячет bottom-bar
  на время показа, возвращает через `exit-btn`/Escape.
- На бэкенде уже есть нужные сущности:
  - `IUserMatchHistory.GetBlock(int count)` возвращает `IReadOnlyList<MatchOverview>` (у каждого пользователя свой лог).
  - `IMatch.GetState()` возвращает `MatchState` с `ParticipantDecks` и `RatingChanges`.
  - `UserProgression` и `UserRating` уже рассылают проекции (`ProgressionProjection`, `RatingProjection`).
- В клиенте `RatingProjection` ещё не зарегистрирована в `MetaServicesExtensions.AddMetaServices()`
  (зарегистрированы ProfileProjection, ProgressionProjection, DeckProjection, CardsProjection,
  LootProjection, MatchResult, LobbyResult). Нужно добавить.
- Shared контракты для "получить список истории" и "получить детали матча" отсутствуют —
  их необходимо создать по паттерну `SharedBackendUser.LootOpenRequest/LootOpenResponse`
  + `UserCommand<T>` в `backend/Orchestration/MetaGateway/UserFlow/`.
- Для рендера карт использовать существующий шаблон `Menu/UI/Decks/MenuCard.uxml` + класс `CardElement` из `MenuDecks.cs`
  (карты соперника/свои — это тот же визуал, что и в колоде).

### Шаги реализации

**1. Shared: добавить сетевые контракты для истории и деталей матча**
  1.1. Расширить `SharedBackendUser` новыми `[MemoryPackable]` классами — `shared/Backend/SharedBackendUser.cs`:
       - `MatchHistoryRequest : INetworkContext` (поле `Count` — сколько последних матчей).
       - `MatchHistoryResponse : INetworkContext` (поле `Matches : List<Match>` — переиспользуем существующий `SharedBackendUser.Match`).
       - `MatchDetailsRequest : INetworkContext` (поле `MatchId : Guid`).
       - `MatchDetailsResponse : INetworkContext` (поля `MatchId`, `OpponentCards : List<CardType>`, `OwnCards : List<CardType>`,
         `Time : TimeSpan`, `RatingChange : int`, `ProgressionChange : int`, `Won : bool`).
  1.2. Зарегистрировать новые типы в `SharedBackendUser.Register(...)`.

**2. Backend: добавить проекцию рейтинга на клиентский сокет и команды истории/деталей**
  2.1. Создать `MatchCommands.cs` — `backend/Orchestration/MetaGateway/UserFlow/MatchCommands.cs`
       [новый файл — добавить в `backend/Orchestration/Orchestration.csproj`]:
       - `MatchCommands.GetHistory : UserCommand<SharedBackendUser.MatchHistoryRequest>` — берёт `IUserMatchHistory.GetBlock(count)` текущего пользователя, конвертирует каждый `MatchOverview.ToContext()` в `SharedBackendUser.Match`, возвращает `MatchHistoryResponse`.
       - `MatchCommands.GetDetails : UserCommand<SharedBackendUser.MatchDetailsRequest>` — берёт `IMatch` по `MatchId`, читает `MatchState`, выделяет наши/чужие карты из `ParticipantDecks` (по `session.UserId`), заполняет `Time`, `RatingChange` (наш дельта из `RatingChanges`), `ProgressionChange` (из `ProgressionOptions.WinExperience`/`LossExperience` по `Won`).
       - `AddMatchCommands(this IHostApplicationBuilder builder)` регистрирует оба UserCommand.
  2.2. Подключить `AddMatchCommands` в `backend/Orchestration/MetaGateway/UserFlow/UserFlowExtensions.cs`.

**3. Client: регистрация проекции рейтинга и расширения MetaBackend**
  3.1. В `client/Assets/Meta/MetaServicesExtensions.cs` добавить `.RegisterBackendProjection<SharedBackendUser.RatingProjection>()`.
  3.2. В `client/Assets/Meta/Connection/BackendEndpoints.cs` добавить extension-методы:
       - `UniTask<SharedBackendUser.MatchHistoryResponse> GetMatchHistory(this IMetaBackend backend, int count)` через `Connection.Writer.WriteRequest<...>`.
       - `UniTask<SharedBackendUser.MatchDetailsResponse> GetMatchDetails(this IMetaBackend backend, Guid matchId)` — аналогично.

**4. Client: UXML/USS новой страницы истории**
  4.1. Создать `client/Assets/Menu/UI/History/MenuHistory.uxml`:
       - корень `history-root` на синем фоне (`resource("Menu/background_blue")`);
       - сверху ряд статов: два блока `stat-block` (Rating, Progression) с большим числом, подчёркиванием и подписью;
       - ниже горизонтальный контейнер: слева `ScrollView` `history-list` (класс `pool-scroll`), справа `VisualElement` `details-panel` c двумя подпанелями — верхняя `opponent-area` (красный фон + 5 слотов карт), средняя строка `match-info-row` (`round-time`, `rating-change`, `progression-change`), нижняя `own-area` (синий фон + 5 слотов карт);
       - поверх `details-panel` полупрозрачный `loading-overlay` с индикатором (бар как у `progression`), управляется через класс `visible`;
       - `Button` `btn-exit` класса `exit-btn`.
  4.2. Создать `client/Assets/Menu/UI/History/MenuHistory.uss`:
       - `.history-root`, `.history-title`, `.stat-row`, `.stat-block`, `.stat-value`, `.stat-divider`, `.stat-label`;
       - `.history-list` (использовать скролл-рецепт из `MenuCards.uss .pool-scroll` — содержимое вертикальное, `flex-direction: column`, `flex-wrap: nowrap`);
       - `.history-entry`, `.history-entry:hover`, `.history-entry.selected` — строка с датой/временем в пиксельной рамке;
       - `.details-panel`, `.opponent-area` (красный фон), `.own-area` (синий фон), `.match-info-row`;
       - `.card-slot` (42×48, без фона) — либо переиспользовать `.card-element` из `MenuCards.uss`;
       - `.loading-overlay`, `.loading-bar`, `.loading-bar-fill`;
       - `.exit-btn` (взять у `MenuProgression.uss`).
  4.3. Зарегистрировать оба файла в `client/Menu.csproj` (`.cs` через `Compile Include`, `.uxml`/`.uss` через `None Include`).

**5. Client: скрипт экрана истории**
  5.1. Создать `client/Assets/Menu/Screens/History/MenuHistory.cs`
       [новый файл — добавить в `client/Menu.csproj`]:
       - `public interface IMenuHistory : IUIState {}`
       - `MenuHistory : MonoBehaviour, IMenuHistory, ISceneService, IScopeSetup, IUIStateAsyncEnterHandler`
         по образцу `MenuProgression`:
         * Поля: `[SerializeField] VisualTreeAsset _cardTemplate;`
         * Inject: `IMetaBackend backend`, `IBackendProjection<RatingProjection>`, `IBackendProjection<ProgressionProjection>`, `ICardsRegistry`, `ICardConfigs`.
         * `Create()` — `SetActive(false)` + `RegisterComponent(this).As<IMenuHistory>().As<IScopeSetup>()`.
         * `OnSetup()` — подписки на проекции рейтинга/прогрессии, обновление Label `stat-value`.
         * `OnEntered(handle)` — `AttachGameObject`, показать root, спрятать bottom-bar (паттерн `FindBottomBar`), загрузить первую страницу истории через `_backend.GetMatchHistory(30)`, наполнить `history-list` кликабельными `history-entry` (форматирование `dd.MM HH:mm`), навесить Escape/exit-btn, дождаться закрытия.
         * Клик по матчу — `LoadMatchDetails(matchId, lifetime)`: показать overlay, `await _backend.GetMatchDetails(matchId)`, скрыть overlay, отрисовать карты в `opponent-area` и `own-area` через `CardElement.SetCard`, заполнить `round-time`, `rating-change` (со знаком), `progression-change`.
         * Следить, чтобы клики по матчу не накладывались (гвард `_isLoading`).

**6. Client: добавить кнопку history в bottom-bar**
  6.1. В `client/Assets/Menu/UI/Main/MenuBottomBar.uxml` между `btn-progression` и `btn-cards` добавить
       `<t:NavButton name="btn-history" text="history" class="u-ithaca" />` + `<VisualElement class="separator" />`.
  6.2. В `client/Assets/Menu/Main/Navigation/MenuNavigation.cs` добавить:
       - в `Construct` — `IMenuHistory history`, сохранить в поле;
       - в `OnSetup` — `var btnHistory = Root.Q<Button>("btn-history"); btnHistory.ListenClick(lifetime, () => _stateMachine.ProcessChild(_stateMachine.Base, _history));`.

**7. Unity Editor: интеграция в сцену (ручная настройка)**
  7.1. В `client/Assets/Menu/Common/Options/Menu.unity` создать новый GameObject `History` с
       `UIDocument` (PanelSettings = `MenuPanelSettings.asset`, Source Asset = `MenuHistory.uxml`)
       и компонентом `MenuHistory`, назначить `_cardTemplate = MenuCard.uxml` VisualTreeAsset.
       Шаг ручной — описать в `_result.md` и отметить в `_progress.md`.

### Ключевые файлы

| Файл | Роль в задаче |
|------|---------------|
| `shared/Backend/SharedBackendUser.cs` | Новые сетевые контракты (MatchHistoryRequest/Response, MatchDetailsRequest/Response) |
| `backend/Orchestration/MetaGateway/UserFlow/MatchCommands.cs` | Новый — UserCommand-ы, отдающие историю и детали |
| `backend/Orchestration/MetaGateway/UserFlow/UserFlowExtensions.cs` | Подключение AddMatchCommands |
| `backend/Meta/Users/Matches/UserMatchHistory.cs` | Источник списка матчей (`GetBlock`) |
| `backend/Meta/Matches/Match.cs` | Источник деталей матча (`GetState`, `ParticipantDecks`, `RatingChanges`) |
| `client/Assets/Meta/MetaServicesExtensions.cs` | Регистрация RatingProjection |
| `client/Assets/Meta/Connection/BackendEndpoints.cs` | GetMatchHistory / GetMatchDetails расширения |
| `client/Assets/Menu/UI/History/MenuHistory.uxml` | Новая разметка страницы истории |
| `client/Assets/Menu/UI/History/MenuHistory.uss` | Новые стили страницы истории |
| `client/Assets/Menu/Screens/History/MenuHistory.cs` | Новый MonoBehaviour экрана истории |
| `client/Assets/Menu/UI/Main/MenuBottomBar.uxml` | Добавить кнопку `btn-history` |
| `client/Assets/Menu/Main/Navigation/MenuNavigation.cs` | Биндинг кнопки history на переход к экрану |
| `client/Menu.csproj` | Регистрация новых .cs/.uxml/.uss |
| `backend/Orchestration/Orchestration.csproj` | Регистрация MatchCommands.cs (если требуется ручное) |
| `client/Assets/Menu/Common/Options/Menu.unity` | Добавить GameObject History (ручной шаг в Editor) |

### Документация к прочтению

- `docs/UI_MENU.md` — пиксель-арт UI Toolkit: PanelSettings 512×288, resource()-спрайты, ScrollView-рецепт (разделы 7 и 8) — **ключ** для скролла истории.
- `docs/COMMON_CONTAINER.md` — MonoBehaviour сервисный паттерн (ISceneService/IScopeSetup/Create/OnSetup), т.к. создаётся новая такая MonoBehaviour.
- `docs/COMMON_LIFETIMES.md` — `ListenClick`/`Advise` через lifetime в InnerLifetime Handle.
- `docs/COMMON_ORLEANS.md` — UserCommand + [Transaction] паттерн для новых серверных команд.
- `docs/API_DESIGN_FULL.md` — UniTask/async на клиенте для GetMatchHistory/GetMatchDetails.
- `docs/CODE_STYLE_FULL.md` — `_camelCase` поля, порядок членов.

### Риски

- `MatchState.RatingChanges[userId]` — это абсолютное значение прибавки для победителя/проигравшего,
  там уже хранится знак (для проигравшего — отрицательное из `UserRatingRecords.Loss.GetRating()`).
  Надо перепроверить при реализации.
- Запросы на получение деталей матча идут через `_orleans.Transactions.Run(() => match.GetState())`
  по образцу `LootCommands.OpenLootBox`.
- Изменение сцены `Menu.unity` вручную в YAML опасно (fileID коллизии). Безопаснее —
  попросить пользователя создать GameObject в Unity Editor после завершения всех файловых изменений.
- `MenuBottomBar` уже узкий, кнопка `history` должна войти рядом с `progression`/`cards` без сломанной
  ширины chat-input (150px фиксированная) — проверить визуально.

Начинаем реализацию?
