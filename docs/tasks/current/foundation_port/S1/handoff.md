# S1 + S2 + S5 handoff

Связка S1 (консоль, compose), S2 (внутренние эндпоинты), S5 (CORS). Источник: A `5def6dd1d` (F1.3-F1.5), compose-хунки из `c3ae81284`.

## Шаг 0: подтверждение (чтением кода)

- S1: `ConsoleGateway/Program.cs:26-27` выключал авторизацию при пустом токене или `local-dev-token` в любом окружении, сравнение через `==`. В compose `CONSOLE_TOKEN: ${CONSOLE_TOKEN}` без `:?`, `ASPIRE_TOKEN:-dev-token`, `OTEL_API_KEY:-dev-otlp-key`, у `DB_*` и `GAME_SERVER_URL` нет `:?`. Подтверждено.
- S2: `/api/monitor` (connected-users, matchmaking) и `/api/sessions` (+ players) без авторизации, отдают Guid пользователей и сессий. Подтверждено. Вызывающих нет: консоль их не вызывает, в `tools/scripts` обращений нет (`console-watch.sh` только запускает `dotnet watch`). Передавать токен некому.
- S5: `CorsExtensions` использует `AllowAnyOrigin`; `GameGateway/MiddlewareExtensions` и `BackendMiddlewareExtensions` используют `SetIsOriginAllowed(_ => true)` + `AllowCredentials`, websocket-handshake Origin не проверяет. Подтверждено.

## Как перенесено

- Патчем A (файлы совпадали с `5def6dd1d^`): `Common/Extensions/CorsExtensions.cs`, `GameGateway/MiddlewareExtensions.cs`, `GameGateway/Program.cs`, новый `Orchestration/Extensions/ConsoleAccess.cs`, тесты `Tools/Tests/Security/*`.
- Вручную (файлы отличались): `ConsoleGateway/Program.cs`, `GameGateway/SessionEndpoints.cs`, `MetaGateway/MonitorEndpoints.cs`, `MetaGateway/Program.cs`, `Extensions/ProjectsSetupExtensions.cs`, `BackendConnectionMiddleware.cs` (только `AddBackendMiddleware`), `Aspire/Program.cs` (`CONSOLE_TOKEN` для meta и game).
- Деплой: `Tools/deploy/docker-compose.yaml`, `.env.example`, `COOLIFY.md`, `.agents/docs/DEPLOY.md`.

## Отличия от A

- Origin-ы по умолчанию: `https://minesleader.xyz,https://play.minesleader.xyz,https://html-classic.itch.zone` (Q2). Шаблонов `*.itch.zone` в A нет, поэтому они не добавлены; сравнение точное.
- `ASPIRE_TOKEN` и `OTEL_API_KEY` сделаны `${NAME:?}`, как требует задача. В A дашборд вынесен в профиль `with-dashboard` с пустыми дефолтами. В B дашборд запущен всегда, профиль не вводился.
- `/api/monitor/matchmaking` (есть только в B) закрыт тем же фильтром группы и добавлен в тест.
- `InternalEndpointsTests` не использует `SessionTestWorld` из A: сессии и matchmaking подменены через NSubstitute. В `Tests.csproj` добавлена ссылка на `GameGateway.csproj`.
- Тесты origin-ов переписаны на домены B, добавлены кейсы `play.minesleader.xyz`, `evil.minesleader.xyz`, `evil.itch.zone`.
- Из `c3ae81284` не перенесено: pin pgbouncer (это задача B10), `!override` сети в `docker-compose.local.yaml`, перестройка COOLIFY.md (Prerequisites и т.п.). К безопасности это не относится.

## Проверка

- `dotnet build backend/backend.slnx` прошёл.
- `Tests --filter-namespace Tests.Security`: 60/60.
- Полный прогон: 1059 тестов, 1057 прошли, 2 падают в `Tests.Meta.UserDeckTests` (Orleans `TypeManifestOptions` не пропускает `Dictionary<int, IReadOnlyList<CardType>>`). Они падают и без ссылки на GameGateway и без Security-тестов, к задаче не относятся.
- `docker compose config` без переменных падает (`required variable ... is missing a value`), без одного только `CONSOLE_TOKEN` тоже падает; с полным набором проходит, у meta и game есть `Cors__AllowedOrigins` и `CONSOLE_TOKEN`.

## Деплой (до выкатки)

В Coolify задать `CONSOLE_TOKEN` (не `local-dev-token`), `ASPIRE_TOKEN`, `OTEL_API_KEY`. Проверить, что заданы `DB_HOST/DB_NAME/DB_USER/DB_PASSWORD` и `GAME_SERVER_URL`. `CORS_ALLOWED_ORIGINS` задавать не обязательно (дефолт выше), но новый домен клиента нужно добавить туда до выкатки. Внутренние эндпоинты в проде вызываются с `-H "Authorization: Bearer $CONSOLE_TOKEN"`.
