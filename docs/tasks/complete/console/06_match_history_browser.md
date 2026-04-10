## Задача: Match History Browser — глобальная история матчей

### Цель
Создать страницу для просмотра истории всех матчей в системе с фильтрами по дате, типу, результату. Сейчас история матчей доступна только per-user через UserDashboard.

### Контекст
`IUserMatchHistory` хранит историю per-user (список `MatchOverview`). `Match` grain хранит состояние одного матча. Глобального индекса матчей нет — нужно создать. `MatchOverview` содержит: `Guid Id`, `GameMatchType Type`, `Guid Winner`, `TimeSpan Time`, `DateTime Date`, `IReadOnlyList<MatchParticipantData> Participants`.

### Шаги реализации

**1. Создать глобальную коллекцию матчей**
  1.1. Создать `MatchCollection` по паттерну `StateCollection<Guid, MatchCollectionEntry>` — `backend/Meta/Matches/MatchCollection.cs` [новый файл]
  1.2. `MatchCollectionEntry`: `Guid Id`, `GameMatchType Type`, `Guid Winner`, `TimeSpan Duration`, `DateTime Date`, `int ParticipantCount`
  1.3. State class с `[GenerateSerializer]`, `[Id(N)]`, `IStateValue`
  1.4. Зарегистрировать в `StatesLookup` + `ProjectsSetupExtensions.AddStates()`
  1.5. Зарегистрировать StateCollection в DI

**2. Записывать матчи при завершении**
  2.1. В `Match.OnComplete()` — вызвать `_matchCollection.OnUpdatedTransactional()` — `backend/Meta/Matches/Match.cs`
  2.2. Inject `IMatchCollection` в `Match` grain constructor

**3. Создать Blazor страницу**
  3.1. Создать `backend/Console/Pages/Matches/MatchHistory.razor` [новый файл — добавить в Console.csproj]
  3.2. Route: `/match-history`
  3.3. Фильтры: тип матча (dropdown), дата (от-до), результат
  3.4. Таблица: Date, Type, Players, Winner, Duration, кнопка перехода к деталям
  3.5. Пагинация (PageSize = 20)
  3.6. Сортировка по дате (newest first)

**4. Интеграция**
  4.1. Добавить роут в `ConsoleConstants.Pages` — `backend/Console/Common/ConsoleConstants.cs`
  4.2. Добавить карточку в `HomeGameSection.razor` — `backend/Console/Pages/Home/HomeGameSection.razor`

### Ключевые файлы

| Файл | Роль в задаче |
|------|---------------|
| `backend/Meta/Matches/Match.cs` | Вызов OnUpdatedTransactional при завершении |
| `backend/Meta/Matches/MatchCollection.cs` | Новая коллекция [новый файл] |
| `backend/Meta/Matches/MatchServicesExtensions.cs` | DI регистрация коллекции |
| `backend/Console/Pages/Matches/MatchHistory.razor` | Новая страница [новый файл] |
| `backend/Console/Common/ConsoleConstants.cs` | Роут |
| `backend/Orchestration/Extensions/ProjectsSetupExtensions.cs` | AddStates регистрация |

### Документация к прочтению
- `rules/ORLEANS_STATE.md` — StateCollection 3-step registration
- `rules/BLAZOR.md` — паттерны страниц

### Риски
- **Объем данных**: со временем коллекция вырастет. StateCollection загружает все в память — для match history нужна пагинация на уровне SQL, а не in-memory. Возможно, стоит использовать прямой SQL запрос через `IDbSource` вместо StateCollection
- **Миграция**: существующие матчи не попадут в коллекцию (только новые). Для ретроспективы — одноразовый скрипт миграции
- **Транзакция**: `OnUpdatedTransactional` внутри `Match.OnComplete()` — нужно убедиться что OnComplete уже в транзакции
