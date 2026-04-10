## Задача: State Explorer — просмотр raw grain state

### Цель
Создать страницу для просмотра raw state любого grain по его ID. Полезно для дебага: "почему у этого юзера сломался рейтинг?", "какое состояние у этого матча?".

### Контекст
`IStateStorage` уже имеет метод `Read<TKey, TState>(identities)` для чтения состояний из PostgreSQL. `IGrainStatesRegistry` хранит маппинг всех зарегистрированных state типов. Данные хранятся в JSON в PostgreSQL таблицах.

### Шаги реализации

**1. Создать API для чтения raw state**
  1.1. Метод: принимает `stateType` (строка из StatesLookup) + `grainKey` (Guid или string)
  1.2. Возвращает: JSON строку raw state
  1.3. Реализация: использовать `IStateStorage` + `IGrainStatesRegistry` для чтения из правильной таблицы
  1.4. Или прямой SQL: `SELECT data FROM {tableName} WHERE key = @key`

**2. Создать Blazor страницу**
  2.1. Создать `backend/Console/Pages/Debug/StateExplorer.razor` [новый файл — добавить в Console.csproj]
  2.2. Route: `/debug/state`
  2.3. Dropdown: выбор типа state (User, UserRating, UserProgression, UserDeck, Match, Bot, etc.) — из StatesLookup
  2.4. Input: Grain Key (Guid)
  2.5. Кнопка "Load"
  2.6. Результат: JSON viewer с подсветкой (или `<pre>` для MVP)
  2.7. Возможность скопировать JSON

**3. Интеграция**
  3.1. Добавить роут в `ConsoleConstants.Pages`
  3.2. Доступ только через прямой URL или Debug секцию на Home

### Ключевые файлы

| Файл | Роль в задаче |
|------|---------------|
| `backend/Infrastructure/Orleans/State/StateStorage.cs` | Чтение state из PostgreSQL |
| `backend/Infrastructure/Orleans/State/StatesRegistry.cs` | Реестр зарегистрированных state типов |
| `backend/Console/Pages/Debug/StateExplorer.razor` | Новая страница [новый файл] |
| `backend/Console/Common/ConsoleConstants.cs` | Роут |

### Документация к прочтению
- `rules/ORLEANS_STATE.md` — StatesLookup, state structure
- `rules/BLAZOR.md` — формы, error handling

### Риски
- **Security**: отображает raw данные из БД. Только для dev/admin
- **Десериализация**: raw JSON из PostgreSQL может содержать internal поля Orleans serializer. Для MVP показывать как есть
- **Типизация**: чтение generic `Read<TKey, TState>` требует знание типов в compile-time. Альтернатива — прямой SQL с raw JSON
