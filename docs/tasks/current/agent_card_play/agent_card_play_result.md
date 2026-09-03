---
task: agent_card_play
updated: 2026-09-03
---

# Agent Card Play — результат

## Что получил агент

- **Observation** (`Self.Hand[*]`): `Target`, `Shape`, `Size`, `NeedsPosition`, `Summary`. Источник — `shared/Game/Agent/AgentCardCatalog.cs`. Рука противника без oracle по-прежнему `Type = "?"`, новые поля пустые.
- **Legal plays** для любой карты с позицией: клетки считаются по форме из каталога, размеру из конфига и типу клеток (`Taken` / `Free` / `Any`), которым карта пользуется в `Use`. Карты без позиции — `NeedsPosition=false`, `Error` пустой. Единственная возможная ошибка правил — `No target rule for {type}`, и тест гарантирует, что для текущих `CardType` она не возникает.
- **Solver**: `scout_targets` (своё поле) и `attack_targets` (поле врага, метрики `closed` и `open`), `--shape` / `--size`.
- **CLI**: `diff` (что изменила карта), `stop`, фикстура в `start` / `ensure-play` через `EditorPrefs` → `GameMock.ReadFixture`.
- **Skill**: карты решаются до сапёра, каждая карта — строка `Type: play @x,y — reason` / `Type: skip — reason`, правило маны дословно, отчёт с секцией `Cards`.

## Как проверить

```bash
dotnet build backend/Tools/Tests/Tests.csproj
backend/Tools/Tests/bin/Debug/net10.0/Tests --filter-class "*AgentLegalPlays*" --filter-class "*AgentCardCatalog*" --filter-class "*AgentObservationBuilder*"
python3 tools/scripts/board-solver.py --self-test
python3 tools/scripts/game-agent.py start --scenario cross_vs_hard   # Unity + cluster
python3 tools/scripts/game-agent.py state                            # Self.Hand == [Trebuchet, OpponentBomb]
python3 tools/scripts/game-agent.py use-card --type Trebuchet --x 2 --y 5
python3 tools/scripts/game-agent.py diff
```

## Известные ограничения

- `ChainReaction` требует мину под кликом; legal plays отдают все закрытые клетки врага (мины не текут). Подсказка в `Summary`: целиться во флаги врага.
- `OpponentBomb` по контракту не предлагает флагнутые клетки, хотя `Use` их принимает.
- `attack_targets` — плотность клеток, не вероятность мины. Легальность подтверждает `legal-plays`.
- Live-прогон в Unity в этой сессии не выполнялся.
