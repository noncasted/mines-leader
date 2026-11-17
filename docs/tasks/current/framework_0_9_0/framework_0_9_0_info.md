---
task: framework_0_9_0
status: done
phase: complete
created: 2026-11-17
updated: 2026-11-17
total_steps: 17
completed_steps: [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17]
blocked_steps: []
---

## Задача: перевод mines-leader на фреймворк 0.9.0

### Что я хочу

Перевести игру на релиз фреймворка `v0.9.0`. Указатель сабмодуля `tools/framework` в `origin/main` игры стоит на `a11e507a`
(2026-10-21): это раньше базы первого релиза `v0.1.0` (`63401a9`), поэтому нужны шаги всех релизов по порядку: `releases/0.1.0.md`
(вместе с разделом «Указатель старше базы», A1-A7), `0.2.0`, `0.2.1`, `0.2.2`, `0.3.0`, `0.4.0`, `0.5.0`, `0.5.1`, `0.6.0`, `0.7.0`,
`0.8.0`, `0.9.0`. Между `a11e507a` и `v0.9.0` 446 коммитов фреймворка.

Главная часть - контент. С 0.8.0 Addressables удалены из фреймворка: контент после старта - бандлы движка (`ContentBundle`,
Scriptable Build Pipeline, `IContentLoader`, `ContentReference<T>` / `ContentScene`), каталоги на них. Игре надо разложить свои группы
Addressables по ассетам `ContentBundle`, перевести загрузку скоупов и сцен, удалить Addressables (пакет, asmdef, `link.xml`,
`AddressableAssetsData`), поправить шаблон WebGL, nginx и скрипт деплоя. 0.9.0 добавляет общие бандлы игры (`IContentSharedBundles`).

Итог работы: игра на `v0.9.0`, контент на бандлах движка, сборки клиента (WebGL) и бэкенда без ошибок, тесты зелёные, замер размера
WebGL-сборки до и после, только в gzip (как `docs/web_build_size/2026-11-16` и `2026-11-17` pixel-swarm, без brotli и сырых размеров).
Образец уже сделанного перевода - pixel-swarm: сводка `/projects/pixel-swarm/docs/tasks/complete/framework_0_8_0.md`, коммиты
`git -C /projects/pixel-swarm log --oneline a73cf683b..origin/main` (17 коммитов, ключевые `fc2bf844c`, `5755d34ef`, `16375cee1`,
`f7747c32a`, `64dbd76aa`, `d66658b0f`, `8e41b8252`), классы `client/Assets/Common/Internal/Editor/Content/GameSharedBundles.cs`,
`StressContentStep.cs`.

Работа идёт в своей копии проекта (`control copy create`), как с pixel-swarm: рабочее дерево `/projects/mines-leader` занято другой
сессией (незакоммиченные `backend/Cluster.Game/Discovery/GameRoles.cs`, `client/ProjectSettings/InputManager.asset`, указатель
`tools/framework` выгружен на `c5b7c94`, локальный коммит `2f47e207` впереди `origin/main`). Основа ветки - `origin/main`, не `HEAD`
основного дерева. Рабочее дерево сабмодуля основного дерева не трогать.

### Цель

1. `tools/framework` игры на теге `v0.9.0` (`10762143`), шаги всех релизов 0.1.0-0.9.0 выполнены или отмечены как пропущенные с причиной.
2. Контент на `ContentBundle`: ни `com.unity.addressables`, ни `AssetReference`, ни `AddressableAssetsData` в проекте; сборка логирует
   `[ContentBuild] N content bundles for WebGL`, раскладка без ошибок.
3. `dotnet build backend/backend.slnx` - 0 ошибок; компиляция Unity без ошибок и без новых `CINGR*`; WebGL-сборка проходит.
4. Тесты: бэкенд игры, EditMode игры, `ContainerGenerator.Verify` (`tools/scripts/run-tests.sh`) зелёные; число тестов не меньше, чем до
   начала (кроме удалённых с причиной).
5. Замер WebGL в gzip до (на `origin/main` с Addressables) и после, отчёт в `docs/web_build_size/<дата>/report.md` + `stats.json`.
6. Поведение игры не сломано: меню, матч (PvP и с ботом), выход из матча и повторный вход, пауза, результаты, звук.

### Контекст

- Фреймворк: `/projects/framework`, `origin/main` = `fab236f4`, тег `v0.9.0` = `10762143` (аннотированный, объект `c1ce7c2c`).
- Unity игры `7000.0.0a10` (как у pixel-swarm), профиль сборки один.
- Правила: `/projects/framework/CLAUDE.md` (правило 8: после изменения собрать; API фреймворка можно ломать), `.claude/docs/INDEX.md`
  фреймворка и его принципы (P1-P14).
- Связанные задачи игры - раздел «Пересечения» ниже.

## Текущее состояние (по `origin/main` = `e895fe1d`, 2026-10-26)

### Фреймворк в игре

`client/Packages/manifest.json` (пакеты `file:../../tools/framework/...`):

| Пакет | Что |
|-------|-----|
| `com.noncasted.framework.core`, `.flow`, `.shared` | ядро, flow, протокол |
| модули `audio`, `backend`, `cameras`, `inputs`, `network`, `publish`, `ui` | используемые модули |
| платформы `platform.itch` (`AddItchSaves`), `platform.web` (`AddWebLoadingScreen` под `UNITY_WEBGL && !UNITY_EDITOR`) | платформы |
| форки `com.unity.addressables`, `com.unity.ugui`, `com.unity.web.stripping-tool`, `com.unity.render-pipelines.core`, `com.cysharp.unitask`, `com.cysharp.memorypack`, `com.coplaydev.unity-mcp` | пакеты фреймворка |
| `com.shared.game` | `shared/` игры (зависит от `com.noncasted.framework.shared`) |

Нет: `com.noncasted.framework.common` (A1, без него ядро 0.8+ не разрешится), `layout` (0.8.0 шаг 14), `animations` (A4: спрайтовые
анимации используют `CellAnimator`, `CellVisuals`, `FlagAnimator`). Не используются: `telemetry`, `lobbies`, `localization`, `settings`,
`resolution`, `framerate`, `browser`, `match-metrics`, платформы `steam` / `telegram`.

asmdef игры (12): `Art`, `Flow`, `Global`, `Internal.Content`, `Internal.Content.Editor`, `Internal.Content.Tests`, `Rendering`,
`GamePlay`, `GamePlay.Editor`, `Menu`, `Meta`, `ADF-RBG.Mulligan`. Ссылки на `Unity.Addressables` / `Unity.ResourceManager` /
`Unity.Addressables.Editor` - в 7 из них (`Flow`, `Global`, `Internal.Content`, `Internal.Content.Editor`, `GamePlay`, `Menu`, `Meta`).
Ни один не ссылается на `Framework.Common` (`GUID:3ea5c3f1172040daa713d5ad71728b78`).

`client/ProjectSettings/Framework.json`: есть `ContentAssembly`, `ContentFolder` (`Assets/Common/Internal`), `ModuleRoots`, `SceneFolders`,
`StrippedPlayerAssemblies: [Unity.2D.Tilemap.Runtime]`; лишние для 0.8 `SharedCommonPaths`, `GroupPhases`.

### Бэкенд

`backend/backend.slnx`, `Directory.Packages.props` импортирует props фреймворка. Проекты: `Cluster.Game`, `Game`, `Meta`, `Console`,
`Orchestration/{Aspire,ConsoleGateway,Coordinator,Extensions,GameGateway,MetaGateway,Silo}`, `Tools/{Benchmarks,DeploySetup,Generators,Tests}`.
Ссылки на фреймворк: `shared/framework.shared.csproj`, `backend/{Common,Infrastructure,Cluster,Session}`; нет `common/framework.common.csproj`
и `shared/SessionCore/framework.session.core.csproj` (A1, A3). `DisableTransitiveProjectReferences` - ссылки нужны в каждом csproj.

