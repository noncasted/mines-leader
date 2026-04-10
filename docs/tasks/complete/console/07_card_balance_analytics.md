## Задача: Card Balance Analytics — статистика по картам для баланса

### Цель
Собирать и отображать статистику использования карт: win rate деков с конкретными картами, частота использования, средний mana efficiency. Помогает принимать решения по балансу.

### Контекст
Сейчас никакой статистики по картам не собирается. `Match.OnComplete()` записывает `MatchState` с деками участников и победителем. Для аналитики нужно:
1. Трекать использование карт в бою (сколько раз сыграна каждая карта)
2. Трекать win rate деков содержащих конкретную карту

### Шаги реализации

**1. Добавить трекинг использования карт в матче**
  1.1. Создать счетчик `CardUsageTracker` — per-player, per-match dictionary `CardType -> int` (количество использований)
  1.2. Инкрементировать в `CardUseCommand` при успешном использовании карты
  1.3. Передать в `Match.OnComplete()` вместе с результатами

**2. Создать storage для агрегированной статистики**
  2.1. Создать `CardAnalyticsState` — `backend/Meta/Matches/Analytics/CardAnalyticsState.cs` [новый файл]
    ```
    - PerCard: Dictionary<CardType, CardStats>
    - CardStats: { int TimesInDeck, int TimesUsed, int Wins, int Losses }
    ```
  2.2. Обновлять при каждом OnComplete: для каждой карты в деках участников — инкрементировать TimesInDeck, Wins/Losses; добавить TimesUsed из трекера

**3. Создать grain или сервис для аналитики**
  3.1. Вариант A: `ICardAnalytics` grain с `State<CardAnalyticsState>` — обновляется через side effect после матча
  3.2. Вариант B: Прямые SQL-агрегации на основе MatchCollection (если задача #6 реализована)

**4. Создать Blazor страницу**
  4.1. Создать `backend/Console/Pages/Analytics/CardAnalytics.razor` [новый файл — добавить в Console.csproj]
  4.2. Route: `/analytics/cards`
  4.3. Таблица карт: CardType, Times in Deck, Times Used, Win Rate (%), Avg Uses per Match
  4.4. Сортировка по любой колонке
  4.5. Фильтр по периоду (опционально)

**5. Интеграция**
  5.1. Добавить роут в `ConsoleConstants.Pages`
  5.2. Добавить карточку в `HomeGameSection.razor`

### Ключевые файлы

| Файл | Роль в задаче |
|------|---------------|
| `backend/Game/GamePlay/Commands/CardUseCommand.cs` | Точка трекинга использования карт |
| `backend/Meta/Matches/Match.cs` | OnComplete — запись статистики |
| `backend/Meta/Matches/Analytics/CardAnalyticsState.cs` | Новый state [новый файл] |
| `backend/Console/Pages/Analytics/CardAnalytics.razor` | Новая страница [новый файл] |
| `shared/Configs/CardConfigOptions.cs` | Справочник всех типов карт |

### Документация к прочтению
- `rules/ORLEANS_STATE.md` — State<T> registration
- `rules/ORLEANS_GRAINS.md` — grain pattern
- `rules/BLAZOR.md` — таблицы, сортировка

### Риски
- **Объем изменений**: затрагивает Game session (трекинг) + Meta (агрегация) + Console (UI) — большая задача
- **Производительность**: агрегация per-card при каждом матче — легкая операция, но grain может стать hotspot. Side effect буферизует нагрузку
- **Зависимость от #6**: если Match History Browser (#6) реализован, можно считать аналитику из коллекции матчей вместо отдельного state
- **Retroactive data**: статистика будет только для новых матчей. Старые не учтены
