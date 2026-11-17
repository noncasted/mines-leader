---
task: startup_backend_trace_console
status: done
created: 2026-11-17
---

# Консоль показывает backend-трейс записи startup клиента

## Требования
- Framework (Console / reader-сборка, не gateway): опциональный источник backend-трейсов (options: URL дашборда Aspire + API key), регистрирует игра; без него страница как сейчас (plain id). Неломающее изменение; шаги для игр - в `tools/framework/releases/unreleased.md`.
- Reader (IHttpClientFactory): GET `/api/telemetry/traces/{id}` с `x-api-key`, OTLP JSON -> модель span-ов (id, parent, name, service, scope, start/end UTC + offset, duration, attributes, error). Not found -> "трейса больше нет в дашборде"; сетевые/прочие ошибки -> сообщение на панели (граница - консоль).
- `TelemetryStartupPage.razor`: секция "Backend trace" (дерево, сервис, длительность, waterfall на своей оси, атрибуты, ошибки), переиспользуя вид дерева клиентских span-ов. Только Tailwind-классы из `backend/Orchestration/ConsoleGateway/wwwroot/css/app.css`. Оси клиента и бэкенда не совмещать.
- Unit-тесты маппинга OTLP JSON в `tools/framework/backend/Tests`: дерево, offsets, атрибуты, error, empty/not-found.
- Game: `backend/Orchestration/ConsoleGateway/Program.cs` - регистрация из секции `AspireDashboard` (`Url`, `ApiKey`); AppHost (`backend/Orchestration/Aspire`) передаёт URL и ключ консоли, если может (иначе документировать локальную настройку).
- Prod compose: `aspire-dashboard` + `DASHBOARD__API__AUTHMODE: ApiKey`, `DASHBOARD__API__PRIMARYAPIKEY: ${ASPIRE_API_KEY:?}`; console + URL `http://aspire-dashboard:18888` и тот же ключ. Секрет в `COOLIFY.md`, `.env.example`, `.claude/docs/DEPLOY.md` (где перечислены OTEL_API_KEY / ASPIRE_TOKEN). Проверить имена env для образа dashboard 13.x.
- Docs: `.claude/docs/TELEMETRY.md` - абзац.
- Не коммитить, не пушить, не деплоить.

## План
1. Framework: options + extension + reader + model + mapper + page section + тесты + unreleased.md; сборка и тесты фреймворка.
2. Game config/docs (параллельно с 1, без сборки): compose, COOLIFY.md, .env.example, DEPLOY.md, TELEMETRY.md, исследование AppHost и правка Aspire Program.cs.
3. Game wiring (после 1 и 2): ConsoleGateway Program.cs, сборка backend.slnx, тесты, перезапуск кластера, живая проверка страницы, скриншот в docs/media/research/startup_backend_trace_console/.

## Статус шагов
| Шаг | Статус |
|-----|--------|
| 1 | done |
| 2 | done |
| 3 | done |
