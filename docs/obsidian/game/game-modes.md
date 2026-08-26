# Игровые режимы

Три режима определены в `GameMatchType`:

## Сравнение режимов

| Параметр | Single | TimeLimited | LastManStanding |
|----------|--------|-------------|-----------------|
| **Тип** | PvE (против бота) | PvP | PvP |
| **Таймер** | Нет | 120с банк на игрока | 30с на раунд |
| **Бонус времени** | -- | +5с за действие | -- |
| **HP** | 3 | 3 | 3 |
| **Ходов за раунд** | 5 | 5 | 5 |
| **Стартовая мана** | 1 | 1 | 1 |
| **Потолок маны** | 10 | 10 | 10 |
| **Рука / Колода** | 5 / 10 | 5 / 10 | 5 / 10 |
| **Поле** | 16x16, 40 мин | 16x16, 40 мин | 16x16, 40 мин |
| **Победа по флагам** | Да | После 2 раундов | После 2 раундов |

Подробное описание каждого PvP-режима вынесено в отдельные заметки:

- [[time-limited|TimeLimited]] — личный банк времени, пополняемый действиями
- [[last-man-standing|LastManStanding]] — общий таймер раунда, рост маны

Английский текст карточек выбора режима и промпт иконок: в каждой заметке секция **Menu card**, общий рецепт — [[modes/mode-icons|Mode icons]].

---

## Single (Training)

Режим тренировки против AI-бота. Не регистрирует специального `IGameRound` — используется для практики.

- Всегда создаётся через `MatchFactory.CreateWithBot()`
- Тип матча автоматически устанавливается в `GameMatchType.Single`
- Нет ограничений по времени

---

## TimeLimited

Основной PvP-режим: у каждого игрока свой банк из 120 секунд на весь матч, каждое открытие клетки или использование карты даёт +5 секунд. Кончилось время — проигрыш.

Полное описание: [[time-limited|TimeLimited]].

---

## LastManStanding

PvP-режим с таймером в 30 секунд на каждый ход и без бонусов времени. Максимальная мана растёт на +1 в конце каждого своего хода до потолка в 10.

Полное описание: [[last-man-standing|LastManStanding]].

---

## Конфигурация

Параметры режимов хранятся в `config.gameMode.json` и загружаются через `GameModeOptions`:

```json
{
  "LastManStanding": {
    "PlayerHealth": 3,
    "PlayerMoves": 5,
    "PlayerStartMana": 4,
    "MaxManaCap": 10,
    "RoundTime": 30,
    "CardMovesCost": 0
  },
  "TimeLimited": {
    "PlayerHealth": 3,
    "PlayerMoves": 5,
    "PlayerStartMana": 4,
    "MaxManaCap": 10,
    "RoundTime": 120,
    "TimeGainPerAction": 5,
    "CardMovesCost": 0
  }
}
```

Список режимов, доступных в матчмейкинге, хранится в `config.matchMaking.json` и загружается через `MatchMakingOptions`. На клиент уходит той же projection, что и остальные конфиги:

```json
{
  "Available": [20, 30]
}
```

`20` — TimeLimited, `30` — LastManStanding.

Параметры игрока (размер руки и колоды) хранятся в `config.player.json` и загружаются через `PlayerConfigOptions`:

```json
{
  "HandSize": 5,
  "DeckSize": 10
}
```

## Ключевые файлы

| Файл | Описание |
|------|----------|
| `shared/Domain/GameMatchType.cs` | Enum режимов |
| `shared/Configs/GameModeOptions.cs` | Параметры режимов |
| `shared/Configs/MatchMakingOptions.cs` | Доступные для матчмейкинга режимы |
| `shared/Configs/PlayerConfigOptions.cs` | Параметры игрока |
| `backend/Game/GamePlay/Context/Rounds/TimeLimitedRound.cs` | Реализация TimeLimited |
| `backend/Game/GamePlay/Context/Rounds/LastManStandingRound.cs` | Реализация LastManStanding |
| `backend/Game/Global/SessionFactory.cs` | Выбор режима при создании сессии |
| `backend/Orchestration/Coordinator/config.gameMode.json` | JSON-конфиг режимов |
| `backend/Orchestration/Coordinator/config.matchMaking.json` | JSON-конфиг доступных режимов матчмейкинга |
| `backend/Orchestration/Coordinator/config.player.json` | JSON-конфиг игрока |
