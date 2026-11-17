## Телеметрия клиента, метрики матча и история матчей (перенос из pixel-swarm)

### Что сделано
- Клиентская телеметрия (модуль фреймворка `telemetry`): сессии `menu` / `match` в `GameLoop` (первая `menu` - после авторизации меты), трейс стартапа после каждой авторизации меты (`MetaLoop`), тайминги кадра, спаны WebGL-шаблона; приём на MetaGateway, сессии в Silo, флаг на Features консоли, чтение `/api/client-telemetry` под `CONSOLE_TOKEN`.
- Метрики матча (модуль `match-metrics`): общие часы матча - `SharedMoveSnapshot.MatchMs` (сервер, `MatchClock`, штамп в `SnapshotSender.Send`); клиентские писатели в `client/Assets/GamePlay/Metrics` (ходы, клетки click/cascade/card, флаги, мины, карты, итог); серверный поток `SessionMetrics`; admission по записи матча; таблицы `match_metric_*` в DeploySetup; `/api/match-metrics`; Grafana dev на 9434.
- История матчей: расширенный `IMatch` (ники, `ClosedAt`, закрытие без победителя и по концу сессии), коллекция заголовков, `/api/match-history`, консоль `/match-history`, `/match/{id}`, `/match/{id}/players/{userId}`; при деплое удаляются мёртвые записи старого формата (`DeadMatchRecordsCleanup`).
- Агент читает всё сам: скиллы `client-telemetry` (симлинк фреймворка) и `match-history`; описание в `.claude/docs/TELEMETRY.md`.
- Задеплоено 2026-11-17: коммиты 92c49429, d0428420, c4abf0a6; протокол f1459c107e28ee7f; web-тег 20261117-191802-c4abf0a6.

### Ключевые файлы
- `.claude/docs/TELEMETRY.md` - карта всего: что пишется, где читать, контракт метрик
- `client/Assets/GamePlay/Metrics/` - клиентские часы и писатели
- `backend/Game/Session/Metrics/SessionMetrics.cs`, `backend/Game/GamePlay/Context/MatchClock.cs`
- `backend/Meta/Matches/` (`MatchState`, `MatchMetricsAdmission`, `Players/MatchGameMetrics.cs`)
- `backend/Orchestration/MetaGateway/Matches/CONTRACT.md` - API истории матчей
- `backend/Orchestration/MetaGateway/UserFlow/Commands/{ClientTelemetry,MatchMetrics}Commands.cs`

### Заметки
- Реванш - новая сессия: новый id, новая запись, часы с нуля.
- `SnapshotReceiver.Add` перезаписывает обработчик типа: писатели слушают `IReceivedSnapshots.Received`, а не свои `ISnapshotHandler`.
- click/cascade выводит клиент (сервер не различает); `mines.hit` - только подрывы клика/аккорда своего поля.
- Флаг Client Telemetry на проде включает пользователь (Features консоли); без него сессий и трейсов нет, метрики матча от флага не зависят.
- В редакторе телеметрия выключена (`ClientTelemetryOptions.enabledInEditor = false`).
- Не проверены вживую: реванш, попадание исключения писателя в лог.
