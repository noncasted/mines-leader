## Задача: Список игроков и дашборд игрока

### Цель
Добавить на главную страницу виджет со списком игроков (с поиском и пагинацией) и создать отдельную страницу-дашборд игрока с шапкой, колодами и историей матчей, доступную по URL с ID.

### Риски

- **Дата регистрации не хранится:** `UserAuthState` содержит только `bool IsExists`, `DateTime` нигде нет. Либо добавить `DateTime RegisteredAt` в `UserState` и заполнять при первой регистрации — либо убрать дату из шапки. Нужно решение до начала реализации.
- **Прогрессия не в `IUserCollection`:** `IUserCollection` содержит только `UserState` (Id + Name). Прогрессию надо подгружать отдельно через `IStateStorage` по паттерну BotManagement — массовый запрос `Storage.Read<Guid, UserProgressionState>(identities)`.
- **Колоды игрока:** Доступны только через Orleans grain `handle.Deck.GetSelected()` — только активная колода. Все колоды — через `IStateStorage` с `StatesLookup.UserDeck`.
- **История матчей:** Через `IStateStorage` с `StatesLookup.UserMatchHistory`. В `UserMatchHistoryState.Matches` нет поля Winner name — есть только `Guid Winner`. Имя победителя придётся резолвить отдельно или показывать только Guid.
- **Папка `Pages/User/` уже есть в `.csproj`** (строка `<Folder Include="Pages\User\" />`), но файлы нужно добавить в `<Compile Include="..." />`.

### Шаги реализации

1. **Решить вопрос с датой регистрации** — добавить `DateTime RegisteredAt` в `UserState` (и проставлять в `UserFactory`) или убрать из дашборда.

2. **Добавить маршруты** — `backend/Console/Common/ConsoleConstants.cs`
   - `UserDashboard = "/users/{0}"`

3. **Создать `PlayersWidget.razor`** — `backend/Console/Pages/Home/PlayersWidget.razor` [новый файл — добавить в Console.csproj]
   - `@inherits UiComponent`, инжект `IUserCollection`, `IStateStorage`
   - В `OnSetup`: загрузить все `UserState` + батч `UserProgressionState` через `Storage.Read`
   - Подписаться на `IUserCollection.Updated.Advise(lifetime, ...)` для реактивного обновления
   - Строка поиска (filter по Name или Id.ToString())
   - Таблица: Name | ID (click → `JSRuntime.InvokeVoidAsync("navigator.clipboard.writeText", ...)`) | XP
   - Три точки при hover → `Navigation.NavigateTo($"/users/{id}")`
   - Пагинация: 10 записей, prev/next кнопки

4. **Изменить `Home.razor`** — `backend/Console/Pages/Home/Home.razor`
   - Переключить на двухколоночный лейаут: слева тайлы (Tests, Configs, Bots), справа `<PlayersWidget />`

5. **Создать `UserDashboard.razor`** — `backend/Console/Pages/User/UserDashboard.razor` [новый файл — добавить в Console.csproj]
   - `@page "/users/{Id}"`, `[Parameter] public string Id { get; set; }`
   - В `OnInitializedAsync`: парсим Guid, загружаем `UserState`, `UserProgressionState`, `UserMatchHistoryState`, `UserDeckState` через `IStateStorage` + Orleans для колод
   - **Шапка:** имя, XP (progression.CalculateTotal()), дата регистрации (если добавим в UserState)
   - **Секция колод:** таблица по `UserDeckState.Entries`, каждая запись — список CardType, активная выделена
   - **История матчей:** таблица `MatchOverview[]` с пагинацией (10 штук), колонки: Date | Type | Winner | Duration

### Ключевые файлы

| Файл | Роль |
|------|------|
| `backend/Console/Pages/Home/Home.razor` | Главная — добавить двухколоночный лейаут |
| `backend/Console/Pages/Bots/BotManagement.razor` | Образец: паттерн загрузки через IStateStorage + таблица |
| `backend/Console/Common/UiComponent.cs` | Базовый компонент — OnSetup + Lifetime |
| `backend/Console/Common/ConsoleConstants.cs` | Маршруты — добавить UserDashboard |
| `backend/Console/Console.csproj` | Добавить Compile Include для новых файлов |
| `backend/Meta/Users/Entities/UsersCollection.cs` | IUserCollection — источник данных списка |
| `backend/Meta/Users/Progression/UserProgression.cs` | UserProgressionState.CalculateTotal() |
| `backend/Meta/Users/Matches/UserMatchHistory.cs` | UserMatchHistoryState.Matches |
| `backend/Meta/Users/Decks/UserDeck.cs` | UserDeckState.Entries |
| `backend/Meta/Users/Entities/User.cs` | UserState — Name, Id (+ RegisteredAt если добавим) |
| `backend/Common/Lookups/StatesLookup.cs` | StateName/TableName для IStateStorage запросов |

### Документация к прочтению
- `rules/CODE_STYLE.md` — порядок членов, именование (применяем для новых Blazor компонентов)
