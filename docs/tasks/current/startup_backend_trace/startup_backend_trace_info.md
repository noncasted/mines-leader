---
task: startup_backend_trace
status: done
created: 2026-11-17
---

# Связать backend OTLP-трейс авторизации meta-сокета с записью startup trace клиента

## Требования (из задачи пользователя, дословно по смыслу)
- Сервер сам создаёт трейс; клиент ничего нового не шлёт; ProtocolHash не меняется.
- MetaGateway: ActivitySource `MetaConnectionTelemetry.Source = new("MetaGateway.Connection")`, регистрация через `AddTraceSource`.
- `BackendConnectionMiddleware`: корневой span "Meta connect" (Server), отвязанный от ASP.NET request activity (ActivityLink на request), дочерние: ReadRequest, MetaSocketAuth.Authenticate (теги new/known guest, статус результата), SendResponse, await OnConnected. Error status на путях отказа. Root закрывается до `await completion.Task`.
- `UserSession`/`IUserSession.TraceId` (ActivityTraceId), `ClientTelemetryCommands` передаёт `session.TraceId`.
- Orleans activity propagation НЕ добавлять (follow-up).
- Framework: `AcceptStartup(trace, ActivityTraceId)` -> `ClientTelemetryStartups.Accept` -> parser `with { BackendTraceId }`; `TelemetryStartupTrace [Id(9)]`, `TelemetryStartupState [Id(15)]`, `TelemetryStartupInfo`, read JSON `backendTraceId`, TestHost seed; HTTP intake передаёт `default`; тесты; console `TelemetryStartupPage.razor` "Backend trace"; `report.py` `backend_trace=`; SKILL.md; CONTRACT.md; `releases/unreleased.md`.
- Game docs `.claude/docs/TELEMETRY.md`.
- Не коммитить, не деплоить.

## План
1. Framework (tools/framework/backend/ClientTelemetry, Console, Tests, skill, CONTRACT, unreleased) + framework тесты ClientTelemetry.
2. Game (MetaGateway span, UserSession, ClientTelemetryCommands, TELEMETRY.md) + сборка backend.slnx + тесты MetaSocketAuth/Handshake + живая проверка (control skill, Aspire OTLP).

## Статус шагов
| Шаг | Статус |
|-----|--------|
| 1 | done |
| 2 | done |
