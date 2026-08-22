## card_config_grid

### Что сделано
- Секция Cards в консоли — 5 гридов по категориям (Scout, Cross-Board, Improve, Resources, Hand) с поиском по имени.
- Универсальный `CardGridItem.razor`: иконка, название, бейдж Max, int-поля через рефлексию.
- Иконки раздаются из `docs/obsidian/game/cards/icons/` по `/card-icons/`.
- Удалены 6 неиспользуемых специализированных редакторов карт.

### Ключевые файлы
- `backend/Console/Game/Configs/Configs.razor`
- `backend/Console/Game/Configs/CardGridItem.razor`
- `backend/Orchestration/ConsoleGateway/Program.cs`

### Заметки
- Lockdown и SoulLink — категория Resources (не Buff).
- `_Max` карты показывают иконку базового имени без суффикса.
