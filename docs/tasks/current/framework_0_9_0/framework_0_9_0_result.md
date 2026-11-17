## Перевод mines-leader на фреймворк 0.9.0 — Результат

### Статус: Готово, в `origin/main` = `70df9d59` (фреймворк `v0.9.3`)

### Что сделано
- Игра на фреймворке `v0.9.2` (`5c4da7db`; через `v0.9.0`, `v0.9.1`), шаги релизов 0.1.0-0.9.0 выполнены или пропущены с причиной (0 попаданий; 0.1.0 #15 пропущен; лобби/хостинг фреймворка не подключались - решение пользователя).
- Контент на бандлах движка: 7 `ContentBundle` (`Main`, `SharedCommon` - в `.data`; `Menu`, `GamePlayViews`, `SharedMenu`, `GamePlay` - gzip; `Music` - отдельно), общие - `GameSharedBundles` (`ContentSharedSync`); Addressables удалены полностью.
- Сборки: `dotnet build backend/backend.slnx` 0 ошибок, `dotnet build client.sln` 0 ошибок, WebGL `Result: Success`, `[ContentBuild] 7 content bundles for WebGL`.
- Тесты `run-tests.sh`: backend 1535/1535, generator 180, Unity 601 + 47 штатных skip. Копии тестов фреймворка удалены (340 атрибутов) - решение пользователя.
- Поведение: меню, бот-матч, пауза, выход/повтор, результат Win/Lose, Rematch, конец хода при перетаскивании, окна меню, музыка, переподключение, коды закрытия 1000 - OK (редактор и собранная страница через nginx игры).
- Размер (gzip / прод, МБ): весь билд 6,997 -> 6,355 / 5,800 -> 5,072; до меню 6,545 -> 5,966 / 5,285 -> 4,683; запросов до меню 22-23 -> 4-5 (`docs/web_build_size/2026-11-17/report.md`).

### Измененные файлы

| Файл | Что изменено | Шаг | Evidence |
|------|-------------|-----|----------|
| `docs/web_build_size/2026-11-17/stats.json` | замер «до» (Addressables, gzip) | 1 | `b1246bdf` |
| `.claude/skills/*` (16 ссылок), `.claude/{control.json,settings.json,project-skills.md}`, `CLAUDE.md`, `.claude/docs/{GAMEPLAY,VOCABULARY}.md` | общие скиллы ссылками, INDEX | 4 | `4d1aefe1` |
| `backend/**` (csproj, ~93 `using`, сессии, гейтвеи, консоль, бенчмарки), `backend/Tools/Tests/**` (удалены копии тестов фреймворка), `shared/Protocol/{NetworkContextUnion,ProtocolHash}.cs`, `shared/shared.csproj` | бэкенд на 0.7.0 | 3 | `db00685d`..`9d1c68c3` |
| `client/Packages/{manifest,packages-lock}.json`, 11 asmdef, `tools/framework`, `.gitignore`, `InputManager.asset`, `NetworkOptions.asset`, карты (A5), `Settings.cs`, `MetaLoop.cs`, `Cells/*`, `ButtonSoundsPlayer.cs`, `GlobalUIExtensions.cs`, `PlayerModifiers.cs`, `GameRandom`, `GameFloatingText`, `MenuDecks` | клиент на 0.7.0 | 2 | `4faa00d1`..`294c0608` |
| `Hand.cs`, `SnapshotReceiver.cs`, `MockBase.cs` | фиксы дымовой проверки 0.7.0 | 2b | `7fa27318` |
| манифест, lock, 7 asmdef, `Framework.json`, `.gitignore`, указатель v0.8.0 | 0.8.0 без Addressables | 5 | `d8d29706` |
| `InternalScopeExtensions.cs`, `GameStartup.cs`, `WebGlDeploy.cs`, каталоги (сгенерированные), `*Groups.json`, `StartupAssetsPreload.cs`, `MusicAudio.cs`, `MenuLoop`, `Scenes` в scope-расширениях, `Editor/Content/Main.asset` | AddContent, каталоги на бандлах | 6 | `aa5116aa` |
| 6 сцен меню и матча (`m_LightingDataAsset`) | сцены без встроенного кубмапа | 7 | `30b98615` |
| `Editor/Content/{Menu,GamePlay,GamePlayViews,Music,SharedCommon,SharedMenu}.asset`, asmdef `Flow`/`Menu`/`GamePlay` | раскладка бандлов | 8 | `8f13d64a` |
| `WebGLTemplates/Default/index.html`, `client/deploy/{nginx.conf,README.md}`, `tools/scripts/deploy-webgl.sh` | отдача `StreamingAssets/Content` | 12 | `e959eaa4` |
| `client/Assets/AddressableAssetsData/**` (удалена), `WebGlDeploy.cs` (удалён), `EditorBuildSettings.asset`, `UIElementsSchema/*.xsd`, `client.slnx`, `.claude/docs/{VOCABULARY,COMMON_CONTAINER}.md` | Addressables удалены | 9 | `3c50f42d` |
| `client/ProjectSettings/Framework.json` | `Internal.EncodedAudio` в стрип | 10 | `d49112d0` |
| `tools/framework` (v0.9.0), `Editor/Content/GameSharedBundles.cs` [новый], `Main.asset`, `SharedCommon.asset`, `SharedMenu.asset`, `Internal.Content.Editor.csproj` | 0.9.0, общие бандлы синком | 11 | `dc23e91c`, `f5069a20` |
| `tools/scripts/run-tests.sh` | SkipAnalyzersCopy, watchdog SBP, UNITY_LOCK | 13 | `fe55ee6b` |
| `backend/Tools/Tests/Reactive/*` (3), `Protocol/{StableHash,ClientProtocolCheck}Tests.cs` | удалены копии тестов фреймворка | 13 | `2519d65c` |
| `.claude/deploy-client.json` [новый], `.claude/project-skills.md` | deploy-client для WebGL | 14 | `42040995` |
| `docs/web_build_size/2026-11-17/{report.md,stats.json}` | замер «после», отчёт | 16 | `49c7551c` |
| `.claude/docs/` (12 копий удалены, ссылки в 5 доках), `.claude/skills/{task,validate}/SKILL.md` | копии общих доков удалены | D1 | `9e7c6595` |
| `tools/framework` (v0.9.1), `.claude/project-skills.md` | 0.9.1, deploy-client PASS | G1 | `9a9b85e2`, `51df7760` |
| `tools/framework` (v0.9.2), `Editor/Content/{Main,SharedCommon,SharedMenu}.asset` | 0.9.2, ручные корни `Main` сняты | G2 | `4258fb24`, `4d8494a8` |
| `tools/framework` (v0.9.3), `backend/Tools/DeploySetup/{SideEffectsSetup,StatesCleanup,StatesDrop}.cs`, `DeployIdentity.cs`, 6 тестов бэкенда, `GameMock.cs`, `MenuMock.cs` | 0.9.3: `SideEffectTables`, `SharedViewableProperty`, `UniTask Process()` | G3 | `70df9d59` |
| `.claude/docs/{CODE_EXAMPLES,UI_MENU}.md` (удалены), пути в доках и скиллах, `CLAUDE.md` | мёртвые доки и ссылки | D2 | `810588af` |
| фреймворк: `ContentBundleLayout.cs`, тесты, `CONTENT.md`, `releases/0.9.2.md` | фикс раскладки, релиз 0.9.2 (push origin main + тег) | F2, R2 | `48bb6646`, `5c4da7db` |

### Отличия от плана
<!-- Recorded immediately when approach changes -->
- Шаг 3: теги `Session*`/`Lobb*` не добавлены (конфликт с `SharedMatchmaking` 150-156); `OperationProgress` в `Common.Extensions`; `HomeClusterSection` не добавлен (дубль Features); фикстуре - `Lazy<ITransactionConfig>`.
- Шаг 2b (дымовая проверка Play на 0.7.0) добавлен вне плана. Шаги 5+6 и 7+8 выполнены одним агентом каждый.
- Шаг 6: `Main.asset` создан раньше шага 8; музыка - отдельная аудиогруппа `Music` вместо `AddContentBundle`.
- Шаг 8: пятый бандл `GamePlayViews`; общие бандлы вручную до шага 11; `Internal.Platforms.Web` в 3 asmdef.
- Шаг 11: `Menu_Play`, `Menu_Navigation` - корни `Main` (обход дефекта раскладки фреймворка).

### Нерешенные вопросы
<!-- Populated as open questions arise -->
- `HierarchyBindingsValidator` в Play: «structure is stale» для `Menu.unity` (Canvas/Play, Canvas/Navigation) - ложное срабатывание фреймворка (отложенная валидация после рантайм-перестройки меню).
- (решено 2026-11-17) `verify.sh` - фикс во фреймворке `a2fadb9b`, указатель - шаг G1; дефект раскладки - шаг F2 во фреймворке; копии доков - шаг D1; `GamePlayViews` - оставить; локальная сборка, теги матчмейкинга, `OptionsContainer` Production - оставить как есть.
- Не проверены: `GameRandom`, `FlagAnimator` (MissingReference, было до задачи), Home/`OnlineWidget` консоли (нет локальных учётных данных).
- Воспроизводится (было до задачи): NRE `LastManStandingTurnBasedRound.SkipTurn()` :224 при отключении до первого раунда.
- Чужой процесс 1349193 (`/projects/storytale-workshop`) держит `/tmp/claude-1000/unity-batchmode.lock`.
