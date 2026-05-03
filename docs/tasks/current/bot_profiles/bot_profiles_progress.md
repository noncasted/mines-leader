## Разные профили для ботов — Рабочие заметки

### Статус: Завершено

### Выполнено
- [x] Созданы shared модели: `BotProfile` enum, `BotProfileConfig`, `BotDeck`, расширен `BotConfigOptions`
- [x] Backend: `IBotProfileStrategy` + `BotProfileBase` + `EasyBotProfile`/`MediumBotProfile`/`HardBotProfile` + `BotProfileStrategyProvider`
- [x] `BotRunner` — тонкий делегатор, выбирает профиль и вызывает `ExecuteTurn`
- [x] `BotFlagAction` — constraint-solving Level-1 (все) + Level-2 (Medium/Hard), без direct scan
- [x] `BotCellAction` — proven-safe через constraint-solving, без random fallback
- [x] `BotCardAction` — utility-based для всех профилей
- [x] `BotFactory` — выбирает случайную деку из профиля при создании бота, подгоняет под `DeckSize`
- [x] `GameFlow` — логирует профиль бота в логи сессии (`LogBotProfile`)
- [x] Console UI: `BotConfigEditor.razor` — табы Easy/Medium/Hard с числовыми полями + DeckBuilder
- [x] Console UI: `BotDeckBuilder.razor` — список дек, Add, Delete (мусорка при hover), Edit
- [x] Console UI: `BotDeckEditor.razor` — имя деки + грид карт (зеленая обводка toggle), валидация count == DeckSize
- [x] Console UI: `Configs.razor` — Bot Global Settings с Matchmaking Threshold
- [x] `DeckOptions.BotPool` удален, тесты обновлены под новую логику
- [x] `config.bot.json` — дефолтные деки: Easy (1), Medium (2), Hard (10)
- [x] Циклическая зависимость DI (IBotProfileStrategyProvider → профили → BotFlagAction → провайдер) устранена через прямое чтение `IBotConfig.CurrentProfile`
- [x] Сборка: полный backend — Build succeeded, 0 errors

### Ключевые файлы (измененные/созданные)
| Файл | Роль |
|------|------|
| `shared/Configs/BotProfile.cs` | [новый] Enum Easy/Medium/Hard |
| `shared/Configs/BotProfileConfig.cs` | [новый] Настройки профиля + `List<BotDeck> Decks` |
| `shared/Configs/BotDeck.cs` | [новый] `Name` + `List<CardType> Cards` |
| `shared/Configs/BotConfigOptions.cs` | Расширен профильной структурой |
| `backend/Game/GamePlay/Bot/Profiles/IBotProfileStrategy.cs` | [новый] `ConstraintDepth` + `ExecuteTurn` |
| `backend/Game/GamePlay/Bot/Profiles/BotProfileBase.cs` | [новый] Базовый класс с хелперами хода |
| `backend/Game/GamePlay/Bot/Profiles/EasyBotProfile.cs` | [новый] ConstraintDepth=1 |
| `backend/Game/GamePlay/Bot/Profiles/MediumBotProfile.cs` | [новый] ConstraintDepth=2 |
| `backend/Game/GamePlay/Bot/Profiles/HardBotProfile.cs` | [новый] ConstraintDepth=2 |
| `backend/Game/GamePlay/Bot/Profiles/BotProfileStrategyProvider.cs` | [новый] Фабрика стратегий |
| `backend/Game/GamePlay/Bot/BotServiceExtensions.cs` | Регистрация профильных сервисов |
| `backend/Game/GamePlay/Bot/Actions/BotFlagAction.cs` | Constraint-solving, без провайдера |
| `backend/Game/GamePlay/Bot/Actions/BotCellAction.cs` | Constraint-solving, без провайдера |
| `backend/Game/GamePlay/Bot/Actions/BotCardAction.cs` | Utility-based |
| `backend/Game/GamePlay/Bot/BotRunner.cs` | Тонкий делегатор |
| `backend/Game/Session/Logging/ISessionLogger.cs` | + `LogBotProfile` |
| `backend/Game/Session/Logging/SessionFileLogger.cs` | + `LogBotProfile` |
| `backend/Game/GamePlay/Context/GameFlow.cs` | Логирование профиля бота |
| `backend/Meta/Bots/BotFactory.cs` | Выбор деки из профиля |
| `backend/Meta/Users/Decks/DeckOptions.cs` | Удален `BotPool` |
| `backend/Tools/Tests/Meta/BotTests.cs` | Обновлены под новую логику |
| `backend/Console/Game/Configs/BotConfigEditor.razor` | Табы + DeckBuilder |
| `backend/Console/Game/Configs/BotDeckBuilder.razor` | [новый] Список дек |
| `backend/Console/Game/Configs/BotDeckEditor.razor` | [новый] Редактор деки |
| `backend/Console/Game/Configs/Configs.razor` | Bot Global Settings |
| `backend/Orchestration/Coordinator/config.bot.json` | Дефолтные деки для всех профилей |

### Важные находки из сессии
1. **Циклическая зависимость DI**: `BotProfileStrategyProvider` → профили → `IBotFlagAction` → провайдер. Исправлено заменой `IBotProfileStrategyProvider` на прямое чтение `IBotConfig.CurrentProfile` в `BotFlagAction`/`BotCellAction`.
2. **Сериализация `Dictionary<BotProfile, BotProfileConfig>`**: Newtonsoft.Json корректно сериализует enum-ключи как строки.
3. **`BotFactory` и `UserCards.AddCard`**: при выборе деки из профиля нужно добавить все карты из деки через `handle.Cards.AddCard(card)`, иначе `UserDeck.ValidateCards` падает.
4. **`UserDeck.Update` валидирует карты**: проверяет `IUserCards.HasCard(card)` для каждой карты в деке — бот должен владеть картами до `handle.Deck.Update`.
5. **Старый `DeckOptions.BotPool`** — удален полностью, тесты переписаны на `IBotConfig.CurrentProfileConfig.Decks`.
