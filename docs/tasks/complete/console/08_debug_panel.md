## Задача: Debug Panel — читы для активных матчей через консоль

### Цель
Создать страницу для отправки debug-команд в активные матчи: добавить карту, изменить здоровье/ману/ходы, завершить матч. Полезно для тестирования карт и отладки без клиента.

### Контекст
`CheatCommands` уже существуют в `backend/Game/GamePlay/Commands/`: `CardAdd`, `CardDiscard`, `ChangeMana`, `ChangeHealth`, `ChangeMaxMana`, `ChangeMaxHealth`, `ChangeMoves`, `EndMatch`. Они зарегистрированы в CommandsCollection и доступны через CommandDispatcher. Нужен UI для отправки этих команд.

### Шаги реализации

**1. Зависимость от задачи #1 (Live Matches Monitor)**
  1.1. Нужен способ получить список активных сессий и их участников
  1.2. Нужен способ отправить команду в конкретную сессию конкретному игроку

**2. Создать API для отправки cheat-команд**
  2.1. Endpoint в Game Gateway: `POST /api/debug/session/{sessionId}/player/{playerIndex}/command` — принимает тип команды и параметры
  2.2. Реализация: найти сессию в `SessionsCollection`, получить `IUser` по playerIndex, отправить команду через `CommandDispatcher`
  2.3. Или: отдельный grain `IDebugGrain` доступный из Console

**3. Создать Blazor страницу**
  3.1. Создать `backend/Console/Pages/Debug/DebugPanel.razor` [новый файл — добавить в Console.csproj]
  3.2. Route: `/debug`
  3.3. Step 1: Выбрать активную сессию (dropdown из Live Matches)
  3.4. Step 2: Выбрать игрока (Player 1 / Player 2)
  3.5. Step 3: Выбрать команду (dropdown: AddCard, ChangeHealth, ChangeMana, ChangeMoves, EndMatch)
  3.6. Step 4: Параметры (зависят от команды): CardType dropdown, число для health/mana/moves
  3.7. Кнопка "Execute" + ToastService feedback

**4. Интеграция**
  4.1. Добавить роут в `ConsoleConstants.Pages`
  4.2. НЕ добавлять в основную навигацию — только через Home или прямой URL (dev tool)

### Ключевые файлы

| Файл | Роль в задаче |
|------|---------------|
| `backend/Game/GamePlay/Commands/CheatCommands.cs` | Существующие команды — CardAdd, ChangeHealth и др. |
| `backend/Game/Session/Users/CommandDispatcher.cs` | Механизм отправки команд в сессию |
| `backend/Game/Global/SessionsCollection.cs` | Получение сессии по Id |
| `backend/Console/Pages/Debug/DebugPanel.razor` | Новая страница [новый файл] |

### Документация к прочтению
- `rules/BLAZOR.md` — формы, error handling
- `docs/GAMEPLAY.md` — CardType enum, game flow

### Риски
- **Зависит от #1**: нужен список активных сессий
- **Security**: debug panel не должен быть доступен в production. Нужен feature flag или auth guard
- **Cross-gateway**: команды нужно отправлять в Game Gateway, Console — отдельный процесс. Нужен HTTP endpoint или messaging
- **Timing**: команда может прийти в неподходящий момент (между ходами, во время завершения). CheatCommands должны обрабатывать это gracefully