- 93 файла бэкенда с `using Common.Reactive` (A1: -> `Internal` + ссылка на `framework.common.csproj`).
- Сессии: свои `SessionFactory`, `SessionSearch`, `SessionsCollection`, `SessionEndpoints` (`Game.Global`), `LobbyFactory` (`Meta.Matches`),
  матчмейкинг в `MetaGateway/Matchmaking`. Имена совпадают с типами `backend/Hosting` фреймворка (`ISessionsCollection`, `ISessionSearch`,
  `ILobbyFactory`, `SessionEndpoints`), но в других пространствах имён, а `Hosting` игра не подключает: `CS0433` не ожидается.
- `backend/Tools/Tests`: 151 файл, большая часть - копии тестов фреймворка (каналы, транзакции, сеть, кластер).
- Консоль: свой `ConnectedUsersWidget`, `Home.razor`, бенчмарки на `OperationProgress`.

### Контент: группы Addressables

`client/Assets/AddressableAssetsData`: 89 файлов без `.meta` (28 групп, 52 схемы, настройки, `link.xml`). Поправка к исходным данным:
в `.cs` клиента Addressables упоминает только `client/Assets/Common/Internal/Editor/Tools/WebGlDeploy.cs` (`SharedAddressablesSync.Sync()`),
ещё `BundleUrlVersioning.Setup()` в `Flow/Startup/GameStartup.cs`; своих `AssetReference` в коде и `m_AssetGUID` в ассетах вне
`AddressableAssetsData` нет. Остальные 17 файлов с `Addressable*` - `AddressableState` бэкенда (Orleans, к задаче не относится). Игра
грузит контент только через каталоги и сцены скоупов, поэтому шаг 4 релиза 0.8.0 (свои ссылки) пустой.

| Когда | Группы (Addressables -> каталог) | Кто грузит |
|-------|-------------------------------|------------|
| Старт, корень | `Assets_Internal` (`InternalAssets`: `OptionsContainer`, `NetworkOptions`) | `InternalScopeExtensions.Construct`: `LoadEnvAssetGroup` |
| Старт, параллельно | `Sprites_CardsIcons`, `Sprites_CardBuffs`, `Sprites_MenuPlay`, `Sprites_Portraits`, `Audio_Global` (`GlobalAudio`: `GameMusic` - `Game_Music.wav` 5,76 МБ исходника, звуки UI) | `StartupAssetsPreload` (держит до конца приложения), мета ждёт |
| Старт, глобальный скоуп | `Assets_Global`, `Prefabs_Global` | `GlobalScopeExtensions`: `LoadEnvAssetGroup`, `LoadPrefabGroup` |
| Меню | `Scenes_Menu` (`Menu`, `Menu_Board`), `Prefabs_Menu`, `Prefabs_GamePlay` (меню грузит и его), `Sprites_MenuNavigation`, `Sprites_MenuUnlocks`, `Sprites_Settings` | `MenuScopeExtensions`: `FindOrLoadSceneWithServices(Scenes.X.Value)`, `LoadPrefabGroup` |
| Матч | `Scenes_GamePlay` (`Game_Field`, `Game_Overlay`, `Game_Pause`, `Game_Result`), `Assets_GamePlay`, `Audio_GamePlay`, `Prefabs_GamePlay`, `Sprites_Cards`, `Sprites_GameCells`, `Sprites_GameField`, `Sprites_GameUI`, `Sprites_GameUIPlate` | `GamePlayScopeExtensions`: `Request*Group`, `FindOrLoadSceneWithServices` |
| Общие | `Shared_Common` (19: шейдеры `TMP_Bitmap`, `Sprite`, материал, шрифты `m3x6`, `Ithaca-LVB75`, атласы aseprite, префабы `Random`, `Board_Base`, `Settings_Slider`), `Shared_GamePlay` (`Game_UI_Plate`), `Shared_Menu` (`Menu_Unlocks`) | писал `SharedAddressablesSync` |
| Пусто | `Default Local Group` | - |

`Framework.json` `GroupPhases` делит их на `Menu` (`Scenes_Menu`, `Prefabs_Menu`, `Sprites_MenuNavigation`, `Sprites_MenuUnlocks`) и `GamePlay`
(`Scenes_GamePlay`, `Assets_GamePlay`, `Sprites_Cards`, `Sprites_GameUIPlate`). Сцена Build Settings одна: `Assets/Common/Flow/Startup/Startup.unity`.
json-списки каталогов: `Catalogues/Assets/AssetGroups.json`, `Prefabs/PrefabGroups.json` (ключ `nonAddressable`), `Audio/AudioGroups.json`,
`Sprites/SpriteGroups.json`. Сгенерированный `Catalogues/Scenes/Scenes.cs` - `StaticScene`.

### Сборки и деплой

- Профиль один: `client/Assets/Settings/Build Profiles/web_dev - Desktop - Development.asset` (WebGL, `m_Development: 0`, без defines).
- `.claude/deploy-client.json` нет. `.claude/skills/deploy-client` - старая копия (сборка в открытом редакторе через MCP и
  `Internal.WebGlDeploy` игры), не ссылка. Выкладка - `tools/scripts/deploy-webgl.sh` (Coolify, nginx `client/deploy/nginx.conf`,
  проверка `StreamingAssets/aa/catalog.bin`).
- Стрип: `client/ProjectSettings/Packages/com.unity.web.stripping-tool/Settings.json` и строка форка в манифесте есть (шаг 8 0.4.0 сделан).
- WebGL-шаблон один: `client/Assets/WebGLTemplates/Default` (ветка `cacheControl` для `.bundle`, без `audioSession`).
- CI: `.github/workflows/ci.yml`; тесты `tools/scripts/run-tests.sh` (собирает `ContainerGenerator` в Release без `SkipAnalyzersCopy`).
- `brotli` на машине есть (`/usr/bin/brotli`, шаг 7 0.8.0).

### Claude в игре

- `.claude/skills/*` - копии (22 папки), ни одной ссылки на фреймворк. `update-framework` есть только в локальном незапушенном коммите
  `2f47e207` (копия `SKILL.md`), в `origin/main` нет. `control`, `media-cleanup`, `deploy-client` (новый), `device-lab`, `workflow` нового
  формата - не подключены. `.claude/control.json`, `.claude/project-skills.md`, `.claude/settings.json` нет.
- `CLAUDE.md` игры не импортирует `@tools/framework/.claude/docs/INDEX.md` (A6).
- `control` определяет проект по `git rev-parse --show-toplevel`, `control.json` необязателен: копию можно создать скриптом фреймворка
  `/projects/framework/.claude/skills/control/control copy create <name>` из `/projects/mines-leader` (не проверено).

### Пересечения с задачами игры

- `docs/tasks/current/foundation_port/` (порт фиксов из peace-data-reborn, оркестратор, 32 задачи): почти всё `done`. Открыты C5
  (`Internal.Editor` без ссылки на `Flow` - теперь это сборка фреймворка), T2 (`run-tests.sh`), T3 (защита WebGL-деплоя - заменяет
  `verify.sh` из `deploy-client`, шаг 7 0.8.0), T4 (пропатченные пакеты - форки и `update-override-packages` фреймворка), N13 (возврат в
  идущий матч, дизайн). T2-T4 и C5 этой задачей закрываются или теряют смысл: решить с пользователем (вопрос 6). `editor_owner.txt` той
  задачи ведёт очередь редактора основного дерева - в копии он не нужен.
