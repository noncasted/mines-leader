## Разные профили для ботов — Результат

### Статус: Завершено

### Что сделано
1. Создан enum `BotProfile` (Easy, Medium, Hard) в `shared/Configs/BotProfile.cs`.
2. Создан `BotProfileConfig` с полями профиля + `HashSet<CardType> CardPool`.
3. `BotConfigOptions` переработан: `MatchmakingApplyThreshold` оставлен глобальным, добавлен `CurrentProfile` и `Dictionary<BotProfile, BotProfileConfig> Profiles` с дефолтами.
4. `IBotProfileStrategy` — `ConstraintDepth` (1/2) + `CardPool` + `ExecuteTurn`. Убраны CanUseMineKnowledge/CanUseConstraintFlagging.
5. `BotProfileBase` — базовый класс с общими хелперами хода.
6. Все профили: порядок Flags → Cards → Cells (все понимают мету). Разница в глубине constraint-solving, лимитах и пуле карт.
7. `BotFlagAction` — только constraint-solving (Level-1/Level-2). Нет direct scan.
8. `BotCellAction` — только proven-safe через constraint-solving. Нет random fallback.
9. `BotCardAction` — utility-based для всех профилей. Фильтрация по `CardPool` из конфига (пустой = все карты).
10. `BotRunner` — тонкий делегатор, выбирает профиль и вызывает `ExecuteTurn`.
11. Console: `BotConfigEditor.razor` — табы Easy/Medium/Hard с числовыми полями.
12. Console: `BotCardPoolEditor.razor` — грид тайлов всех карт, клик toggles зеленую обводку (в пуле / не в пуле).
13. Console: `Configs.razor` — секция "Bot Global Settings" с `MatchmakingApplyThreshold` и выбором `CurrentProfile`.
14. `config.bot.json` обновлен под новую структуру.

### Измененные файлы

| Файл | Что изменено |
|------|-------------|
| `shared/Configs/BotProfile.cs` | [новый] Enum профилей |
| `shared/Configs/BotProfileConfig.cs` | [новый] Настройки профиля + CardPool |
| `shared/Configs/BotConfigOptions.cs` | Расширен профильной структурой |
| `backend/Game/GamePlay/Bot/Profiles/IBotProfileStrategy.cs` | [новый] ConstraintDepth + CardPool + ExecuteTurn |
| `backend/Game/GamePlay/Bot/Profiles/BotProfileBase.cs` | [новый] Базовый класс с хелперами хода |
| `backend/Game/GamePlay/Bot/Profiles/EasyBotProfile.cs` | [новый] ConstraintDepth=1, пул 5 карт |
| `backend/Game/GamePlay/Bot/Profiles/MediumBotProfile.cs` | [новый] ConstraintDepth=2, пул 17 карт |
| `backend/Game/GamePlay/Bot/Profiles/HardBotProfile.cs` | [новый] ConstraintDepth=2, пустой пул |
| `backend/Game/GamePlay/Bot/Profiles/BotProfileStrategyProvider.cs` | [новый] Фабрика стратегий |
| `backend/Game/GamePlay/Bot/BotServiceExtensions.cs` | Регистрация профильных сервисов |
| `backend/Game/GamePlay/Bot/Actions/BotFlagAction.cs` | Только constraint-solving (Level-1/Level-2) |
| `backend/Game/GamePlay/Bot/Actions/BotCellAction.cs` | Только proven-safe через constraint-solving |
| `backend/Game/GamePlay/Bot/Actions/BotCardAction.cs` | Utility-based для всех + фильтрация CardPool |
| `backend/Game/GamePlay/Bot/BotRunner.cs` | Тонкий делегатор: выбор профиля + ExecuteTurn |
| `backend/Console/Game/Configs/BotCardPoolEditor.razor` | [новый] Грид тайлов карт с toggle обводкой |
| `backend/Console/Game/Configs/BotConfigEditor.razor` | Табы + числовые поля + CardPool редактор |
| `backend/Console/Game/Configs/Configs.razor` | Глобальные настройки ботов + выбор профиля |
| `backend/Orchestration/Coordinator/config.bot.json` | Новая структура JSON с CardPool |

### Отличия от плана
- Архитектура профилей изменилась: вместо capability-флагов используется `ConstraintDepth` и `CardPool` из конфига.
- `BotConfigOptions` использует `Dictionary<BotProfile, BotProfileConfig>` — Newtonsoft.Json сериализует enum-ключи как строки.

### Нерешенные вопросы
- Нет
