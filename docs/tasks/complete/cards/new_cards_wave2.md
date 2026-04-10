## Задача: Реализация 4 новых карт (Lockdown, Saboteur, Sonar, Purge)

### Цель
Добавить 4 новые карты в систему: Lockdown (уменьшение ходов противника), Saboteur (закрытие открытых клеток на поле противника), Sonar (авто-флаг мин в области), Purge (снятие вражеских эффектов со своего поля).

---

### Карта 1: Lockdown
> Мана: 3 | Цель: Opponent | Без позиции | Длительность: 2 раунда

Уменьшает `_maxTurns` противника на 1 на 2 раунда. По паттерну Siphon (owner + opponent) + Smoke (IRoundActionService для восстановления).

**Нужен новый интерфейс конфига** `ICardLockdownConfig` с `Duration` и `MovesReduction`, либо просто свойства в config-классе.

### Карта 2: Saboteur
> Мана: 4 | Цель: OpponentBoard | Паттерн: ромб (размер 3)

Закрывает до 5 открытых клеток (FreeCell -> TakenCell) в ромбе. По паттерну Bloodhound (board + pattern + payload), но использует `SelectFree` + `cell.ToTaken()` вместо `SelectTaken` + `cell.ToFree()`.

**Примечание:** Нужна механика запоминания истории ходов. Эту задачу можно вынести отдельно; базовую версию реализовать без истории (просто закрывает любые открытые клетки в области).

### Карта 3: Sonar
> Мана: 3 | Цель: OwnBoard | Паттерн: ромб (размер 4)

Ставит флаги на мины в области, не открывая клетки. По паттерну Bloodhound (board + pattern + payload), но использует `SelectTaken` + фильтр `HasMine` + `cell.SetFlag()`.

### Карта 4: Purge
> Мана: 2 | Цель: Self | Без позиции

Снимает все вражеские эффекты (Smoke, Fog) со всех клеток своего поля. По паттерну Medic (owner only), но итерирует `owner.Board.Cells` и вызывает `cell.RemoveEffect()`.

---

### Шаги реализации

#### Фаза 1: Shared (протокол и конфиги)

1. **Добавить CardType** — `shared/Domain/CardType.cs`
   - `Lockdown = 2200`
   - `Saboteur = 2300`
   - `Sonar = 2400`
   - `Purge = 2500`

2. **Добавить конфиги** — `shared/Configs/CardConfigOptions.cs`
   - MemoryPackUnion на `ICardConfig`: индексы 21-24
   - MemoryPackUnion на `ICardSizeConfig`: индексы 12-13 (Saboteur, Sonar)
   - Config-классы:
     - `Lockdown : ICardConfig` — ManaCost=3, Target=Opponent, Duration=2, MovesReduction=1
     - `Saboteur : ICardConfig, ICardSizeConfig` — ManaCost=4, Size=3, Target=OpponentBoard, MaxCells=5
     - `Sonar : ICardConfig, ICardSizeConfig` — ManaCost=3, Size=4, Target=OwnBoard
     - `Purge : ICardConfig` — ManaCost=2, Target=Self
   - Свойства в `CardConfigOptions`: `Lockdown_Normal`, `Saboteur_Normal`, `Sonar_Normal`, `Purge_Normal`
   - Записи в `All` словаре

3. **Добавить payload-классы** — `shared/Game/Cards/ICardUsePayload.cs`
   - MemoryPackUnion: индексы 21-24
   - `CardUsePayload.Lockdown : ICardUsePayload` (без позиции)
   - `CardUsePayload.Saboteur : IBoardCardUsePayload` (с позицией)
   - `CardUsePayload.Sonar : IBoardCardUsePayload` (с позицией)
   - `CardUsePayload.Purge : ICardUsePayload` (без позиции)

4. **Добавить snapshot-классы** — `shared/Game/Snapshots/CardActionSnapshotRecord.cs`
   - MemoryPackUnion: индексы 21-24
   - `CardActionSnapshot.Lockdown : ICardActionData`
   - `CardActionSnapshot.Saboteur : ICardActionData` + `IReadOnlyList<Position> ClosedCells`
   - `CardActionSnapshot.Sonar : ICardActionData` + `IReadOnlyList<Position> FlaggedCells`
   - `CardActionSnapshot.Purge : ICardActionData`

