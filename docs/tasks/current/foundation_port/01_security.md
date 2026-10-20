# 01. Безопасность (S1-S5)

A = `/projects/peace-data-reborn`, B = этот репозиторий. Пути без префикса указаны в B относительно `backend/`.

S1 и S2 сделать первыми. S3 и S4 меняют протокол и handshake, их выкатывают одним релизом с N4, N6 и N7 (см. overview, раздел "Протокольный релиз").

## S1. Консоль: токен только в dev, compose без дефолтов

- **Приоритет:** P0. **Волна:** 1.
- **Источник в A:**
  - `5def6dd1d` (F1.4): `Orchestration/Extensions/ConsoleAccess.cs`, `ConsoleGateway/Program.cs`, тест `Tools/Tests/Security/ConsoleAccessTests.cs` с `TestWebHost.cs`.
  - `c3ae81284`: `Tools/deploy/docker-compose.yaml`, `.env.example`, `COOLIFY.md`.
- **Проблема:**
  - `Orchestration/ConsoleGateway/Program.cs:26-27`: при пустом токене или `local-dev-token` авторизация отключена.
  - `Tools/deploy/docker-compose.yaml:235`: `CONSOLE_TOKEN: ${CONSOLE_TOKEN}` без `:?`, поэтому незаданная переменная в Coolify открывает админ-консоль в проде.
  - В том же compose: `:275` `ASPIRE_TOKEN` по умолчанию `dev-token`, `:48,277` `OTEL_API_KEY` по умолчанию `dev-otlp-key`, `:65` `DB_*` без `:?`.
- **Шаги:**
  1. Перенести `ConsoleAccess`: вне Development старт падает; сравнение через `FixedTimeEquals`.
  2. В compose `:?` у всех секретов.
  3. Обновить `.env.example` и описание деплоя.
  4. Перенести тест.
- **Готово:**
  - Production с `local-dev-token` не стартует.
  - `docker compose config` без переменных падает.
  - Тесты зеленые.
- **Деплой:** пользователь задает `CONSOLE_TOKEN`, `ASPIRE_TOKEN`, `OTEL_API_KEY` в Coolify до выкатки.

## S2. Закрыть внутренние эндпоинты

- **Приоритет:** P0. **Волна:** 1.
- **Источник в A:** `5def6dd1d` (F1.3): `MetaGateway/MonitorEndpoints.cs`, `GameGateway/SessionEndpoints.cs`, `GameGateway/Program.cs`, `MetaGateway/Program.cs`, тест `Tools/Tests/Security/InternalEndpointsTests.cs`.
- **Проблема:** `Orchestration/MetaGateway/MonitorEndpoints.cs:7-9` и `Orchestration/GameGateway/SessionEndpoints.cs:10-12` открыты всем и отдают Guid пользователей. Вместе с S3 это захват чужого аккаунта.
- **Шаги:**
  1. Закрыть `Authorization: Bearer $CONSOLE_TOKEN`, как в A.
  2. Проверить, кто их вызывает (консоль, скрипты в `tools/scripts`, например `console-watch.sh`), и передать им токен.
  3. Перенести тест.
- **Готово:** без токена 401; консоль и скрипты работают; тест зеленый.

## S3. Гостевой секрет вместо логина по userId

- **Приоритет:** P0. **Волна:** протокольный релиз (см. overview). Решение пользователя: Q1.
- **Источник в A:** `f6f6fd317` (F1.2), описание в A `.claude/docs/DEPLOY.md`, раздел "Guest secret migration (F1.2)".
  - Новые файлы `GuestSecrets`, `MetaSocketAuth`; правки `BackendConnectionMiddleware.cs`, `Meta/Users/*`, клиентский `Authentication.cs`.
  - Тесты в `Tools/Tests/Meta`.
  - Опция `GUEST_LEGACY_LOGIN_UNTIL`: окно для старых гостей без секрета.
- **Проблема:**
  - `Orchestration/MetaGateway/UserFlow/Connection/BackendConnectionMiddleware.cs:62,142` и `Meta/Users/Entities/UserFactory.cs:50-55`: вход по голому `?userId`. Кто знает чужой Guid, входит в его аккаунт.
  - Guid утекает через S2 и через `Game/Session/Root/Session.cs:127,133`: `BackendId` рассылается пирам по матчу.
- **Шаги:**
  1. Перенести выдачу секрета при первом входе, хранение хэша и проверку.
  2. Окно legacy-входа.
  3. Перестать рассылать `BackendId` пирам: заменить на id в сессии.
  4. Ветки Telegram из A не переносить: в B Telegram нет, это упрощает миграцию.
  5. Ключи `PlayerPrefs` вынести в задачу N12.
- **Готово:**
  - Тесты A перенесены и зеленые.
  - Старый гость в окне legacy входит и получает секрет.
  - Вход по чужому userId без секрета отклоняется.
  - Пиры не видят meta Guid.
- **Деплой:**
  - Бэкап БД.
  - `GUEST_LEGACY_LOGIN_UNTIL` = дата деплоя + 30 дней.
  - Клиент выкатывается сразу после backend.
  - Мониторинг выдачи секретов.

## S4. Подписанные тикеты сессий

- **Приоритет:** P0. **Волна:** протокольный релиз.
- **Источник в A:** `f6f6fd317` (F1.1): `SessionTickets`, `SessionTicketsSetup`, `SessionGate` (резерв мест), таймаут handshake, выдача тикета в matchmaking. Секрет `SESSION_TICKET_SECRET`: base64, 32 байта и больше, одинаковый для meta и game.
- **Проблема:**
  - `Orchestration/GameGateway/SessionConnectionMiddleware.cs:67,89,100`: GameGateway верит `UserId` и `SessionId` от клиента. Можно войти в любую сессию под любым пользователем: занять чужое место, заставить сервер засчитать сдачу, испортить рейтинг.
  - `ConnectionOneTimeHandle.cs:23`: handshake без таймаута (`CancellationToken.None`).
- **Шаги:**
  1. Перенести `SessionTickets`, `SessionGate` и таймаут handshake как есть.
  2. Выдачу тикета встроить в matchmaking B. Лобби в B устроено иначе, чем в A.
  3. Перенести тесты.
- **Готово:**
  - Вход без тикета, с чужим или просроченным тикетом отклоняется.
  - Матч с ботом и матч двух клиентов работают.
  - Тесты зеленые.
- **Деплой:** `SESSION_TICKET_SECRET` в Coolify (`openssl rand -base64 48`).

## S5. CORS allow-list

- **Приоритет:** P1. **Волна:** 1. Решение пользователя: Q2, список origin-ов.
- **Источник в A:** `5def6dd1d` (F1.5): `Common/Extensions/CorsExtensions.cs`, `GameGateway/MiddlewareExtensions.cs`, `BackendConnectionMiddleware.cs`, тест `Tools/Tests/Security/AllowedOriginsTests.cs`.
- **Проблема:** любой origin вместе с credentials:
  - `Common/Extensions/CorsExtensions.cs:15`;
  - `Orchestration/GameGateway/MiddlewareExtensions.cs:7-11`;
  - `BackendConnectionMiddleware.cs:159-163`, проверка Origin у websocket.
- **Готово:** чужой Origin отклоняется на HTTP и websocket; тест зеленый.
