## Разные профили для ботов (Easy/Medium/Hard)

### Что сделано
- Созданы shared модели: `BotProfile` enum, `BotProfileConfig`, расширен `BotConfigOptions` с `Dictionary<BotProfile, BotProfileConfig>`.
- Созданы backend стратегии: `IBotProfileStrategy`, `EasyBotProfile`, `MediumBotProfile`, `HardBotProfile`, `BotProfileStrategyProvider`.
- Зарегистрированы в DI через `BotServiceExtensions`.
- Переработан `BotRunner` на фазовый подход (Flags → Cards → Cells).

### Что НЕ сделано
- Стратегии не интегрированы в `BotFlagAction`/`BotCellAction` — профильная логика (`CanUseMineKnowledge`) не проброшена.
- Консоль: нет табов Easy/Medium/Hard, нет секции глобальных настроек.
- Файлы не закоммичены (только в рабочей копии).

### Ключевые файлы
| Файл | Роль |
|------|------|
| `shared/Configs/BotProfile.cs` | Enum профилей |
| `shared/Configs/BotProfileConfig.cs` | Настройки профиля |
| `shared/Configs/BotConfigOptions.cs` | Словарь профилей |
| `backend/Game/GamePlay/Bot/Profiles/IBotProfileStrategy.cs` | Интерфейс |
| `backend/Game/GamePlay/Bot/Profiles/BotProfileStrategyProvider.cs` | DI provider |
| `backend/Game/GamePlay/Bot/BotRunner.cs` | Фазовый ход (без использования профилей) |
| `backend/Game/GamePlay/Bot/Actions/BotFlagAction.cs` | Direct scan (без учета профиля) |

### Заметки
- Рабочая копия содержит untracked файлы профилей и modified BotRunner/BotConfigOptions.
- Для завершения нужно: пробросить `IBotProfileStrategy` в actions, добавить консольные табы, проверить сериализацию Dictionary<enum, object>.
