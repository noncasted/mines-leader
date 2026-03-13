## Задача: Перепись документации GAMEPLAY.md под специфику проекта

### Цель
Заменить унаследованную документацию (level editor, dialogue system, animation schemes) актуальной документацией по реальным системам Mines Leader: game flow, board, cards, snapshot sync, bot AI.

### Шаги реализации
1. Перепись GAMEPLAY.md — `.claude/docs/GAMEPLAY.md`
2. Обновление CLAUDE.md — добавить секцию о структуре client/backend/shared, обновить ключевое слово-поиск для GAMEPLAY.md — `.claude/CLAUDE.md`

### Ключевые файлы

| Файл | Роль в задаче |
|------|---------------|
| `.claude/docs/GAMEPLAY.md` | Основной перезаписанный файл — теперь описывает реальные системы проекта |
| `.claude/CLAUDE.md` | Обновлён: структура проекта (client/backend/shared), новые ключевые слова |

### Результат
- `GAMEPLAY.md` теперь содержит: game flow, board system (flood-fill algorithm), card system (ICard, CardUseResult, все CardType), snapshot sync protocol, bot system (BotRunner, IBotCardStrategy, BotBoardUtils), matchmaking flow, key files reference
- `CLAUDE.md` теперь содержит: описание трёх кодовых баз (client/backend/shared), правило 8 о разделении Unity/Orleans паттернов, обновлённое ключевое слово для GAMEPLAY.md
