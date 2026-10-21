# Foundation port: handoff оркестратора (2026-10-21)

Сессия оркестратора: `mines-leader-c9`. Статусы задач и журнал — `overview.md`, решения — `orchestrator_decisions.md`, правила и ответы пользователя — `executor_brief.md`.

## Состояние

- Волны 1-3 закрыты, приняты вживую (HEAD 8861536b). Волна 2 закрыта. B12 отложена, B11 и N11 не подтверждены.
- Волна 4 (протокольный релиз): N6 (13a72c03 + 6ef0de72), N7 (65801f3e), N4 (6d8aecf9) — done.
  - S3+S4 — done (2138cb8e).
  - N10 — done (1721c865), фикс отправки в закрытый сокет — 7f2d9550.
  - Было: N10 (корреляция результата лобби/матча с запросом; плюс вариант ошибки в `MatchResult` для сбоя создания матча из очереди — хвост B1/N4).
  - Два клиента PvP не проверены: в manifest нет MPPM — спросить пользователя (вручную или второй редактор/WebGL-сборка).
  - После N10: чек-лист деплоя протокольного релиза (раздел ниже) и приёмка волны 4: два клиента в одном матче, окно обновления при старом хэше, вход без тикета / с чужим userId отклоняется.
- Волна 5 закрыта: R2 f67bd162, C1–C3 77bb0ce5.
- Волна 6: C6 380ae5c1, T1+C4+C7 750de794. Дрейф HierarchyBindings исправлен (32d19389). C5, T2, T3, T4 ждут коммита правок соседней сессии (asmdef, run-tests.sh, deploy-webgl.sh, встроенные пакеты — см. git status). Волна 7 — todo.

## Как работаем

- Исполнители: фоновые `Agent` с `isolation: worktree` (бэкенд) — промпт: ID, файлы задачи, overview, executor_brief, `git merge --ff-only main` первым шагом. Клиентские правки исполнители делают в worktree без редактора, проверку в редакторе делает проверяющий агент `a8c9b1b49e6641ee4` (resume через SendMessage; у него скрипт `scratchpad/restart_aspire.sh`).
- Перенос из worktree в основное дерево:
  ```
  W=.claude/worktrees/agent-<id>; S=<scratchpad>
  git -C $W status --short --untracked-files=all | grep '^??'   # должно быть пусто (исполнители делают git add -N)
  git -C $W diff HEAD --binary > $S/x.patch; git -C $W diff HEAD --name-only > $S/x.files
  git apply --3way $S/x.patch
  while read f; do [ -e $W/$f ] && { cmp -s $W/$f $f || echo DIFF $f; } || { [ -e $f ] && echo NOTDELETED $f; }; done < $S/x.files
  ```
  Только `git diff HEAD` (staged-удаления), в zsh списки путей — через `--pathspec-from-file`.
- Проверка бэкенда HEAD+патч — в отдельном worktree `scratchpad/verify` (detached; `git reset --hard; git clean -fd; git checkout --detach <HEAD>; git apply x.patch`; build + `backend/Tools/Tests/bin/Debug/net10.0/Tests`). В основном дереве обычно запущен Aspire.
- Коммит: `git commit --pathspec-from-file=$S/x.files -m ...` (только path-scoped; в index основного дерева бывают чужие staged-пути). В теле `(port of peace-data-reborn <hash>)`, в конце `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`. Затем `git worktree remove -f -f` + `git branch -D`, статус и журнал в `overview.md`.
- MCP: наш редактор `client@a78bdd3a` (порт 6401); `client@e1585409` — storytale-workshop.
- Редактор: `editor_owner.txt` (none / N*-verify / peer:<сессия>). Соседняя сессия `mines-leader-0f` (рендер, WebGL-сборки, MemoryPack) берёт редактор по сообщению — уступать, она возвращает.
- Протокол: каждая задача волны 4 обновляет `protocol_schema.golden.txt`, `protocol_bytes.golden.txt` (`UPDATE_PROTOCOL_GOLDEN=1`) и `ProtocolVersion.Hash`. Текущий хэш (после N10): `701d30859db55520`.

## Для пользователя (открытые вопросы)

- `SessionFileLogger` в проде пишет файл на сессию без ротации и глотает ошибки — предложено включать только в Development.
- Деплой волн 1-3 можно делать отдельно от протокольного релиза; но в main уже N6/N7/N4 — теперь клиент и бэкенд с main выкатываются вместе.
- Новая ошибка клиента "Trying to listen terminated lifetime" (CardRemoteSpawn) — смотреть в R2 (волна 5).

## Чек-лист деплоя (собирать по мере волны 4)

- Coolify, обязательные: `CONSOLE_TOKEN` (не `local-dev-token`), `ASPIRE_TOKEN`, `OTEL_API_KEY`; уже должны быть `DB_*`, `GAME_SERVER_URL`. После S4: `SESSION_TICKET_SECRET` (`openssl rand -base64 48`).
- Опционально: `CORS_ALLOWED_ORIGINS` (дефолт minesleader.xyz, play.minesleader.xyz, html-classic.itch.zone), `ORLEANS_CLUSTER_ID`/`ORLEANS_SERVICE_ID` (дефолт mines-leader; был `default`).
- Полный передеплой стека (ClusterId меняется). SQL очистки старых строк membership — `B5/handoff.md`.
- pgbouncer `v1.26.0-p0` вместо `latest`.
- В проде нет файловых логов/снапшотов метрик (только OTEL), бенчмарки выключены.
- S3: живые гости без секрета не сохраняются (ответ Q1) — что увидят старые игроки, см. `S3/handoff.md` после S3. Бэкап БД перед деплоем.
- Порядок: бэкенд, сразу клиент (WebGL `tools/scripts/deploy-webgl.sh`). Деплой только по просьбе пользователя.
