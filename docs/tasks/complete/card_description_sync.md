## Синхронизация описаний карт

### Что сделано
- В `CardConfigOptions` добавлены 22 интерфейса эффектов (`IDurationalCardConfig`, `IDamageCardConfig`, …) и применены ко всем картам.
- Числа в `cards-info.json` заменены маркап-токенами (`{ROUNDS}`, `{DAMAGE}`, `{SIZE}`, …).
- `CardDescriptionProvider` резолвит токены из конфигов; UI читает `GetDescription(CardType)`.
- Документация всех эффектов и токенов — `.agents/docs/CARD_EFFECTS.md`.

### Ключевые файлы
- `shared/Configs/CardConfigOptions.cs`
- `client/Assets/Meta/Cards/CardDescriptionProvider.cs`
- `client/Assets/Resources/cards-info.json`
- `.agents/docs/CARD_EFFECTS.md`

### Заметки
- `CardDescriptionProvider` сам грузит JSON — не зависит от `CardsRegistry` (иначе цикл DI).
- Дублирующие свойства `Duration` удалены, остался `TurnsDuration`.
