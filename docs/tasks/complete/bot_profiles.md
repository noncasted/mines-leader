## Разные профили для ботов (Easy/Medium/Hard)

### Что сделано
- Созданы shared модели: `BotProfile` enum, `BotProfileConfig` с `List<BotDeck>`, расширен `BotConfigOptions` со словарем профилей.
- Backend: `IBotProfileStrategy` + `BotProfileBase` + `EasyBotProfile`/`MediumBotProfile`/`HardBotProfile` + `BotProfileStrategyProvider`.
- `BotRunner` — тонкий делегатор, выбирает профиль и вызывает `ExecuteTurn`.
- `BotFlagAction` — только constraint-solving (Level-1/Level-2), без direct scan.
- `BotCellAction` — только proven-safe через constraint-solving, без random fallback.
- `BotCardAction` — utility-based + фильтрация по `CardPool` из профиля.
- `BotFactory` — выбирает случайную деку из профиля при создании бота.
- Console: `BotConfigEditor.razor` с табами Easy/Medium/Hard + `BotDeckBuilder.razor` + `BotDeckEditor.razor`.
- `UserDeckConfigOptions` + `UserDeckConfigState` — конфигурация дек для пользователей.

### Ключевые файлы
| Файл | Роль |
|------|------|
| `shared/Configs/BotProfile.cs` | Enum профилей |
| `shared/Configs/BotProfileConfig.cs` | Настройки профиля + список дек |
| `shared/Configs/BotDeck.cs` | Имя деки + список карт |
| `shared/Configs/BotConfigOptions.cs` | Словарь профилей + текущий профиль |
| `backend/Game/GamePlay/Bot/Profiles/IBotProfileStrategy.cs` | Интерфейс профиля |
| `backend/Game/GamePlay/Bot/Profiles/BotProfileBase.cs` | Базовый класс с хелперами |
| `backend/Game/GamePlay/Bot/BotRunner.cs` | Делегатор хода |
| `backend/Console/Game/Configs/BotConfigEditor.razor` | Редактор профилей |
| `backend/Console/Game/Configs/BotDeckEditor.razor` | Редактор деки |
| `backend/Meta/Bots/BotFactory.cs` | Выбор деки из профиля |

### Заметки
- Циклическая зависимость DI (`BotProfileStrategyProvider` → профили → `BotFlagAction` → провайдер) устранена через прямое чтение `IBotConfig.CurrentProfile`.
- `UserDeck.Update` валидирует `HasCard` — бот должен владеть картами до `handle.Deck.Update`.
- Newtonsoft.Json сериализует `Dictionary<BotProfile, BotProfileConfig>` с enum-ключами как строки.
