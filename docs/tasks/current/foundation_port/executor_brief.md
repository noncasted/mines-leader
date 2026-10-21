# Foundation port: бриф исполнителя

Читать вместе с `overview.md` (источник фиксов, как накладывать патчи A) и файлом своей задачи.

## Правила

1. Шаг 0: подтвердить баг в B чтением кода или падающим тестом и записать в `<ID>/handoff.md`. Если баг не подтвердился — стоп, отчёт.
2. Переносить минимально: фикс и его тесты. Не тянуть игровой код A и не рефакторить окружение.
3. Fail-fast (`CLAUDE.md`): никаких новых `if (x == null) return;` и пустых `catch`.
4. Проверка бэкенда: `dotnet build backend/backend.slnx`, затем тесты. `dotnet test` на SDK 10 не работает: собрать `dotnet build backend/Tools/Tests/Tests.csproj` и запустить `backend/Tools/Tests/bin/Debug/net10.0/Tests --filter-class "*Name*"` (сначала новые тесты, потом полный прогон без фильтра). Логи — `.claude/docs/TESTING.md`. Тесты, которые без Docker пропускаются, указать в отчёте.
5. Проверка клиента: компиляция в редакторе B без ошибок, EditMode-тесты через MCP (только stdio-сервер `unityMCP`, HTTP не использовать). Только когда оркестратор выдал редактор (`editor_owner.txt`).
6. Не коммитить, коммитит оркестратор. В отчёте: изменённые файлы, результат сборки и тестов, отличия от A.
7. Отчёт по-русски, коротко.
8. Работа идёт в своём git worktree (изоляция от соседей). Правки оставлять незакоммиченными в worktree, оркестратор переносит их в основное дерево. Файлы `docs/tasks/current/foundation_port/<ID>/handoff.md` создавать в worktree.
9. Не трогать `client/Packages`, `client/ProjectSettings`, `client/Assets/Plugins`: там соседняя сессия чинит апгрейд Unity.

## Окружение (изменилось после создания задач)

- Пользователь обновил клиент B на Unity `7000.0.0a10` (CoreCLR-альфа, как A). Изменение ещё не закоммичено, им занимается соседняя сессия `mines-leader-f7`. Значит, фиксы A под CoreCLR (формат float, `string.GetHashCode` на процесс, N8) к B **применимы**.

## Ответы пользователя (2026-10-21)

- Q1 (S3): живых гостей не сохранять. Legacy-окна нет, вход только с секретом. `GUEST_LEGACY_LOGIN_UNTIL` не переносить.
- Q2 (S5): origin-ы — свой домен и itch.io: `https://minesleader.xyz`, `https://play.minesleader.xyz`, `https://html-classic.itch.zone` (плюс `*.itch.zone`, если в A есть поддержка шаблонов). Localhost только в Development.
- Q3/Q6: прод жив, Postgres свой (B7 остаётся P1). Деплой: Coolify compose (`backend/Tools/deploy/docker-compose.yaml`) и WebGL через `tools/scripts/deploy-webgl.sh`.
- Q4: переподключение нужно. N5 включает переподключение meta по образцу A `336dbcb59`; N13 — волна 7 с дизайном.
- Q5 (N11): решает оркестратор при старте волны 3.

## Агенты

- Агенты: S1+S2+S5 - a113fe3627b1ad9f2
- Агенты: B1+B2 - aa7b7b145cfa9c208
- Агенты: N1+N2 - a69624932f82e8db1

## Клиент после апгрейда

- Компиляция в редакторе на 7000.0.0a10 зелёная (0 ошибок). `client/*.csproj` устарели: перед `dotnet build client.sln` перегенерировать проекты (Assets > Open C# Project), иначе ложные ошибки NUnit и RP Core Editor.
- Агенты: R1 - ad475a66608ab63d8
- Агенты: N5+N12 - aaee6f05bc3026856 (основное дерево, без редактора до сигнала)
- Агенты: N3 - aa57356bc591a5e81
- Агенты: B3+B4 - ad14fabdc423a59af
- Агенты: B5+B6+B7 - a9e02c5bcecb51956
- Агенты: N8 - ab5bce46a36d07ee5
- Агенты: B8+B9 - ab54fcb7851c49f05
- Агенты: B10+B11 - a72bd5ca497837577
- Агенты: N9 - a94a5260f30e6a2bd
- Агенты: N11 - aa2dbd5adbce2d7ce
- Агенты: B13 - a71a8ae0e3839d06c
- Агенты: N6 - a3559dbb685cebf5d
- Агенты: N7 - a3587b366038d78f1; проверка в редакторе - a8c9b1b49e6641ee4
- Агенты: N4 - a0367aee7b0a0400f
- Агенты: S3+S4 - a440bd635e9dbc779 (done), N10 - a7b1c0d2bf0d7c60b (done), фикс 7f2d9550 - ad252020127ef2049 (done), R2 - af4a1084fcab4e5e0 (done), C1+C2+C3 - a5f85a417b435cb73 (done), T1+C4+C7 - a5aa481a75db1471f (done), C6 - a786e8cb3db469473 (done), дрейф HierarchyBindings - aeb28dc10c022e76a