- `docs/tasks/current/unused_with_parameter/` (только в рабочем дереве, не в git): удалить лишние `WithParameter` после `CINGR015`
  (`ISnapshotHandler.cs:22`, `CardStatesExtensions.cs:29`, 32 действия карт) и проверить конец матча после того, как корни `LocalCard` /
  `RemoteCard` получили `ScopeLifetime`. `CINGR015` и `ScopeLifetime` приходят с этим обновлением (0.1.0, «Проверить поведение»): после
  шага 2 этой задачи список предупреждений сверить с её списком; саму чистку оставить той задаче или влить сюда (вопрос 7).
- `docs/tasks/plan/framework_review_migration.md`: план миграции на правки ревью 2026-10-26 - это A5 релиза 0.1.0 (`updater` бросает
  `OperationCanceledException`, `CardLocalSpawn.cs:55`, `CardRemoteSpawn.cs:50`, `CardLocalDrag.cs:127`, `CardDropArea.cs:53`, ...). Его
  шаги выполняются внутри шага 2 этой задачи.

## Шаги релизов и попадания в игре

Поиск: команда шага из заметок релиза, выполненная как `git -C /projects/mines-leader grep <шаблон> origin/main -- <пути>` (для `grep -r`
шагов 0.8.0 / 0.9.0 - тот же шаблон через `git grep`). Объём: малый - до часа, средний - несколько часов, крупный - день и больше.

### 0.1.0 (с разделом «Указатель старше базы»)

| Шаг | Касается | Попадания | Объём |
|-----|----------|-----------|-------|
| A1 `Framework.Common` | да | 93 файла бэкенда `using Common.Reactive`; ни одного csproj с `framework.common`; манифест без `common`; asmdef без `GUID:3ea5c3f1...` | средний, механический |
| A2 лобби и хостинг | проверить | своих `LobbyCode` нет; свои `SessionsCollection`, `SessionSearch`, `SessionEndpoints`, `LobbyFactory` в `Game.Global` / `Meta.Matches`, `Hosting` не подключён; теги `Session*` / `Lobb*` в union - если протокол их использует | малый (если не переводить на `Hosting`, вопрос 3) |
| A3 ядро сессий | да | ссылка на `framework.session.core.csproj`; `serviceFactory.OnSessionCreated` x3 в `backend/Game/Global/SessionFactory.cs`; 41 файл `Game`/`GameGateway` с `IUser`/`ISessionData`/`IServiceFactory` (`IUser.Connection` -> `IUserConnection`, `ISessionLog`); черновая сборка 0.2.0 стояла на `IUser`, `Service`, `ISessionData` | крупный |
| A4 модуль анимаций | да | 3 файла (`CellAnimator`, `CellVisuals`, `FlagAnimator`) | малый |
| A5 правки ревью | да | план `docs/tasks/plan/framework_review_migration.md`; `docs/changes/2026-10-26-review-fixes.md` фреймворка | средний |
| A6 доки и скиллы | да | нет импорта `INDEX.md`, скиллы - копии | малый |
| 1 `IScope*Async` | да | 2: `Common/Global/Settings/Settings.cs:41`, `Meta/MetaLoop.cs:57` | малый |
| 2 `PlayAsync` | да | 6 в 3 файлах `GamePlay/Boards/Cells` | малый |
| 7 `ButtonSounds.Bind` | да | 4 в `Common/Global/Audio/ButtonSoundsPlayer.cs` | малый |
| 8 `ClientOutdatedNoticeStyle` | да | `Common/Global/UI/GlobalUIExtensions.cs:33` (`AddUI` есть) | малый |
| 9 `ViewableDictionary.Set` | да | `GamePlay/Players/Entity/Modifiers/PlayerModifiers.cs` | малый |
| 10 оси геймпада | да | 0 в `origin/main` (в рабочем дереве другой сессии 10 осей не закоммичены) | малый |
| 12 `SessionData` | да | `backend/Tools/Tests/Game/SessionWorld.cs:32` | малый |
| 13 опции record | да | `backend/Tools/Tests/Fixtures/OrleansTestClusterFixture.cs:189-192` | малый |
| 14 состояния кластера | да | 5 в 4 тестах (`ClusterFeaturesWriterTests`, `DeployCleanupTests`, `DeployHealthCheckerTests`, `ClusterFeaturesStateSerializationTests`) | малый |
| 15 `IEventStateValue.Id` | по желанию | 13 агрегатов `backend/Meta` | пропустить (вопрос 8) |
| 16 ссылки `update-framework`, `control` | да | нет ни одной | малый |
| 3, 4, 5, 6, 11, A7 | нет | 0 попаданий | - |

### 0.2.0, 0.2.1, 0.2.2

| Шаг | Касается | Попадания | Объём |
|-----|----------|-----------|-------|
| 0.2.0 #3 контракты каналов | да | `IsPipeExists` x2 в `backend/Tools/Tests/Messaging/RuntimePipeTests.cs` | малый |
| 0.2.0 #5 `.gitignore` Transient | да | `client/Assets/Common/Internal/Editor/Transient*` не игнорируется | малый |
| 0.2.0 #9 `ConnectionWriter.Run(lifetime)` | да | 8 в `backend/Tools/Tests/Network/ConnectionWriterTests.cs` (вызовы уже с lifetime - проверить компилятором) | малый |
| 0.2.0 #10 копии `control` | да | `control copy install` один раз на машину | малый |
| 0.2.0 #7 разметка спрайтов | нет | 98 записей: 89 `Sheet`, 9 `Animation`, все допустимы | - |
| 0.2.1 #1 ссылка `media-cleanup` | да | ссылки нет | малый |
| 0.2.0 #1, 2, 4, 6, 8; 0.2.2 | нет | 0 попаданий / без действий | - |

### 0.3.0, 0.4.0

| Шаг | Касается | Попадания | Объём |
|-----|----------|-----------|-------|
| 0.3.0 #1 `GameProfiler.Stage` | да | 17 в 9 файлах (`GameLoopScopeExtensions`, `GamePlayLoader`, ...: `Scope("Game loop")`, `Scope("Load")`) | малый |
| 0.4.0 #4 `IScreenCanvasCamera` | да | `AddNetwork` в `InternalScopeExtensions.cs:31`, `AddClientOutdatedNotice` в `GlobalUIExtensions.cs:24`: нужен `AddScreenCanvasCamera` или `AddScreenCanvasOverlay` (вопрос 5) | малый |
| 0.4.0 #5 шрифт оверлея задержки | да | `AddSessionServices` в `GamePlay/Loop/GamePlayScopeExtensions.cs`, поля `_latencyOverlayFont` в ассете `NetworkOptions` нет | малый |
| 0.4.0 #6 `deploy-client.json` | да | нет файла, скилл - старая копия | малый |
| 0.3.0 #2, #3; 0.4.0 #1, #2, #3, #7, #8, #9, #11 | нет | 0 попаданий (шаблон без телеметрии не трогать, `audioSession` нет, стрип настроен) | - |
| 0.4.0 #10 `device-lab` | по желанию | - | - |

### 0.5.0, 0.5.1, 0.6.0

| Шаг | Касается | Попадания | Объём |
|-----|----------|-----------|-------|
| 0.5.0 #3 `Aspire.AppHost.Sdk` | да | `backend/Orchestration/Aspire/Aspire.csproj:1` - `13.5.3`, поднять до версии фреймворка | малый |
| 0.6.0 #1 Home консоли, `AddOnlineTracker` | да | 15 в 9 файлах: `Console/Home/ConnectedUsersWidget.razor`, `Home.razor:12`, `SetupCoordinator` в `Orchestration/Extensions/ProjectsSetupExtensions.cs:30`; `DeployCleanupTests` - список deploy-scoped типов | средний |
| 0.5.0 #1, #2, #4; 0.5.1 | нет | 0 (одно ложное `resetResolutionOnWindowResize` в профиле) | - |
| 0.5.0 #5 `audit-swarm` | по желанию | - | - |

### 0.7.0

