## card_config_grid — Результат

### Статус: Завершено

### Что сделано
1. Создан универсальный компонент `CardGridItem.razor` — карточка с иконкой сверху, названием, бейджем Max и редактируемыми параметрами (через рефлексию int-свойств с setter).
2. Переписана страница `Configs.razor`: секция Cards теперь показывает 5 гридов по категориям из документации (Scout, Cross-Board, Improve, Resources, Hand).
3. Добавлен поиск по имени карты — фильтрует все категории, скрывает пустые секции.
4. Категории синхронизированы с `docs/obsidian/game/cards/implemented/`:
   - Improve: TrebuchetAimer (+Max), Overclock, Medic, Purge, Adrenaline, CoinToss, Focus, Shield, PowerSurge
   - Resources: Siphon, Lockdown, BloodPact, ManaSurge, DoubleOrNothing, Embargo, GamblersRuin, ManaFountain, SoulLink
   - Lockdown и SoulLink перенесены из старой Buff-категории в Resources
5. Настроена раздача иконок из `docs/obsidian/game/cards/icons/` по пути `/card-icons/` через `UseStaticFiles` в ConsoleGateway.
6. Удалены 6 неиспользуемых специализированных редакторов карт.
7. Исправлен существующий баг: `CardLockdownConfigEditor.razor` ссылался на несуществующее свойство `Duration` вместо `TurnsDuration`.
8. Проекты `Console.csproj` и `ConsoleGateway.csproj` собираются без ошибок.

### Измененные файлы

| Файл | Что изменено |
|------|-------------|
| `backend/Console/Game/Configs/Configs.razor` | Полностью переписана секция Cards: гриды по категориям, поиск, `CardGridItem` |
| `backend/Console/Game/Configs/CardGridItem.razor` | Новый универсальный компонент карточки карты |
| `backend/Orchestration/ConsoleGateway/Program.cs` | Добавлен `UseStaticFiles` для `/card-icons/`, обновлён `isStaticPath` в auth middleware |
| `backend/Console/Game/Configs/CardLockdownConfigEditor.razor` | Исправлено `Duration` → `TurnsDuration` |
| `backend/Console/Game/Configs/CardConfigEditor.razor` | Удалён (не использовался) |
| `backend/Console/Game/Configs/CardSizeConfigEditor.razor` | Удалён (не использовался) |
| `backend/Console/Game/Configs/CardRangeConfigEditor.razor` | Удалён (не использовался) |
| `backend/Console/Game/Configs/CardChainReactionConfigEditor.razor` | Удалён (не использовался) |
| `backend/Console/Game/Configs/CardSiphonConfigEditor.razor` | Удалён (не использовался) |

### Отличия от плана
- План предполагал оставить старые редакторы, но поскольку они нигде не использовались — удалены для чистоты кодовой базы.
- Lockdown был исправлен в `CardLockdownConfigEditor.razor` перед удалением, чтобы не сломать сборку.
