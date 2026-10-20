# 05. Инструменты и деплой (T1-T4)

A = `/projects/peace-data-reborn`, B = этот репозиторий.

## T1. CI и csproj генератора в git

- **Приоритет:** P2. **Волна:** 5.
- **Источник в A:** `b9f89f20b` (F0.2, F0.10):
  - `.github/workflows/ci.yml`;
  - `.gitignore`;
  - `client/Tools~/ContainerGenerator/ContainerGenerator.csproj`;
  - `client/Tools~/ContainerGenerator.Verify/ContainerGenerator.Verify.csproj`;
  - README генератора.
- **Проблема:**
  - `.gitignore:84` (`/client/**/*.csproj`) игнорирует csproj генератора, поэтому на чистом клоне генератор не собирается.
  - В B нет `.github/workflows`, CI отсутствует.
- **Шаги:**
  1. Добавить исключения в `.gitignore` и закоммитить csproj.
  2. Перенести `ci.yml`: сборка и тесты backend, Verify генератора.
- **Готово:**
  - на чистом `git worktree` собираются генератор и backend;
  - команды CI проходят локально;
  - пуш и проверка CI на GitHub — только с согласия пользователя.

## T2. run-tests.sh

- **Приоритет:** P2. **Волна:** 5, после T1.
- **Источник в A:**
  - `821f27521` (F8.4): `tools/scripts/run-tests.sh`;
  - `/projects/peace-data-reborn/.claude/docs/TESTING.md`, раздел "Running everything locally".
- **Что делает скрипт в A:**
  - прогоняет тесты backend, Verify генератора и Unity EditMode в batchmode в reflink-снимке рабочего дерева;
  - не стартует Unity, если свободной памяти меньше 8 GB.
- **Шаги:**
  1. Перенести скрипт, поправить пути и версию Unity B.
  2. Описать скрипт в `.claude/docs/TESTING.md`.
- **Готово:** скрипт заканчивается `ALL TESTS PASSED` либо честным списком падений, которые были и до этапа.

## T3. Защита WebGL-деплоя

- **Приоритет:** P1, если B деплоит WebGL через `tools/scripts/deploy-webgl.sh`. **Волна:** 5.
- **Источник в A:**
  - `c53569ea9`: `deploy-webgl.sh` отказывает, если `index.html` ссылается на отсутствующий `Build/*`;
  - память A `webgl-wasm-ld-flaky`: инкрементальная сборка Bee стирает вывод предыдущей сборки в другую папку. 2026-10-17 это положило сайт A.
  - В A также есть отказ при уже живом `build-guid.txt` и проверка шаблона target; см. `git log -- tools/scripts/deploy-webgl.sh` в A.
- **Проблема:** `tools/scripts/deploy-webgl.sh` в B старый (126 строк против 236 в A), этих проверок нет.
- **Шаги:**
  1. Перенести проверки payload и `build-guid`.
  2. Не тянуть из A Telegram-таргеты, если в B их нет.
- **Готово:** скрипт отказывает на сборке с удаленным `Build/*.wasm.br`. Проверка на локальной копии, без загрузки на сервер.

## T4. Пропатченные пакеты

- **Приоритет:** P3. **Волна:** 5.
- **Источник в A:** `0a67a0b08` (F0.7):
  - `.claude/skills/update-override-packages/` (skill, `packages.json`, патчи);
  - пин unity-mcp в `client/Packages/manifest.json`.
- **Шаги:**
  1. Сверить `client/Packages` B: какие пакеты встроены и пропатчены.
  2. Закрепить git-зависимости на commit.
  3. Если пропатченные пакеты есть, перенести skill в `.claude/skills/` и описать патчи.
- **Готово:** нет git-зависимостей без пина; у каждого пропатченного пакета есть описание патча.