| Шаг | Касается | Попадания | Объём |
|-----|----------|-----------|-------|
| #1, #2 Regenerate All с валидатором | да | проверка в редакторе (`HasPending`/`RegenerateAll` в скриптах игры нет) | малый-средний |
| #5 контракт соединения | да | `MetaGateway/UserFlow/Connection/BackendConnectionMiddleware.cs:154`, `GameGateway/SessionGate.cs:79`, `MetaGateway/UserFlow/Commands/UserCommandsDispatcher.cs:30,96`, `UserConnectionEntryPoint.cs`, тесты `ConnectionReaderTests`, `ConnectionWriterTests`, `UserConnectionEntryPointTests` (20 строк, 5 файлов) | средний |
| #6 `OperationProgress` | да | 44 файла: `Tools/Benchmarks`, `Console/Infrastructure/Benchmark/*.razor` - перенести копию в `Tools/Benchmarks/Common` | средний |
| #7 `Reserve` | да | 7 в 4 файлах (`SessionFactory.cs:172,274`, `SessionSearch.cs:31,42`, ...) | малый |
| #8 состояния кластера | да | `IsDevelopment = true` в `OrleansTestClusterFixture.cs:131`; `Members`/`Slices` в `DeployCleanupTests.cs:131,147` | малый |
| #9 метры у фич, `RequeueStuck` | да | `Orchestration/Extensions/ServiceDefaultsExtensions.cs:79,82`; `Coordinator/ClusterCoordinator.cs:40`, `Tests/State/SideEffectTests.cs:178` | малый |
| #10 `AddConsoleInfrastructure` | да | `ConsoleGateway/Program.cs:47-50`, `Tools/DeploySetup/AuditLogSetup.cs`, `ConsoleGateway/App.razor:35`, `Console/Game/Configs/Configs.razor:260` | малый |
| #3, #11, `LobbyJoinStatus`, `IDeployContext` | нет | 0 | - |
| #4 `match-metrics` | по желанию | - | - |

### 0.8.0

| Шаг | Касается | Попадания | Объём |
|-----|----------|-----------|-------|
| #1 ассеты `ContentBundle` | да | 0 ассетов; `SharedCommonPaths`, `GroupPhases` в `Framework.json`; `FrameworkContentStamp` нет в `.gitignore` | средний (раскладка) |
| #2 `AddContent`, `IContentLoader` | да | `InternalScopeExtensions.cs` (корень), `FindOrLoadSceneWithServices` x4 `GamePlayScopeExtensions.cs:123-126`, x2 `Menu/Common/MenuScopeExtensions.cs:59-60` | малый |
| #3 каталоги на бандлах | да | `Scenes.cs` (`StaticScene`, сгенерирован), `Scenes.X.Value` x6, `nonAddressable` в `AssetGroups.json`, `PrefabGroups.json` | малый (Regenerate All) |
| #5 удалить Addressables | да | манифест, 7 asmdef, `AddressableAssetsData/link.xml`, `BundleUrlVersioning.Setup()` в `GameStartup.cs`, `SharedAddressablesSync.Sync()` в `WebGlDeploy.cs`, папка `AddressableAssetsData` | средний |
| #6 шаблон, nginx, деплой | да | `WebGLTemplates/Default/index.html:60-64`, `client/deploy/nginx.conf:87-92`, `client/deploy/README.md:63`, `tools/scripts/deploy-webgl.sh:114-120` | малый |
| #10 `Internal.UGUI` | да | 17 файлов: `Global` (1), `GamePlay` (8), `Menu` (8) - GUID `1a5b96d3...` в 3 asmdef | малый |
| #11 `StableHash` / `common` | да | `common` нет в манифесте; `backend/Tools/Tests/Protocol/StableHashTests.cs` | малый (вместе с A1) |
| #14 пакет `layout` | да | `ui` без `layout` в манифесте; 12 `*Bindings.g.cs` (`GamePlay/Assets/Generated` 6, `Menu/Assets/Generated` 6) - GUID `632dc5ed...` в `GamePlay`, `Menu` | малый |
| #16 `SkipAnalyzersCopy` | да | `tools/scripts/run-tests.sh:218,223` | малый |
| #8 доп. бандлы `AddContentBundle` | по желанию | музыка `Game_Music.wav` в `GlobalAudio` (вопрос 2) | малый |
| #4, #7, #9, #12, #13, #15 | нет | 0 (`AssetReference` нет; `brotli` есть; лямбда `GamePlayLoader.cs:45` - загрузчик игрового цикла, не корень) | - |

### 0.9.0

| Шаг | Касается | Попадания | Объём |
|-----|----------|-----------|-------|
| #2 `IContentSharedBundles` | да, как новый класс | своего синка нет; раскладка из нескольких бандлов потребует общих бандлов (как `GameSharedBundles` pixel-swarm) | малый |
| #1, #3 | нет | 0 (`IContentBuildStep`, `ContentBuild.*` в игре нет) | - |

Итого по объёму: бэкенд - крупный (A1, A3, 0.7.0), клиент до 0.7.0 - средний, контент 0.8.0 - средний (кода мало, основное - раскладка и
проверка загрузок), деплой и замер - средний.

## План реализации