#### Фаза 2: Backend (логика карт)

5. **Lockdown.cs** — `backend/Game/GamePlay/Cards/Lockdown.cs` [новый файл — добавить в Game.csproj]
   - Конструктор: `IPlayer opponent, CardConfigOptions.Lockdown config, IRoundActionService roundActionService`
   - `Use()`: уменьшает `opponent.Moves.SetMax(current - reduction)`, планирует `LockdownDisposeAction` через `roundActionService.Schedule()`
   - `LockdownDisposeAction : IRoundAction` — восстанавливает `SetMax(original)`

6. **Saboteur.cs** — `backend/Game/GamePlay/Cards/Saboteur.cs` [новый файл — добавить в Game.csproj]
   - Конструктор: `IBoard target, CardConfigOptions.Saboteur config, CardUsePayload.Saboteur payload`
   - `Use()`: `PatternShapes.Rhombus(size).SelectFree(target, position)`, берёт до `MaxCells` клеток, вызывает `cell.ToTaken()` для каждой

7. **Sonar.cs** — `backend/Game/GamePlay/Cards/Sonar.cs` [новый файл — добавить в Game.csproj]
   - Конструктор: `IBoard target, CardConfigOptions.Sonar config, CardUsePayload.Sonar payload`
   - `Use()`: `PatternShapes.Rhombus(size).SelectTaken(target, position)`, фильтрует `HasMine && !IsFlagged`, вызывает `cell.SetFlag()`

8. **Purge.cs** — `backend/Game/GamePlay/Cards/Purge.cs` [новый файл — добавить в Game.csproj]
   - Конструктор: `IPlayer owner`
   - `Use()`: итерирует `owner.Board.Cells.Values`, для каждой клетки с эффектами — `RemoveEffect()` для всех

9. **CardFactory.cs** — `backend/Game/GamePlay/Cards/CardFactory.cs`
   - Добавить 4 case в switch: Lockdown, Saboteur, Sonar, Purge

#### Фаза 3: Backend (бот-стратегии)

10. **LockdownStrategy.cs** — `backend/Game/GamePlay/Bot/CardStrategies/LockdownStrategy.cs` [новый файл]
    - Приоритет: высокий когда у противника много ходов (аналог HandScrambleStrategy)

11. **SaboteurStrategy.cs** — `backend/Game/GamePlay/Bot/CardStrategies/SaboteurStrategy.cs` [новый файл]
    - Приоритет: высокий когда у противника много открытых клеток
    - Позиция: случайная среди открытых клеток

12. **SonarStrategy.cs** — `backend/Game/GamePlay/Bot/CardStrategies/SonarStrategy.cs` [новый файл]
    - Приоритет: высокий когда много закрытых клеток на своём поле
    - Позиция: случайная среди закрытых клеток

13. **PurgeStrategy.cs** — `backend/Game/GamePlay/Bot/CardStrategies/PurgeStrategy.cs` [новый файл]
    - Приоритет: высокий когда есть активные эффекты на своём поле, 0 если нет

14. **BotServiceExtensions.cs** — `backend/Game/GamePlay/Bot/BotServiceExtensions.cs`
    - Зарегистрировать 4 новые стратегии

#### Фаза 4: Backend (конфиг)

15. **config.cards.json** — `backend/Orchestration/Coordinator/config.cards.json`
    - Добавить записи для Lockdown_Normal, Saboteur_Normal, Sonar_Normal, Purge_Normal

#### Фаза 5: Client (действия карт)

16. **CardLockdownAction.cs** — `client/Assets/GamePlay/Cards/Entities/Actions/CardLockdownAction.cs` [новый файл]
    - По паттерну CardSiphonAction: ICardDropDetector, без позиции

17. **CardSaboteurAction.cs** — `client/Assets/GamePlay/Cards/Entities/Actions/CardSaboteurAction.cs` [новый файл]
    - По паттерну CardBloodhoundAction: ICardDropArea + Pattern (ромб), но SelectFree вместо SelectTaken

