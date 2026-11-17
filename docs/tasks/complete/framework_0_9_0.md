## Перевод mines-leader на фреймворк 0.9.x

Завершено 2026-11-17. Коммиты задачи `e895fe1d..70df9d59` (31 коммит) в `origin/main`, фреймворк `v0.9.3` (`e38f175f`).
Полные рабочие файлы задачи (info, progress, result, журнал оркестрации) - в истории git: `git show b53b87cf:docs/tasks/current/framework_0_9_0/`.

### Что сделано
- Указатель `tools/framework` с `a11e507a` (раньше базы `v0.1.0`) доведён до `v0.9.3`: шаги релизов 0.1.0-0.9.3 выполнены
  или пропущены с причиной. Лобби и хостинг фреймворка не подключались (решение пользователя).
- Бэкенд: csproj и ~93 `using` на модули фреймворка, сессии, гейтвеи, консоль, бенчмарки; `shared/Protocol` (`NetworkContextUnion`,
  `ProtocolHash`); 0.9.3 - `SideEffectTables` в `DeploySetup`, `SharedViewableProperty`, `UniTask Process()`. Копии тестов
  фреймворка удалены (340 атрибутов, решение пользователя).
- Контент на бандлах движка: 7 `ContentBundle` (`Main`, `SharedCommon` - в `.data`; `Menu`, `GamePlayViews`, `SharedMenu`,
  `GamePlay` - gzip; `Music` - отдельная аудиогруппа), общие бандлы синхронизирует `GameSharedBundles`. Addressables удалены целиком
  (пакет, `AddressableAssetsData`, `WebGlDeploy.cs`, `link.xml`).
- WebGL: шаблон `index.html`, `client/deploy/nginx.conf` и `tools/scripts/deploy-webgl.sh` отдают `StreamingAssets/Content`;
  `.claude/deploy-client.json` для скилла deploy-client (PASS на 0.9.3).
- Копии общих доков и скиллов фреймворка в `.claude/` заменены ссылками, мёртвые доки (`CODE_EXAMPLES`, `UI_MENU`) удалены.
- Проверки: `backend.slnx` и `client.sln` 0 ошибок, WebGL `Result: Success`; `run-tests.sh` - backend 1535/1535, generator 180,
  Unity 601 + 47 штатных skip. Поведение (меню, бот-матч, пауза, выход/повтор, Win/Lose, Rematch, окна, музыка, переподключение,
  код 1000) - OK в редакторе и на собранной странице через nginx игры.
- Размер gzip / прод, МБ: весь билд 6,997 -> 6,355 / 5,800 -> 5,072; до меню 6,545 -> 5,966 / 5,285 -> 4,683; запросов до меню
  22-23 -> 4-5 (`docs/web_build_size/2026-11-17/report.md`).
- Во фреймворке по ходу задачи: фикс `verify.sh` (0.9.1), фикс `ContentBundleLayout` + тесты + релиз `v0.9.2`.

### Ключевые файлы
- `client/Assets/Common/Internal/Editor/Content/GameSharedBundles.cs`, `Editor/Content/*.asset` - раскладка бандлов
- `client/Assets/Common/Internal/Runtime/InternalScopeExtensions.cs`, `Common/Flow/Startup/GameStartup.cs`,
  `Internal/Runtime/Catalogues/Shared/StartupAssetsPreload.cs` - `AddContent`, загрузка
- `client/ProjectSettings/Framework.json` - раскладка инструментов, стрип (`Internal.EncodedAudio`)
- `client/deploy/nginx.conf`, `client/Assets/WebGLTemplates/Default/index.html`, `tools/scripts/deploy-webgl.sh`
- `.claude/deploy-client.json`, `tools/scripts/run-tests.sh` (SkipAnalyzersCopy, watchdog SBP, `UNITY_LOCK`)
- `docs/web_build_size/2026-11-17/report.md` - замер до/после

### Заметки
- Regenerate All при сломанной компиляции удаляет ассеты групп - сначала довести код до компиляции (CLAUDE_MISTAKES, Lesson 21).
- Теги `Session*`/`Lobb*` фреймворка в `NetworkContextUnion` не добавлены: конфликт с `SharedMatchmaking` (150-156).
- `Menu_Play`, `Menu_Navigation` были ручными корнями `Main` (обход дефекта раскладки), сняты после 0.9.2.
- Окружение `OptionsContainer.asset` для локальных проверок не коммитится; закоммичено Production.

### Открытые хвосты
- Клиент WebGL на 0.9.3 не выложен на Coolify - деплоем занимается другая сессия (бэкенд передеплоен push-ем с новым протоколом).
- Не проверены: `GameRandom`, `FlagAnimator` (MissingReference при выходе во время анимации флага, было до задачи),
  Home/`OnlineWidget` консоли (нет локальных учётных данных).
- Было до задачи: NRE `LastManStandingTurnBasedRound.SkipTurn()` :224 при отключении до первого раунда.
- Фреймворк: `HierarchyBindingsValidator` в Play пишет ложное «structure is stale» для `Menu.unity` (Canvas/Play, Canvas/Navigation) -
  отложенная валидация после рантайм-перестройки меню.