#### [0] Копия проекта и ветка
- **Статус:** [x] done (копия `/var/tmp/unity-copies/mines-leader/framework-0-9-0`)
- **Цель:** своя копия mines-leader с редактором на ветке от `origin/main`, основное дерево не тронуто.
- **Как:** из `/projects/mines-leader`: `/projects/framework/.claude/skills/control/control copy create framework-0-9-0` (если не
  заработает без `control.json` - `git worktree` + копия `client/Library` по памяти «Parallel sessions»). В копии: `git switch -c
  framework-0.9.0 origin/main`; незакоммиченные правки другой сессии, которые `create` перенёс (`GameRoles.cs`, `InputManager.asset`,
  указатель), убрать из копии (`git restore`, `git -C tools/framework checkout a11e507a`), не трогая основное дерево. Один раз на машину
  `control copy install` (0.2.0 #10).
- **Проверка:** `git -C <root> status` чистый, `git -C <root> ls-tree HEAD tools/framework` = `a11e507a`, `Application.dataPath` редактора
  копии внутри `root`.
- **Файлы:** -
- **Зависит от:** -
- **Блокирует:** 1-17

#### [1] Замер «до»: WebGL на Addressables
- **Статус:** [x] done (`b1246bdf`)
- **Цель:** сборка `origin/main` и её размеры в gzip записаны до любых правок.
- **Как:** batchmode `Unity -batchmode -projectPath client -activeBuildProfile "Assets/Settings/Build Profiles/web_dev - Desktop - Development.asset"
  -executeMethod Internal.WebGlBuild.Build` (есть и в `a11e507a`) в копии; сборку сохранить вне git (`builds/web-<дата>-addressables`).
  Посчитать как pixel-swarm 2026-11-16/17: движок (`.br` распаковать, gzip -9), бандлы `StreamingAssets/aa` (LZ4 + gzip -9 поверх и «как
  отдаётся сейчас»), служебное Addressables, по этапам: до меню, до матча. Только gzip, МБ = 10^6.
- **Проверка:** `docs/web_build_size/<дата>/stats.json` с числами «до»; лог сборки без ошибок.
- **Файлы:** `docs/web_build_size/<дата>/stats.json` [новый файл]
- **Зависит от:** 0
- **Блокирует:** 16

#### [2] Обновление до 0.7.0, клиент
- **Статус:** [x] done (`4faa00d1`..`294c0608`; ветка бэкенда влита `7912a976`; дымовая проверка - шаг 2b)
- **Цель:** указатель на `v0.7.0`, клиент компилируется (Addressables форка ещё в `v0.7.0`).
- **Как:** `/update-framework 0.7.0` по `update-framework` фреймворка (в игре скилла нет - `git -C tools/framework show v0.7.0:.claude/skills/update-framework/SKILL.md`,
  раздел «Как обновить игру» `releases/README.md`). Шаги клиента: A1 (манифест `com.noncasted.framework.common`, `GUID:3ea5c3f1...` в каждый
  asmdef со ссылкой на `Internal`), A4 (`com.noncasted.framework.animations` + asmdef `GamePlay`), A5 (по `framework_review_migration.md`),
  0.1.0 #1, #2, #7, #8, #9, #10 (блок осей из `InputManager.asset` pixel-swarm), 0.2.0 #5, 0.3.0 #1, 0.4.0 #4, #5, 0.7.0 #1-#2.
- **Проверка:** компиляция редактора копии без ошибок; список `CINGR*` предупреждений записан и сверен с `unused_with_parameter`;
  Regenerate All заканчивается `0 validation errors`.
- **Файлы:** `client/Packages/manifest.json`, `client/Assets/**/*.asmdef`, `Common/Global/Settings/Settings.cs`, `Meta/MetaLoop.cs`,
  `GamePlay/Boards/Cells/{CellAnimator,CellVisuals}.cs`, `GamePlay/Boards/Cells/Taken/FlagAnimator.cs`, `Common/Global/Audio/ButtonSoundsPlayer.cs`,
  `Common/Global/UI/GlobalUIExtensions.cs`, `GamePlay/Players/Entity/Modifiers/PlayerModifiers.cs`, `Common/Internal/Runtime/InternalScopeExtensions.cs`,
  ассет `NetworkOptions`, `client/ProjectSettings/InputManager.asset`, `.gitignore`, файлы `GameProfiler.Scope` (9), шаги `framework_review_migration.md`
  (`CardLocalSpawn.cs`, `CardRemoteSpawn.cs`, `CardLocalDrag.cs`, `CardDropArea.cs`, ...)
- **Зависит от:** 0, 1
- **Блокирует:** 4, 5

#### [3] Обновление до 0.7.0, бэкенд
- **Статус:** [x] done (ветка `framework-0.9.0-backend`, `9d1c68c3`)
- **Цель:** `dotnet build backend/backend.slnx` - 0 ошибок на `v0.7.0`.
- **Как:** A1 (`using Common.Reactive` -> `using Internal` в 93 файлах, `framework.common.csproj` в каждый csproj), A3 (`framework.session.core.csproj`,
  `IUserConnection`, `ISessionLog`, `IExecutionQueue.SetupServices(factory)` вместо `OnSessionCreated`, теги `PeerSignal` 70 / `PeerTunnel` 71 в
  union), A2 (теги `Session*` / `Lobb*` в union, если нужны протоколу), 0.1.0 #12-#14, 0.2.0 #3, #9, 0.5.0 #3, 0.6.0 #1, 0.7.0 #5-#10. Проход
  «сборка - правка» до нуля ошибок, как в заметках 0.7.0.
- **Проверка:** `dotnet build backend/backend.slnx` 0 ошибок; бэкенд-тесты игры (`run-tests.sh` стадия backend) зелёные, число тестов записано
  до и после (удалённые тесты серверных запросов 0.7.0 #5 - с причиной).
- **Файлы:** `backend/**/*.csproj`, 93 файла `using Common.Reactive`, `backend/Game/Global/{SessionFactory,SessionSearch}.cs`, `backend/Game/**`,
  `backend/Orchestration/MetaGateway/UserFlow/**`, `backend/Orchestration/GameGateway/SessionGate.cs`, `backend/Orchestration/Extensions/{ServiceDefaultsExtensions,ProjectsSetupExtensions}.cs`,
  `backend/Orchestration/Coordinator/ClusterCoordinator.cs`, `backend/Orchestration/ConsoleGateway/{Program.cs,App.razor}`, `backend/Console/Home/{Home.razor,ConnectedUsersWidget.razor}`,
  `backend/Console/Game/Configs/Configs.razor`, `backend/Tools/DeploySetup/AuditLogSetup.cs`, `backend/Tools/Benchmarks/Common/` [новый файл `OperationProgress.cs`],
  `backend/Orchestration/Aspire/Aspire.csproj`, `backend/Tools/Tests/**`, `shared/Protocol` (union)
- **Зависит от:** 0
- **Блокирует:** 5, 13

#### [4] Скиллы и доки Claude
- **Статус:** [x] done (`4d1aefe1`)
- **Цель:** игра пользуется общими скиллами фреймворка по ссылкам.
- **Как:** ссылки `update-framework`, `control`, `media-cleanup`, `deploy-client`, `orchestrate` (заменить копии: `orchestrate` скопирован 2026-11-17 для запуска этой задачи,
  в старом сабмодуле его нет), по желанию `device-lab`, `audit-swarm`;
  копии, у которых есть общий скилл (`commit`, `workflow*`, `split-commit`, `start-cluster`, `pr`, `branch-commit`, `gitstat`, `improve-prompt`,
  `benchmark-analyse`) - заменить ссылками (вопрос 4); `.claude/control.json`, разрешение `Bash(.claude/skills/control/control *)` в
  `.claude/settings.json`, `.claude/project-skills.md`; импорт `@tools/framework/.claude/docs/INDEX.md` в `CLAUDE.md` (A6). Локальный коммит
  `2f47e207` основного дерева становится не нужен (сказать пользователю).
- **Проверка:** `ls -l .claude/skills` - ссылки на `../../tools/framework/.claude/skills/*`; `git ls-files -s .claude/skills | grep ^120000`.
- **Файлы:** `.claude/skills/*`, `.claude/control.json` [новый], `.claude/settings.json` [новый], `.claude/project-skills.md` [новый], `CLAUDE.md`
- **Зависит от:** 2
- **Блокирует:** 14

#### [5] 0.8.0: указатель и манифест без Addressables
- **Статус:** [x] done (`d8d29706`)
- **Цель:** указатель на `v0.8.0`, Addressables нет в манифесте и asmdef, код компилируется (как `fc2bf844c` + `5755d34ef` + `16375cee1` pixel-swarm).
- **Как:** `git -C tools/framework checkout --detach v0.8.0`; манифест: убрать `com.unity.addressables`, добавить `com.noncasted.framework.layout`
  (`common` уже с шага 2); `packages-lock.json` перепишет Unity (`scriptablebuildpipeline` на месте); из 7 asmdef убрать GUID `9e24947d...`,
  `84651a37...`, `69448af7...`; добавить `Internal.UGUI` (`1a5b96d3...`) в `Global`, `GamePlay`, `Menu`, `Internal.Layout` (`632dc5ed...`) в
  `GamePlay`, `Menu`; `Framework.json` без `SharedCommonPaths`, `GroupPhases`; `FrameworkContentStamp` в `.gitignore`; бэкенд - 0.8.0 #11.
- **Проверка:** компиляция без ошибок `Unity.Addressables` / `ListenClick` / `DesignContainer`; `grep -n scriptablebuildpipeline client/Packages/packages-lock.json`.
- **Файлы:** `client/Packages/manifest.json`, `client/Packages/packages-lock.json`, 7 asmdef, `client/ProjectSettings/Framework.json`, `.gitignore`
- **Зависит от:** 2, 3
- **Блокирует:** 6

#### [6] 0.8.0: код загрузки - `AddContent`, каталоги
- **Статус:** [x] done (`aa5116aa`; создан `Main.asset`, музыка - группа `Music`)
- **Цель:** корень и скоупы грузят контент через `IContentLoader`, каталоги перегенерированы.
- **Как:** `InternalScopeExtensions.Construct`: константа `ContentName = "Main"` и `await builder.AddContent(new ContentOptions(ContentName))`
  до `LoadEnvAssetGroup(InternalAssets.Group)` (образец - `InternalScopeExtensions.cs` pixel-swarm); `nonAddressable` -> `preloaded` в
  `AssetGroups.json`, `PrefabGroups.json`; Regenerate All (шаг `Content root` вместо `Shared addressables`: `ContentRoot.asset`, классы групп с
  `AssetGuid` вместо `Address`, ассеты групп переезжают из `Resources` в папки каталогов, пустые `Resources` удалить); убрать
  `BundleUrlVersioning.Setup()` из `GameStartup.cs`; `StartupAssetsPreload` - решить, какие группы `preloaded` (вопрос 1). Порядок
  шагов 6-9 - по разделу «Порядок контента» `releases/0.8.0.md`: сначала код до компиляции, потом ассеты бандлов, `AddressableAssetsData` последней.
- **Проверка:** компиляция; Play Mode: старт и меню грузятся (`EditorContentSource`); стартовый трейс содержит `Content: catalog`,
  `Content: bundle Main`, `Content: bind catalogues`.
- **Файлы:** `client/Assets/Common/Internal/Runtime/InternalScopeExtensions.cs`, `.../Catalogues/**` (сгенерированные), `.../Catalogues/Assets/AssetGroups.json`,
  `.../Catalogues/Prefabs/PrefabGroups.json`, `client/Assets/Common/Flow/Startup/GameStartup.cs`, `.../Catalogues/Shared/StartupAssetsPreload.cs`
- **Зависит от:** 5
- **Блокирует:** 7

#### [7] 0.8.0: сцены скоупов на `ContentScene`
- **Статус:** [x] done (`30b98615`)
- **Цель:** сцены меню и матча грузятся как `ContentScene`, `StaticScene` и перегрузки с `AssetReference` не используются.
- **Как:** после Regenerate All `Scenes.cs` - `ContentScene` (6 сцен: `GameField`, `GameOverlay`, `GamePause`, `GameResult`, `Menu`, `MenuBoard`;
  `Startup` из Build Settings в `Scenes` больше нет - проверить, что его никто не читает); `Scenes.X.Value` -> `Scenes.X` в
  `GamePlayScopeExtensions.cs:123-126`, `MenuScopeExtensions.cs:59-60`; моки сцен (`MockEntry`, если открывают сцену напрямую) - `scene.EditorPath`.
  Сцены с `m_LightingDataAsset {fileID: 20201}` без запечённого света очистить (иначе встроенный кубмап ~60 КиБ br в бандл каждой сцены).
- **Проверка:** компиляция; Play Mode: меню -> матч -> результаты -> меню, повторный матч; открытие `Game_Field` напрямую (мок) работает;
  `git grep -nE 'StaticScene|Scenes\.\w+\.Value' -- client/Assets` пусто.
- **Файлы:** `client/Assets/Common/Internal/Runtime/Catalogues/Scenes/Scenes.cs` (сгенерирован), `client/Assets/GamePlay/Loop/GamePlayScopeExtensions.cs`,
  `client/Assets/Menu/Common/MenuScopeExtensions.cs`, `client/Assets/{Menu,GamePlay}/Assets/Scenes/*.unity`
- **Зависит от:** 6
- **Блокирует:** 8

#### [8] 0.8.0: раскладка бандлов
- **Статус:** [x] done (`8f13d64a`; + бандл `GamePlayViews`, общие вручную)
- **Цель:** ассеты `ContentBundle` покрывают весь контент, раскладка без дублей, `Main` тонкий.
- **Как:** ассеты в `client/Assets/Common/Internal/Editor/Content/` (как pixel-swarm). Предложение (подтвердить, вопрос 1):
  `Main` (корень `ContentRoot.asset`, `IsPreloaded`: внутренние, глобальные опции и префабы, стартовые спрайты, звуки UI),
  `Menu` (`Menu.unity`, `Menu_Board.unity`, группы меню, gzip), `GamePlay` (4 сцены матча, группы матча, gzip), `Music` (группа с
  `Game_Music`, без gzip, `AddContentBundle`, вопрос 2); общие - через `IContentSharedBundles` (шаг 11). Сцены, которых нет в Build
  Settings, - корни своих бандлов. Записать, что из `Shared_Common` куда попало.
- **Проверка:** `[ContentBuild] N content bundles for WebGL` и по строке на бандл без ошибок раскладки; предупреждение про встроенные
  ресурсы отсутствует или разобрано (сцены - шаг 7).
- **Файлы:** `client/Assets/Common/Internal/Editor/Content/*.asset` [новые]
- **Зависит от:** 7
- **Блокирует:** 9, 11

#### [9] 0.8.0: удалить Addressables
- **Статус:** [x] done (`3c50f42d`)
- **Цель:** в проекте нет ничего от Addressables (как `64dbd76aa` pixel-swarm).
- **Как:** `WebGlDeploy.cs` - убрать `SharedAddressablesSync.Sync()` или удалить весь класс в пользу `Internal.WebGlBuild` + `deploy-client`
  (шаг 14); удалить `client/Assets/AddressableAssetsData` (с `link.xml`) последним; проверить, что своего `link.xml` с `Unity.Addressables`
  / `Unity.ResourceManager` больше нет.
- **Проверка:** `git grep -nE 'Addressab|AssetReference|ResourceManagement|BundleUrlVersioning' -- client ':!client/Packages/packages-lock.json'` - пусто,
  кроме `AddressableState` бэкенда; `ls -d client/Assets/AddressableAssetsData` - нет; компиляция.
- **Файлы:** `client/Assets/AddressableAssetsData/**` (удаление), `client/Assets/Common/Internal/Editor/Tools/WebGlDeploy.cs`
- **Зависит от:** 8
- **Блокирует:** 10

#### [10] 0.8.0: link.xml и стрип
- **Статус:** [x] done (`d49112d0`)
- **Цель:** плеер не теряет типы бандлов и не тащит лишнего.
- **Как:** `ContentLinkerProcessor` пишет link.xml типов бандлов сам: проверить в логе сборки; `StrippedPlayerAssemblies` в `Framework.json`
  - `Internal.EncodedAudio`, если в аудиогруппах нет mp3 (`*.mp3.bytes`), проверить; `SubmodulesToStrip` в настройках стрипа не пустой.
- **Проверка:** WebGL-сборка проходит, в Play на собранной странице нет `Could not produce class` / `missing type`; размер `wasm` записан.
- **Файлы:** `client/ProjectSettings/Framework.json`, `client/ProjectSettings/Packages/com.unity.web.stripping-tool/Settings.json`
- **Зависит от:** 9
- **Блокирует:** 12

#### [11] 0.9.0: указатель и общие бандлы
- **Статус:** [x] done (`dc23e91c`, `f5069a20`)
- **Цель:** указатель на `v0.9.0`, общие бандлы пишет `ContentSharedSync` по классу игры.
- **Как:** `git -C tools/framework checkout --detach v0.9.0`; `client/Assets/Common/Internal/Editor/Content/GameSharedBundles.cs` [новый
  файл - сборка `Internal.Content.Editor`] по образцу pixel-swarm: например `SharedCommon` (preloaded, serves `Main`), `SharedMenu`
  (gzip, serves `Menu`), `SharedGamePlay` (gzip, остальное); `Tools/Framework/Codegen/Sync Shared Content Bundles`. Шаги 0.9.0 #1, #3 -
  0 попаданий.
- **Проверка:** второй запуск Sync не меняет файлов; сборка без ошибок раскладки (`ContentBundleLayout.Inspect` - пустые `Duplicates`).
- **Файлы:** `client/Assets/Common/Internal/Editor/Content/GameSharedBundles.cs` [новый], `client/Assets/Common/Internal/Editor/Content/Shared*.asset` [новые]
- **Зависит от:** 8
- **Блокирует:** 12

#### [12] Шаблон WebGL, nginx, деплой
- **Статус:** [x] done (`e959eaa4`; проверка на сборке - шаг 14)
- **Цель:** страница и сервер отдают `StreamingAssets/Content` (как `d66658b0f` pixel-swarm).
- **Как:** `WebGLTemplates/Default/index.html` - убрать ветку `cacheControl` для `.bundle`; `client/deploy/nginx.conf` - правила
  `StreamingAssets/aa` заменить на `location ^~ /StreamingAssets/Content/` (`Cache-Control: public, max-age=31536000, immutable`, `try_files $uri =404`);
  `client/deploy/README.md`; `tools/scripts/deploy-webgl.sh` - убрать проверку `catalog.bin`.
- **Проверка:** `git grep -nE 'aa/|catalog\.bin|UnityCache' -- client/Assets/WebGLTemplates client/deploy tools/scripts` - пусто; локальный
  nginx отдаёт бандл `200`, отсутствующий файл `404`; второе открытие страницы - ни одного запроса к `Content/`.
- **Файлы:** `client/Assets/WebGLTemplates/Default/index.html`, `client/deploy/nginx.conf`, `client/deploy/README.md`, `tools/scripts/deploy-webgl.sh`
- **Зависит от:** 10, 11
- **Блокирует:** 14

#### [13] Тесты
- **Статус:** [x] done (`fe55ee6b`, `2519d65c`)
- **Цель:** все тесты игры зелёные на `v0.9.0`.
- **Как:** `tools/scripts/run-tests.sh` с `-p:SkipAnalyzersCopy=true` у сборки `ContainerGenerator` (0.8.0 #16); EditMode `Internal.Content.Tests`
  и тесты пакетов из `testables` (`core`, `network`); watchdog скрипта не должен считать зависанием компиляцию SBP (как `ef0ef54f8` pixel-swarm).
- **Проверка:** таблица стадий `run-tests.sh` без FAIL; числа тестов «до / после» в `_progress.md`.
- **Файлы:** `tools/scripts/run-tests.sh`, `.github/workflows/ci.yml` (если собирает генератор)
- **Зависит от:** 3, 11
- **Блокирует:** 16

#### [14] `deploy-client` и сборки всех платформ
- **Статус:** [x] done с оговоркой (`42040995`; `verify.sh` фреймворка не читает каталог из UnityFS - фикс во фреймворке)
- **Цель:** WebGL собирается `deploy-client.sh` фреймворка с проверкой бандлов.
- **Как:** `.claude/deploy-client.json` (схема в начале `tools/framework/tools/scripts/deploy-client.sh`, образец pixel-swarm): `tests` =
  `tools/scripts/run-tests.sh`, `deploy` = `tools/scripts/deploy-webgl.sh`, одна цель `web` с профилем `web_dev - Desktop - Development.asset`;
  `tools/framework/tools/scripts/deploy-client.sh web --no-deploy`. Платформа одна (WebGL; itch - только сохранения в PlayerPrefs);
  standalone-сборки нет (вопрос 9).
- **Проверка:** `deploy-client.sh` PASS: тесты, сборка, `verify.sh` (бандлы в `StreamingAssets/Content`, `UnityFS`, `.gz` - их gzip); выкладка
  только по просьбе пользователя.
- **Файлы:** `.claude/deploy-client.json` [новый], `.claude/project-skills.md`
- **Зависит от:** 4, 12, 13
- **Блокирует:** 15, 16

#### [15] Проверка поведения
- **Статус:** [x] done (регрессий нет; `GameRandom`, `FlagAnimator`, Home консоли не проверены)
- **Цель:** изменения поведения из «Проверить поведение» всех релизов не сломали игру.
- **Как:** на собранной странице (локальный nginx, `device-lab` по желанию) и в Play Mode: старт, меню, PvP и бот-матч, пауза, результаты,
  выход и повторный вход в матч (`ScopeLifetime` корней карт, 0.1.0), вложенные окна и «назад» (`ProcessStack`), звук и музыка, переподключение;
  бэкенд: закрытие соединений кодом 1000 (0.7.0), лобби `NotFound` вместо `Full`, Home консоли (`HomeClusterSection`, `OnlineWidget`).
- **Проверка:** список сценариев с результатом в `_progress.md`; лог страницы без ошибок.
- **Файлы:** -
- **Зависит от:** 14
- **Блокирует:** 17

#### [16] Замер «после» и отчёт
- **Статус:** [x] done (`49c7551c`)
- **Цель:** отчёт размера WebGL в gzip до и после.
- **Как:** та же методика, что в шаге 1 и в `/projects/pixel-swarm/docs/web_build_size/2026-11-17/report.md`: движок с контентом в `.data` и без,
  бандлы `.gz` как есть, этапы по каталогу (`Resources/Content/catalog.json` в `.data`): до меню, до матча; запросов контента до меню; крупнейшие
  бандлы и их аналоги в Addressables. Только gzip. UnityPy не читает UnityFS Unity 7000 (заметка pixel-swarm): разбор бандлов - своим скриптом
  вне git или по логу `[ContentBuild]`.
- **Проверка:** `docs/web_build_size/<дата>/report.md`, `stats.json`; итог «весь билд», «до меню», «до матча» с разницей.
- **Файлы:** `docs/web_build_size/<дата>/report.md` [новый], `docs/web_build_size/<дата>/stats.json`
- **Зависит от:** 1, 14
- **Блокирует:** 17

#### [17] Коммиты и сдача
- **Статус:** [x] done (ветка `copy/framework-0-9-0/framework-0.9.0`, в main не вливалась)
- **Цель:** работа в ветке игры, готова к вливанию.
- **Как:** коммиты по этапам, как в pixel-swarm (`[Framework] 0.7.0 ...`, `[Framework] 0.8.0: pointer, manifest ...`, `[Content] ...`,
  `[Content] Addressables removed`, `[Deploy] ...`, `[Framework] 0.9.0 ...`, `[Docs] Web build size ...`); коммиты копии сами появятся в
  проекте веткой `copy/framework-0-9-0/framework-0.9.0`. В `main` и `origin` - только по просьбе пользователя; `copy remove` после сдачи.
- **Проверка:** `git branch --list 'copy/*'` в проекте; `git log --oneline origin/main..copy/framework-0-9-0/framework-0.9.0`.
- **Файлы:** -
- **Зависит от:** 15, 16
- **Блокирует:** -

### Ключевые файлы

| Файл | Роль в задаче |
|------|---------------|
| `client/Packages/manifest.json` | пакеты фреймворка: `common`, `layout`, `animations` добавить, Addressables убрать |
| `client/ProjectSettings/Framework.json` | настройки редактора фреймворка: убрать `SharedCommonPaths`, `GroupPhases` |
| `client/Assets/Common/Internal/Runtime/InternalScopeExtensions.cs` | корневой скоуп: `AddContent`, `AddScreenCanvas*`, `AddNetwork` |
| `client/Assets/Common/Flow/Startup/GameStartup.cs` | старт: `BundleUrlVersioning.Setup()` убрать |
| `client/Assets/Common/Internal/Runtime/Catalogues/**` | каталоги: json-списки, сгенерированные группы, `Scenes.cs`, `StartupAssetsPreload.cs` |
| `client/Assets/GamePlay/Loop/GamePlayScopeExtensions.cs`, `client/Assets/Menu/Common/MenuScopeExtensions.cs` | сцены и группы скоупов матча и меню |
| `client/Assets/Common/Internal/Editor/Tools/WebGlDeploy.cs` | старый деплой из редактора: заменить `deploy-client` |
| `client/Assets/AddressableAssetsData/` | группы Addressables: источник раскладки, затем удалить |
| `client/Assets/WebGLTemplates/Default/index.html`, `client/deploy/nginx.conf`, `tools/scripts/deploy-webgl.sh` | страница, сервер, выкладка |
| `tools/scripts/run-tests.sh` | тесты игры, `SkipAnalyzersCopy` |
| `backend/**/*.csproj`, `backend/Game/Global/SessionFactory.cs`, `backend/Orchestration/**` | бэкенд: A1, A3, 0.7.0 |
| `/projects/framework/releases/0.1.0.md` ... `0.9.0.md` | шаги релизов |
| `/projects/pixel-swarm/client/Assets/Common/Internal/Editor/Content/` | образец ассетов `ContentBundle` и `GameSharedBundles.cs` |

### Документация к прочтению
- `tools/framework/.claude/docs/CONTENT.md` - модель бандлов, раскладка, `AddContent`, загрузчики, хранилища веба.
- `tools/framework/releases/README.md` и `releases/0.1.0.md` ... `0.9.0.md` - шаги и поиски.
- `tools/framework/.claude/docs/COMMON_CONTAINER.md` - `CINGR*` после нового генератора (`CINGR015`, `CINGR017`, `CINGR001` сцены без сервисов).
- `tools/framework/.claude/docs/COMMON_LIFETIMES.md` - отмена ожиданий `updater` и `ScopeLifetime` (A5).
- `tools/framework/.claude/docs/WEBGL_TROUBLESHOOTING.md` - бандлы, Cache API, стрип.
- `tools/framework/.claude/docs/COMMON_ORLEANS.md`, `MESSAGING.md` - бэкенд 0.7.0.
- `tools/framework/.claude/docs/BLAZOR.md` - консоль (0.6.0, 0.7.0 #10).
- `tools/framework/.claude/skills/update-framework/SKILL.md`, `control/SKILL.md`, `deploy-client/SKILL.md`.
- `/projects/pixel-swarm/docs/tasks/complete/framework_0_8_0.md`, `/projects/pixel-swarm/docs/web_build_size/2026-11-17/report.md`.

### Риски
- **Бэкенд A3 - самая большая неизвестная.** Сессии игры написаны на API `Game.Session` до выноса ядра в `Framework.Session.Core`; черновая
  сборка 0.2.0 остановилась на `IUser`, `Service`, `ISessionData`. Сколько ошибок за ними - не видно, пока A1 не снят (заметки 0.1.0-0.8.0
  все упираются в `CS0012` A1). Сложить время на несколько проходов сборки.
- **Копии тестов фреймворка в `backend/Tools/Tests`** (151 файл) ломаются почти от каждого релиза (0.1.0 #12-#14, 0.2.0, 0.6.0, 0.7.0 #5, #8, #9).
  Чинить их дороже, чем удалить те, что дублируют `tools/framework/backend/Tests` (вопрос 10).
- **Поведение `updater`, `ScopeLifetime`, `ProcessStack` (A5, 0.1.0)** не видно компилятору: код после `await RunDelay` в картах не выполнится
  при отмене. Нужен ручной проход по `framework_review_migration.md` и проверка матча.
- **Раскладка бандлов**: ассеты `Shared_Common` (шрифты, шейдеры, атласы, `Board_Base.prefab`) нужны и меню, и матчу; без `IContentSharedBundles`
  сборка падает на дублях. Префабы `GamePlay` меню грузит заранее (`MenuScopeExtensions.cs:62`): бандл `GamePlay` уйдёт в загрузку меню, если
  не развести префабы и сцены.
- **Музыка 5,76 МБ исходника в `GlobalAudio`**, которую держит `StartupAssetsPreload`: в `Main` она увеличит первую загрузку, особенно с
  `IsPreloaded` (в `.data`).
- **Копия и чужие правки.** `control copy create` переносит незакоммиченное основного дерева (оси `InputManager.asset`, `GameRoles.cs`, указатель
  `c5b7c94`) - в копии их сбросить, основное дерево не трогать. `c5b7c94` в основном дереве другой сессии - не на указателе проекта.
- **Old deploy**: `Internal.WebGlDeploy` и старая копия `deploy-client` собирают через MCP в открытом редакторе; после 0.8.0 `SharedAddressablesSync`
  нет, старый путь не скомпилируется - сразу переходить на `deploy-client.sh`.
- **Telegram / itch-сборки**: профиль один, другие платформы игра не собирает - «сборки всех платформ» = одна WebGL-цель (уточнить, вопрос 9).

### Открытые вопросы для пользователя
1. Раскладка: `Main` + `Menu` + `GamePlay` (+ `Music`) + общие `SharedCommon` / `SharedMenu` / `SharedGamePlay` - подходит? `Main` и `SharedCommon`
   предзагружать в `.data` (`IsPreloaded`), как pixel-swarm?
2. Музыку (`Game_Music`) вынести в отдельный бандл `AddContentBundle("Music")`, который качается после старта?
3. Сессии и лобби бэкенда оставить своими (`Game.Global`, `Meta.Matches`) или перевести на `backend/Hosting` фреймворка (`AddSessionHosting`,
   `ILobbyFactory`, ...)? Перевод - отдельная крупная работа, в эту задачу не включён.
4. Скиллы-копии (`commit`, `workflow*`, `split-commit`, `start-cluster`, `pr`, ...) заменить ссылками на фреймворк или оставить копиями?
5. Канвасы фреймворка: `AddScreenCanvasOverlay()` (как сейчас) или `AddScreenCanvasCamera(...)` с камерой UI игры (iPadOS 18.2 Safari теряет
   overlay)?
6. `foundation_port`: T2, T3, T4, C5 закрыть этой задачей (`run-tests.sh`, `verify.sh` из `deploy-client`, форки фреймворка) или оставить?
7. `unused_with_parameter`: делать в этой ветке после шага 2 или оставить отдельной задачей?
8. 0.1.0 #15 (`Id { get; private set; }` с `[JsonProperty]` у 13 агрегатов `backend/Meta`): делать или пропустить?
9. «Все платформы» - только WebGL `web_dev`? Нужен ли новый профиль (например release `website`) для замера и деплоя?
10. Копии тестов фреймворка в `backend/Tools/Tests`, которые повторяют `tools/framework/backend/Tests`: чинить или удалить?

### Ответы пользователя (2026-11-17)
1. Раскладка `Main` + `Menu` + `GamePlay` + общие - да; `Main` и `SharedCommon` предзагружены в `.data` - да.
2. Музыка - отдельный бандл `Music` после старта - да.
3. Сессии и лобби бэкенда остаются своими, на `backend/Hosting` не переводим.
4. Скиллы-копии заменить ссылками на фреймворк - да (свои скиллы игры остаются рядом).
10. Копии тестов фреймворка в `backend/Tools/Tests` - удалить.

Без ответа (вопросы 5-9) - пока по предложениям плана.

### Ответы пользователя (2026-11-17, после сдачи)
1. `verify.sh` - исправить (фикс уже во фреймворке `a2fadb9b`). 2. Дефект раскладки - чинить во фреймворке. 3. `GamePlayViews` - да. 4. Механизм локальной сборки - пока нет. 5. Теги матчмейкинга - не надо. 6. Копии общих доков - удалять. 7. `OptionsContainer` Production - пока да.

#### [F2] Фреймворк: дефект раскладки (ленивая цель корня + сцены другого бандла)
- **Статус:** [x] done (ветка фреймворка `content-layout-lazy-scene` `1968be6e`, не влита)
#### [D1] Удалить копии общих доков из `.claude/docs`
- **Статус:** [x] done (`9e7c6595`)
#### [G1] Игра: указатель на `v0.9.1`, `deploy-client.sh web --no-deploy` PASS
- **Статус:** [x] done (`9a9b85e2`, `51df7760`; DEPLOY-CLIENT PASSED)
#### [G2] Игра: `v0.9.2`, снять ручные корни `Main`, пересинк, deploy-client PASS
- **Статус:** [x] done (`4258fb24`, `4d8494a8`; R2: релиз `v0.9.2` = `5c4da7db`)
#### [G3] Игра: `v0.9.3` (аудит бэкенда/клиента), шаги релиза, deploy-client PASS
- **Статус:** [x] done (`70df9d59`; DEPLOY-CLIENT PASSED; push `origin/main` = `70df9d59`)