18. **CardSonarAction.cs** — `client/Assets/GamePlay/Cards/Entities/Actions/CardSonarAction.cs` [новый файл]
    - По паттерну CardBloodhoundAction: ICardDropArea + Pattern (ромб), SelectTaken

19. **CardPurgeAction.cs** — `client/Assets/GamePlay/Cards/Entities/Actions/CardPurgeAction.cs` [новый файл]
    - По паттерну CardMedicAction: ICardDropDetector, без позиции

20. **CardStatesExtensions.cs** — `client/Assets/GamePlay/Cards/Entities/States/CardStatesExtensions.cs`
    - Добавить 4 case в AddCardAction (регистрация + конфиг)
    - Добавить 4 case в AddCardActionSync (синхронизация)

#### Фаза 6: Client (метаданные)

21. **cards-info.json** — `client/Assets/Resources/cards-info.json`
    - Добавить 4 записи: type, name, description, icon

---

### Ключевые файлы

| Файл | Роль в задаче |
|------|---------------|
| `shared/Domain/CardType.cs` | Enum — добавить 4 новых типа |
| `shared/Configs/CardConfigOptions.cs` | MemoryPackUnion + config-классы + свойства + All словарь |
| `shared/Game/Cards/ICardUsePayload.cs` | MemoryPackUnion + payload-классы |
| `shared/Game/Snapshots/CardActionSnapshotRecord.cs` | MemoryPackUnion + snapshot-классы |
| `backend/Game/GamePlay/Cards/CardFactory.cs` | Switch — маппинг типов на реализации |
| `backend/Game/GamePlay/Cards/Siphon.cs` | Шаблон для Lockdown (owner + opponent) |
| `backend/Game/GamePlay/Cards/Bloodhound.cs` | Шаблон для Saboteur/Sonar (board + pattern) |
| `backend/Game/GamePlay/Cards/Medic.cs` | Шаблон для Purge (owner only) |
| `backend/Game/GamePlay/Cards/Smoke.cs` | Шаблон для Lockdown (IRoundActionService) |
| `backend/Game/GamePlay/Players/Moves.cs` | API для Lockdown: SetMax() |
| `backend/Game/GamePlay/Bot/BotServiceExtensions.cs` | Регистрация бот-стратегий |
| `backend/Orchestration/Coordinator/config.cards.json` | JSON-конфиг значений карт |
| `client/Assets/GamePlay/Cards/Entities/States/CardStatesExtensions.cs` | Маппинг клиентских action/sync |
| `client/Assets/GamePlay/Cards/Entities/Actions/CardSiphonAction.cs` | Шаблон для Lockdown/Purge (без позиции) |
| `client/Assets/GamePlay/Cards/Entities/Actions/CardBloodhoundAction.cs` | Шаблон для Saboteur/Sonar (с позицией) |
| `client/Assets/Resources/cards-info.json` | Метаданные карт для UI |

### Документация к прочтению
- `rules/CODE_STYLE.md` — порядок членов, именование, стиль скобок
- `rules/ORLEANS_GRAINS.md` — не применимо напрямую, но нужно не путать Orleans с VContainer в backend

### Риски
- **Lockdown: восстановление maxMoves** — `Moves.SetMax()` перезаписывает `_maxTurns` без сохранения предыдущего значения. `LockdownDisposeAction` должен сам запомнить оригинальное значение при создании. Если два Lockdown наложатся — нужно корректно стакать/восстанавливать.
- **Saboteur: ToTaken() на клетках с эффектами** — нужно проверить, что `ToTaken()` корректно сохраняет/очищает эффекты при конвертации FreeCell -> TakenCell.
- **Saboteur: пересчёт MinesAround** — после закрытия клеток нужен вызов `Board.MinesScanner` для обновления цифр на соседних открытых клетках. Проверить что `Board.OnUpdated()` триггерит пересчёт.
- **Purge: снятие только вражеских эффектов** — текущие эффекты (Smoke, Fog) накладываются только противником, но если в будущем появятся свои эффекты (баффы), Purge не должен их снимать. Пока можно снимать все.
- **MemoryPackUnion индексы** — в `ICardConfig` пропущены индексы 12, 14, 15. Использовать их или продолжить с 21+? Безопаснее 21+ чтобы не конфликтовать с удалёнными типами.
