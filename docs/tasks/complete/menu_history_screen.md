## Menu History Screen

### Что сделано
- Новая страница истории матчей в главном меню: stats-row (Rating/Progression), слева ScrollView матчей, справа details-panel (красная зона карт соперника, ряд метрик, синяя зона наших карт) с loading overlay.
- Shared контракты: `MatchHistoryRequest/Response`, `MatchDetailsRequest/Response` в `SharedBackendUser`.
- Backend: `MatchCommands.GetHistory` и `GetDetails` — UserCommand-ы через `IUserMatchHistory.GetBlock` и `IMatch.GetState`, разбор `ParticipantDecks`/`RatingChanges` по userId.
- Client: регистрация `RatingProjection`, endpoint-методы `GetMatchHistory`/`GetMatchDetails` на `IMetaBackend`.
- Кнопка `btn-history` в `MenuBottomBar.uxml` между progression и cards, биндинг в `MenuNavigation` через `ProcessChild`.

### Ключевые файлы
- `shared/Backend/SharedBackendUser.cs` — новые MemoryPack-контракты
- `backend/Orchestration/MetaGateway/UserFlow/MatchCommands.cs` + регистрация в `Program.cs`
- `client/Assets/Menu/UI/History/MenuHistory.uxml`/`.uss`
- `client/Assets/Menu/Screens/History/MenuHistory.cs` — MonoBehaviour по паттерну `MenuProgression`
- `client/Assets/Menu/Common/Options/Menu.unity` — вручную прописан root GameObject `History` через YAML

### Заметки
- `Menu.unity` обновлён прямо в YAML (не в Editor) — fileID из диапазона 2100000000+, GUID MenuPanelSettings `5ba6d30d3b21bea1c86c7e3e7cbe6e1b`. Ссылка добавлена в `SceneRoots.m_Roots` и в `SceneServicesFactory._services` (иначе DI не увидит MonoBehaviour).
- `MatchState.RatingChanges[userId]` уже знаковое — для проигравшего отрицательное из `UserRatingRecords.Loss.GetRating()`.
- Loading indicator — статическая `loading-bar-fill` 30%, для быстрого ответа бэкенда достаточно.
