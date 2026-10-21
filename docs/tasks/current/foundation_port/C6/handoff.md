# C6. Мёртвый код: handoff

Источник: A `429a82088` (F0.3, 123 файла) и частичный откат `8870e870d` (UI state machine и HTTP backend stack вернули). База: B `77bb0ce5`.

## Шаг 0

Каждого кандидата из `429a82088` проверил grep по `client/` B: `.cs` (типы, регистрации `As<>`, `[Inject]`), `.asmdef`, guid из `.meta` по сценам, префабам и ассетам, строковые имена (SendMessage, jslib), фикстуры `Tools~/ContainerGenerator.Verify`. В фикстурах `ItchLanguageProvider`, `CameraUtils`, `DelayRunner` и `IBackend*` объявлены inline в исходнике теста, поэтому от реальных типов они не зависят.

## Кандидаты

| Кандидат (A) | Итог в B | Причина |
|---|---|---|
| `Global/Constants/GameConstants.cs` | удалён | `PixelSize` нигде не используется |
| `Publisher/Abstract/Languages/ISystemLanguageProvider.cs`, `Language.cs` | удалён | `ISystemLanguageProvider` регистрировался, но его никто не резолвит. `Language` нужен только ему и `LanguageSave` |
| `Publisher/Itch/Languages/*` (`IItchLanguageAPI`, `ItchLanguageDebugAPI`, `ItchLanguageExternAPI`, `ItchLanguageProvider`) | удалён | нужны только для `ISystemLanguageProvider` |
| `Publisher/Itch/Common/ItchProvider.jslib` | удалён | в нём одна функция `GetLanguageItch`, её вызывал только `ItchLanguageExternAPI` |
| `Global/UI/LanguageSave.cs` | удалён | нигде не используется |
| `Publisher/Abstract/Callbacks/IJsErrorCallback.cs`, `Itch/Common/ItchCallbacks.cs`, `Assets/Prefabs/ItchIO_Callbacks.prefab` | удалён | на `IJsErrorCallback`/`Exception` никто не подписан, `OnException` ни из JS, ни через SendMessage не вызывается. На guid префаба ссылался только `GlobalPublisherOptions.asset` |
| `GlobalPublisherExtensions.AddItchIO` | изменён | убраны Instantiate колбэков и регистрации языка, осталась `ItchSaves` |
| `GlobalPublisherOptions` (.cs, .asset) | оставлен, поле удалено | в B опции живые: `OutdatedNoticeFont` использует `ClientOutdatedNotice`. Удалены только поле `_itchCallbacksPrefab` и его строка в .asset. `GlobalAssets.cs` и группа `Global.asset` не менялись |
| `IAudioVolume` (Mute/Unmute/SetVolume/Values/IsMuted) в `AudioPlayer` | удалён | не резолвится, методы не вызываются. Проверки `_isMuted` убраны, громкость применяется как раньше без mute |
| `IAudioListener` (Enable/Disable) в `AudioListener` | удалён | не резолвится. `OnBaseSetup` включает listener напрямую |
| ссылка `Menu.asmdef` -> `Art` (`33eac981...`) | удалена | в `Art.asmdef` нет ни одного .cs. Unity даже не генерирует для Menu ProjectReference на Art |
| `Global/Backend/*` (HTTP stack), `AddBackend`, `IMetaBackend.Client` | оставлен | в B его никто не вызывает (только регистрация и неиспользуемое свойство `MetaBackend.Client`). Но в A его вернули решением пользователя (`8870e870d`: "Kept for upcoming use"). Это намерение, а не случайность, поэтому удалять без пользователя не стал |
| `Global/UI/StateMachines/*`, `UIConstraints`, `DesignExtensions` WaitClick/ListenClick | оставлен | живой: Menu (`MenuNavigation`, `MenuPlay`, `MenuDecks`, `MenuProfile`, `MenuSettings`, `MenuUnlocks`, `MenuUnlockSelection`), починен в C2 |
| `Global/Inputs/InputConstraintsStorage.cs` | оставлен | живой: зависимость `UIStateMachine` и `UIStateHandle` |
| `Global/Cameras/CameraUtils.cs` | оставлен | живой: `GamePlay/Services/GameInput` (`ICameraUtils`) |
| `Internal/Runtime/Services/Updaters/Delays/*` (`DelayRunner`, `UpdateDelay`) | оставлен | живой: `GamePlay/Services/FloatingText/GameFloatingText` (`IDelayRunner`) |
| `Meta/Registries/IMetaRegistry.cs` + ветка реестров в `MetaLoop`/`MetaState`/`MetaScopeExtensions`, `StartupAssetsPreload` (комменты) | оставлен | живой: `CardsRegistry`, `GameModesRegistry`, `ModifiersRegistry`, `MetaLoop` собирает их |
| `Meta/Matchmaking/MatchMakingConfigs.cs` | оставлен | живой: `MenuPlay` (`_matchMakingConfigs.View` строит режимы) |
| `Profile`: история матчей, рейтинг, Wins/Loses, `Character`; `BackendEndpoints.GetMatchHistory/GetMatchDetails` | оставлен | живой: `MenuProfile`, `MenuProfileStats`, `MenuProfileHistory`, `MenuProfileMatch` |
| `Publisher/Telegram/TelegramLanguageProvider.cs` | в B отсутствует | — |
| `GamePlay/Players/.../PlayerInput.cs`, `PlayerInputSource.cs`, `Menu/Main/MenuState.cs` | в B отсутствует | игровой код A |
| Переименование `LoadPvp` -> `LoadMatch`, `LoadPvPMock` -> `LoadMatchMock` | не делалось | это не мёртвый код, а переименование. Вне объёма C6 |

