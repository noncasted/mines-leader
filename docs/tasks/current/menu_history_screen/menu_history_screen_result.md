## Страница истории матчей — Результат

### Статус: Завершено

### Что сделано

- Shared: новые контракты `MatchHistoryRequest/Response` и `MatchDetailsRequest/Response` в `SharedBackendUser` + регистрация.
- Backend: новые `UserCommand`-ы `MatchCommands.GetHistory` и `MatchCommands.GetDetails`, подключены в `Program.cs`.
- Client: добавлена регистрация `RatingProjection` в `MetaServicesExtensions`.
- Client: добавлены endpoint-методы `GetMatchHistory` / `GetMatchDetails` в `BackendEndpoints`.
- Client: новый экран `MenuHistory` — UXML + USS + MonoBehaviour по паттерну `MenuProgression`, со статами рейтинга/прогрессии сверху, списком матчей слева и панелью деталей справа (красная зона карт соперника / синяя зона наших карт, ряд метрик посередине, индикатор загрузки).
- Client: кнопка `btn-history` в `MenuBottomBar.uxml` + биндинг в `MenuNavigation`.
- Scene: в `Menu.unity` вручную добавлен root GameObject `History` с компонентами `UIDocument` (MenuPanelSettings + MenuHistory.uxml) и `MenuHistory` (с `_cardTemplate = MenuCard.uxml`).

### Измененные файлы

| Файл | Что изменено |
|------|-------------|
| `shared/Backend/SharedBackendUser.cs` | Добавлены 4 новых `[MemoryPackable]` контракта и их регистрация |
| `backend/Orchestration/MetaGateway/UserFlow/MatchCommands.cs` | Новый файл — `GetHistory` и `GetDetails` UserCommand-ы |
| `backend/Orchestration/MetaGateway/Program.cs` | Подключение `.AddMatchCommands()` |
| `client/Assets/Meta/MetaServicesExtensions.cs` | Регистрация `RatingProjection` |
| `client/Assets/Meta/Connection/BackendEndpoints.cs` | Новые методы `GetMatchHistory`, `GetMatchDetails` |
| `client/Assets/Menu/UI/History/MenuHistory.uxml` | Новая разметка экрана |
| `client/Assets/Menu/UI/History/MenuHistory.uss` | Новые стили экрана |
| `client/Assets/Menu/Screens/History/MenuHistory.cs` | Новый MonoBehaviour экрана |
| `client/Assets/Menu/UI/History.meta`, `client/Assets/Menu/Screens/History.meta` | .meta для папок |
| `client/Assets/Menu/UI/History/MenuHistory.uxml.meta`, `MenuHistory.uss.meta`, `MenuHistory.cs.meta` | .meta для ассетов |
| `client/Assets/Menu/UI/Main/MenuBottomBar.uxml` | Добавлена кнопка `btn-history` между progression и cards |
| `client/Assets/Menu/Main/Navigation/MenuNavigation.cs` | Инъекция и биндинг `IMenuHistory` |
| `client/Menu.csproj` | Добавлены три новых Include |
| `client/Assets/Menu/Common/Options/Menu.unity` | Добавлен новый root GameObject `History` с UIDocument + MenuHistory |

### Отличия от плана

- В плане предполагался ручной шаг создания GameObject в Unity Editor. Удалось безопасно прописать новый root GameObject прямо в YAML `Menu.unity` c fileID из запаса 2100000000+ (вне диапазона существующих), ссылкой на MenuPanelSettings (GUID `5ba6d30d3b21bea1c86c7e3e7cbe6e1b`) и новыми stable GUID-ами .meta-файлов.
- `MatchOverview.ToContext()` уже возвращает `SharedBackendUser.Match` — переиспользуется как элемент списка.

### Нерешённые вопросы

- Индикатор загрузки сейчас статический (`.loading-bar-fill` с фиксированной шириной 30%). При необходимости можно заменить на анимацию через `Schedule` или индет-прогресс. Для обычного быстрого ответа бэкенда этого достаточно.
- Клиент должен проверить в Editor, что панель настроек и `_cardTemplate` подхватились на созданном GameObject "History" — если Unity пересгенерирует scene-YAML, inline-полей `m_EditorClassIdentifier` может не хватить и поля `sourceAsset`/`_cardTemplate` могут потребоваться ручной повторной привязки. Рекомендуется открыть сцену после pull и убедиться в Inspector, что поля заполнены корректно.
