## Agent Play

### Что сделано
- Добавлен матч `GameMatchType.LastManStandingTurnBased = 31`: копия LMS без `TimerCountdown`, клиент переиспользует `LastManStandingRound`.
- Сервер собирает `SharedAgentObservation` (player-visible доска, скрытый opponent hand, event buffer) и шлёт one-way после action / turn_start / opponent_turn / game_over.
- Unity MCP-инструменты (`game_open`, `game_flag`, `game_use_card`, `game_end_turn`, …) ходят через `GameAgentBridge`; off-turn Open/Chord/UseCard/EndTurn отклоняются на клиенте.

### Ключевые файлы
- `backend/Game/GamePlay/Context/Rounds/LastManStandingTurnBasedRound.cs`
- `shared/Game/Agent/` — observation DTO + `CardUsePayloadFactory`
- `client/Assets/GamePlay/Agent/GameAgentBridge.cs` + `GameAgentMcpTools.cs`

### Заметки
- Не live-PvP: режим не в продакшн-меню. Oracle (`IncludeOracle`) только для `game_get_state(oracle=true)`.
- `AgentObservationPublisher` не инжектит `IGameRound` в ctor (цикл с turn-based round); current player читается через `IServiceProvider`.
- `EndTurn` игнорирует echo `Trigger == "action"` и ждёт `opponent_turn` / `turn_start` / `game_over`.