## Изменённые файлы

Удалены (`git rm`, вместе с .meta файлов и папок):
- `client/Assets/Common/Global/Constants/` (папка);
- `client/Assets/Common/Global/Publisher/Abstract/Callbacks/` (папка);
- `client/Assets/Common/Global/Publisher/Abstract/Languages/` (папка);
- `client/Assets/Common/Global/Publisher/Itch/Common/` (папка: `ItchCallbacks.cs`, `ItchProvider.jslib`);
- `client/Assets/Common/Global/Publisher/Itch/Languages/` (папка);
- `client/Assets/Common/Global/UI/LanguageSave.cs`;
- `client/Assets/Common/Global/Assets/Prefabs/ItchIO_Callbacks.prefab`.

Изменены:
- `client/Assets/Common/Global/Publisher/GlobalPublisherExtensions.cs`;
- `client/Assets/Common/Global/Publisher/GlobalPublisherOptions.cs`;
- `client/Assets/Common/Global/Assets/Options/GlobalPublisherOptions.asset`;
- `client/Assets/Common/Global/Audio/AudioPlayer.cs`, `AudioListener.cs`, `GlobalAudioExtensions.cs`;
- `client/Assets/Menu/Menu.asmdef`.

Пересечений с `dirty_main.txt` и с зоной исполнителя генератора нет. Backend и shared не трогались.

## Проверка

- `dotnet build client.sln --no-incremental`: 0 ошибок, CINGR-диагностик нет. Для сборки во временные копии .csproj основного дерева внесены правки: из `Global.csproj` убраны 10 удалённых файлов. Ещё убраны 2 незакоммиченных файла основного дерева, которых нет в worktree (`UIClipboard.cs`, `WebGlDeploy.cs`). После сборки копии удалены.
- Редактор и Play Mode не запускались.

## Что проверить в редакторе

1. Компиляция без ошибок. Unity удалит библиотеку `ItchProvider.jslib`; других jslib с `GetLanguageItch` нет.
2. В инспекторе `GlobalPublisherOptions.asset` осталось одно поле `Outdated Notice Font`. Missing-ссылок нет.
3. Play Mode: старт, меню, звук (музыка, клики, слайдеры громкости в настройках), матч с ботом, выход. Публикатор ItchIO регистрирует теперь только `ItchSaves`, сейвы грузятся.
4. WebGL-сборка (при деплое): линковка без `ItchProvider.jslib` проходит. Никакой C# больше не делает DllImport `GetLanguageItch`.
5. Addressables: префаб `ItchIO_Callbacks` не был в группе каталога (на guid ссылался только options-ассет), поэтому группы не меняются.

## Решение пользователя

- HTTP backend stack (`Global/Backend`, `AddBackend`, `IMetaBackend.Client`) в B не используется. Удалить его или оставить "на будущее", как в A?
- Пустой `Art.asmdef` (в `Assets/Art` нет скриптов) в A-списке не было, поэтому оставлен.
