# B13. Тесты инфраструктуры из A — handoff

## Шаг 0: подтверждение

`Tests.Meta.UserDeckTests`: 2 из 6 падали (`Update_AllDecksAndSelectedIndex_Persisted`, `GetSelected_AfterChangingSelectedIndex_ReturnsDifferentDeck`):
`InvalidOperationException: Type "Dictionary<int, IReadOnlyList<Shared.CardType>>" is not allowed`.
Причина: прод настраивает `TypeManifestOptions` через `OrleansSetupExtensions.AllowSharedTypes` (силос и клиент, коммиты 994f05db, 0fec4c7a),
а тестовая фикстура `OrleansTestClusterFixture` — нет.

## Изменения

- `backend/Tools/Tests/Fixtures/OrleansTestClusterFixture.cs`: `Configure<TypeManifestOptions>(OrleansSetupExtensions.AllowSharedTypes)`
  для силоса (`ConfigureSilo`) и клиента (`builder.ConfigureClient`), как в проде. `AllowAllTypes` не используется.

## Фикстура A vs B

Хунков для переноса нет: правки A (a1203e5d8, Marten из реестра) в B уже есть. Остальная разница — игровая специфика B
(`IBotCollection`, игровые конфиги, `MatchmakingEnabled`, `SnapshotDiffGuardEnabled`) и `GameRoles.Silo` вместо `ServiceTag.Silo` (B12, отложено).

## Сверка тестов A (State, Cluster, Messaging, Deploy, Telemetry, Reactive, Security, Network, Protocol)

Все тесты перенесённых задач есть в B. Файлы с отличиями — адаптация к B (`LiveMatchesData` вместо `LiveSessionsData`, `ServiceTag` вместо `GameRoles`,
`DeployStates.All` вместо `StatesLookup.All`, типы протокола B), проверки не потеряны.

Отсутствуют в B, относятся к непереносенным задачам или к игре A:

| Тест A | Задача |
|---|---|
| `Security/GuestLoginOptionsTests`, `Security/SessionTicketsTests`, `Security/TestTickets` | S3, S4 |
| `Network/ConnectionOneTimeHandleTests` | S4 (f6f6fd317) |
| `Network/CommandErrorResponseTests`; в `ConnectionWriterTests`: `ErrorResponse_ThrowsNetworkRequestException`, `ResponseOfAnotherType_ThrowsInvalidOperation`, переименование `..._WritesFail` → `..._WritesDropped` | N4 (1a7d6c0b1) |
| `Protocol/ProtocolSchema`, `Protocol/ProtocolSchemaTests`, `Protocol/SharedSerializationTests` | N6 (d4ccc67b2) |
| `Protocol/ClientProtocolCheckTests` | N7 (d4ebdac91) |
| `Protocol/ProtocolBytesTests` | golden-байты протокола A (3bdde2d39); имеет смысл после N6/N7 |
| `Cluster/ServiceTagTests`, `Deploy/GameCompositionTests`, `DeployCleanupTests.Run_LegacyNumericServiceTagRow_IsReadAndRemoved`, `Codegen/*` | B12 |
| `Cluster/ServiceSlicedStateTests`, `DeployCleanupTests.ServiceSlicedStates_RegisteredName_...` | per-service live data A, в B нет (см. B5, B8) |
| `Cluster/LobbyRegistryTests`, `Deploy/TelegramLinksBackfillTests`, `Meta/*` (Telegram, Steam, лобби) | игровой код A |

## Проверка

- `dotnet build backend/backend.slnx`: 0 ошибок.
- `UserDeckTests`: 6/6.
- Полный прогон: 1236/1236, 0 падений, 0 пропусков.

## Без Docker

Механизма пропуска нет: `DatabaseFixture` поднимает Postgres через Testcontainers, без Docker тесты на `OrleansTestClusterFixture`/`DatabaseFixture`
падают, а не пропускаются. В этом прогоне Docker был, пропусков 0.
