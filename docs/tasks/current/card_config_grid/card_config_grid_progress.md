## card_config_grid — Рабочие заметки

### Статус: В работе

### Заметки

### [2026-05-19] Начало реализации
- Создан новый компонент `CardGridItem.razor` — универсальная карточка карты с иконкой, названием и редактируемыми параметрами через рефлексию.
- Переписана секция Cards в `Configs.razor`: теперь 5 гридов по категориям из доков (Scout, Cross-Board, Improve, Resources, Hand), поиск по имени, скрытие пустых категорий.
- Категории синхронизированы с `docs/obsidian/game/cards/implemented/`:
  - TrebuchetAimer перенесён из Cross-Board в Improve
  - Lockdown перенесён из Buff в Resources
- Настроен `UseStaticFiles` в `ConsoleGateway/Program.cs` для отдачи иконок по `/card-icons/`.
- Исправлен существующий баг в `CardLockdownConfigEditor.razor`: `Value.Duration` → `Value.TurnsDuration`.
- Удалены 6 неиспользуемых специализированных редакторов карт.

### [2026-05-19] Сборка
- `Console.csproj` — 0 errors, 0 warnings (кроме NU1900 по сети).
- `ConsoleGateway.csproj` — 0 errors, 0 warnings.
