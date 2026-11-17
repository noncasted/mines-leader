## Оркестратор - handoff поколения 1 (2026-11-17 22:15)

### Сделано
- Шаги 0-17 плана, 2b, D1, D2, F2, R2, G1, G2 - done (подробности и evidence: `framework_0_9_0_orchestration.md` журнал, `_result.md`).
- Игра в `origin/main` = `810588af` (фреймворк `v0.9.2`), push сделан по просьбе пользователя (бэкенд Coolify передеплоится по вебхуку; клиент выкладывает пользователь).
- Фреймворк: релиз `v0.9.2` (фикс раскладки) запушен; `v0.9.3` выпущен не нами (аудит 2026-11-16).

### Сейчас работает
- G3: агент `a5bca1b3c4019da4a`, marker `ORCH-framework_0_9_0-G3-g1` - обновление игры на `v0.9.3` в копии `/var/tmp/unity-copies/mines-leader/framework-0-9-0` (ветка `framework-0.9.0` от `810588af`): шаги релиза 0.9.3, сборки, `deploy-client.sh web --no-deploy` PASS, Play, коммиты без push. Его уведомление придёт в сессию поколения 1, она перешлёт его тебе через SendMessage.

### Дальше
1. Проверить отчёт G3 (git log `810588af..HEAD` в копии, `git ls-tree HEAD tools/framework` = `e38f175f`, таблица стадий), обновить `_info.md`/`_progress.md`/`_result.md`/`_orchestration.md`.
2. Спросить пользователя про push в `origin main` (прошлое разрешение было на прошлый push; push снова передеплоит бэкенд). Push - из репозитория проекта: `git -C /projects/mines-leader fetch origin && git -C /projects/mines-leader push origin copy/framework-0-9-0/framework-0.9.0:main` (ветку обновляет post-commit хук копии; только fast-forward, без force).
3. Отчёт пользователю по-русски. Предложить `/workflow-complete` и `control copy remove`.

### Решения пользователя (2026-11-17)
- Раскладка Main+Menu+GamePlay(+GamePlayViews)+Music+общие; Main и SharedCommon в `.data`. Сессии/лобби бэкенда свои. Скиллы - ссылки. Копии тестов фреймворка удалять. Опции агенты меняют как нужно (смену окружения не коммитят), Aspire поднимают сами. `OptionsContainer` Production - оставить. Механизм локальной сборки - пока нет. Теги матчмейкинга - не переносить. Мёртвые доки и ссылки - удалять.

### Ресурсы
- Копия: редактор `client@...` порт 6401, кластер копии запущен - держит G3.
- Общий `/tmp/claude-1000/unity-batchmode.lock` держит зависший чужой процесс 1349193 (`/projects/storytale-workshop`) - не трогать, свои `UNITY_LOCK`.
- Основное дерево `/projects/mines-leader`: чужие правки (`GameRoles.cs`, `InputManager.asset`, указатель), локальный `main` на `2f47e207` - не трогать.
