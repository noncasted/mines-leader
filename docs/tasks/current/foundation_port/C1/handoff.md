# C1 + C2 + C3: handoff исполнителя

Worktree `agent-a5f85a417b435cb73`, база `main` 7f2d9550. Правки не закоммичены. Редактор не трогал, клиент проверен только чтением (в worktree нет .csproj). R2 (Lifetime/Reactive, CardLocalIdle, CardLocalSpawn, CardStateLifetime, UpdatableActionBase) не трогал.

## Изменённые файлы

- `client/Assets/Common/Internal/Runtime/Scopes/Common/Events/EventLoop.cs` (C1, C2)
- `client/Assets/Common/Internal/Runtime/Scopes/Services/LoadedScope.cs` (C1)
- `client/Assets/Common/Internal/Runtime/Catalogues/Shared/AssetGroupBuilderExtensions.cs` (C1)
- `client/Tools~/ContainerGenerator/Scope/EdgeResolver.cs` (C1, только комментарий)
- `.claude/docs/COMMON_CONTAINER.md` (C1)
- `client/Assets/Common/Global/UI/StateMachines/UIStateMachine.cs` (C2)
- `client/Assets/GamePlay/Loop/GamePlayLoop.cs`, `client/Assets/Menu/Common/MenuLoop.cs` (C2, вызовы RunCustom)
- `client/Assets/Common/Internal/Runtime/Scopes/Services/SceneServices/SceneServicesFactory.cs` (C3)
- `client/Assets/Common/Internal/Runtime/Services/Updaters/Updater.cs` (C3)
- `client/Assets/Common/Global/Inputs/InputConstraintsStorage.cs` (C3)
- `client/Assets/Common/Global/Publisher/Itch/ItchSaves.cs` (C3)
- `client/Assets/Meta/Connection/BackendProjectionHub.cs` (C3)
- `client/Assets/Meta/MetaScopeExtensions.cs` (C3, регистрация `SharedBackendUser.Match`)
- `client/Assets/Common/Global/Settings/Settings.cs` (C3, удалён `Revert`)

## C1. Asset groups выгружаются после разборки lifetime

**Баг подтверждён.** `LoadedScope.Dispose` в B совпадал с A до фикса: `RunDispose` → `InvokeBeforeDispose` (`group.Release()`) → `Terminate` → выгрузка сцен → `Container.Dispose`. Слушатели lifetime скоупа видят уже освобождённые группы.

**Сделано** по A `aa1f44383`:
- `IEventLoop.AddBeforeDispose/InvokeBeforeDispose` переименованы в `AddAfterDispose/InvokeAfterDispose`, поле `_afterDisposeCallbacks`.
- `AssetGroupBuilderExtensions` (`RequestAssetGroup`, `LoadAssetGroup`) регистрируют `Release` на `AddAfterDispose`.
- `LoadedScope.Dispose`: `RunDispose` → `Terminate` → выгрузка сцен → `Container.Dispose` → `InvokeAfterDispose`.
- Комментарий в `EdgeResolver.cs` и строка в `COMMON_CONTAINER.md`.

**Отличия от A:** `EventLoop` в B без ленивых списков и статических лямбд A, перенесено только переименование фазы. Хунк `WeaponMuzzleFlash` из A игровой, не переносился. Генератор: правка только в комментарии. `.csproj` генератора в worktree нет, поэтому сборку и тесты генератора не запускал (код генератора не менялся).

**Ошибка "Trying to listen terminated lifetime" (CardLocalIdle.Enter ← CardLocalSpawn.Execute ← UpdatableActionBase.Dispose ← Lifetime.Terminate ← LoadedScope.Dispose) от C1 не зависит.** Она возникает внутри `_scopeLifetime.Terminate()`, а его место относительно `RunDispose` C1 не меняет. Механизм: lifetime действия (`RunUpdateAction`) завершается, `UpdatableActionBase.Dispose` вызывает `_completion.TrySetResult()`, и продолжение `CardLocalSpawn.Execute` выполняется синхронно в том же `Terminate`. Дальше `_idle.Enter()` подписывается на уже завершённый lifetime карты. Чинить надо в коде карт или в `RunUpdateAction` (не продолжать после завершения lifetime), это зона R2. Побочный эффект C1: во время `Terminate` группы ассетов ещё загружены. Поэтому такие запоздалые продолжения больше не получат "... is not loaded", остаётся только ошибка про lifetime.

## C2. UIStateMachine и EventLoop.RunCustom

**Баг подтверждён.** `UIStateMachine.CreateChild/CreateStackChild` делали `_handles[state] = childHandle` и никогда не удаляли запись. Словарь держит мёртвые хэндлы закрытых окон. `RunCustom/RunCustomAsync` принимали `lifetime`, но не передавали его слушателю: вызывающий код замыкал свой lifetime.

**Сделано** по A `bfdbf4ce2` (F2.29):
- `UIStateMachine.Add`: запись удаляется по окончании `InnerLifetime` хэндла, если в словаре всё ещё этот хэндл. Повторно открытое окно не теряет новый хэндл при смерти старого.
- `RunCustom<T>(lifetime, Action<T, IReadOnlyLifetime>)` и `RunCustomAsync<T>(lifetime, Func<T, IReadOnlyLifetime, UniTask>)`. Вызывающий код в `GamePlayLoop` (4 вызова) и `MenuLoop` (1) берёт lifetime из аргумента.

**Отличия от A:** в B нет `Internal/Runtime/Common/StateMachines/StateMachine.cs`, эта часть фикса неприменима. `EventLoop` в B без перегрузок `Invoke` со state, поэтому лямбда-обёртка `l => invoker.Invoke(l, lifetime)` вместо статической. Внешних вызовов `IUIStateMachine.Exit/ClearStack(state)` в B нет, поэтому удаление записи не ломает вызывающий код. `GameInputLockState`/`EnterWhile` из того же коммита A не переносились: в B нет потребителей (пауза, магазин, чат A).

## C3. Fail-fast в ядре, Global и Meta

**Баги подтверждены**, файлы совпадают с A до фикса:
- `SceneServicesFactory.Create`: не-`ISceneService` пропускался через `continue`.
- `Updater.SetSpeed`: отрицательная скорость давала `return`.
- `InputConstraintsStorage.Remove`: счётчик зажимался в 0.
- `ItchSaves`: битый сейв проглатывался (`try/catch` с `LogWarning`, `?? new T()`).
- `BackendProjectionHub`: неизвестная проекция писалась в `Debug.Log` и терялась.

**Сделано** по A `bfdbf4ce2` (F2.27, F2.28): все пять мест бросают исключение с понятным текстом. Тексты и комментарии взяты из A.

**Где B опирался на старое поведение (найдено чтением):**
1. **`SharedBackendUser.Match` не имел получателя на клиенте.** `Match.cs:157` вызывает `UserMatchHistory.Add`, а тот шлёт `SendProjection(MatchOverview)` → `SharedBackendUser.Match` обоим игрокам после **каждого** матча. Клиент молча терял его в `BackendProjectionHub`. С fail-fast клиент падал бы в конце каждого матча. Решение: `.RegisterBackendResponse<SharedBackendUser.Match>()` в `MetaScopeExtensions` (без ожидания начального значения, как `MatchResult`). В A история матчей удалена целиком, поэтому там вопроса нет. **Вопрос оркестратору/пользователю:** оставить регистрацию (история пригодится меню) или перестать слать проекцию с бэкенда. Остальные типы, которые шлёт бэкенд (`ProfileProjection`, `UserStatsProjection`, `InGameAchievementsProjection`, `RatingProjection`, `DeckProjection`, `CardsProjection`, `InitialCardPreviews`, три конфига, `SharedMatchmaking.MatchResult`), зарегистрированы.
2. **`InputConstraintsStorage` в B считал наоборот.** `UIConstraints.Game` = `{ Game: false }` (false = запретить), а старый код считал только `true`. Ограничения ввода фактически никогда не считались. Индексатор `this[]` в B никто не читает (его нет в интерфейсе). Перенёс инверсию A (`isAllowed == true` → пропуск), так что счётчик теперь реально растёт и падает. Баланс Add/Remove проверил чтением `UIStateHandle`: `ViewableProperty.Set` с тем же значением не стреляет, `Exit` сначала прячет, потом терминирует, так что повторного Remove нет. Но если баланс где-то нарушен, теперь будет исключение `[InputConstraints] Game is removed more times than added`. Проверить в Play.
3. **`ItchSaves`:** битый `save_v2` теперь роняет `OnBaseSetup` при каждом старте, пока игрок не очистит хранилище. Так в A и так требует правило проекта. Если у живых игроков бывают битые сейвы, это вопрос пользователю.
4. `Updater.SetSpeed` вызывается только с `0f` и `_setSpeed`, отрицательных значений в B нет. `UpdaterProxy.SetSpeed` (как и в A) не менялся.
5. `SceneServicesFactory`: упадёт на сцене с missing script или ручной заменой в `_services`. Проверить загрузкой всех сцен (меню, матч, mock-сцены).

**Настройки:** удалён только мёртвый `ISettings.Revert` (вызовов нет). `ShakeIntensity` и `VSync` в B **не удалял**: в отличие от A, к ним привязаны слайдер и тоггл в `Menu/Settings/MenuSettings.cs` и `GamePlay/UI/Pause/GamePauseSettings.cs` (и в префабах). При этом сами значения никто не применяет (`ISettings.ShakeIntensity/VSync` не читаются, `QualitySettings.vSyncCount` нигде не ставится). То есть в UI два контрола, которые ничего не делают. Удалить их вместе с UI или реализовать — решать пользователю. Хунк `SettingsPanel` (`WebPage.IsFullscreenSupported`) и `Crosshair`/`DamageNumbers` относятся к UI A и не переносились.

**Отличия от A:** не перенесены `StateMachine.cs` (нет в B), `GameInputLockState`, `UIStatesExtensions.EnterWhile`, правки `PauseMenu`/`ShopWindow`/`CheatPanel`/`GameChatInputBlock`/`PlayerInput`/`LevelPhases` (игровой код A), тест `StateMachineTests`, урок F34 в `CLAUDE_MISTAKES.md` (про CoreCLR-редактор и MCP; при желании оркестратор может добавить).

## Что проверить в редакторе

**Компиляция** (чтением проверено, но запуска не было):
- новые `using System;` в `SceneServicesFactory.cs`, `Updater.cs`; в `BackendProjectionHub.cs` убран `using UnityEngine;` (больше не нужен);
- `static (l, lt) => ...` в `GamePlayLoop`/`MenuLoop` — статические лямбды C# 9; в клиенте B это первые такие, если компилятор Unity против — убрать `static`;
- `IEventLoop` реализует только `EventLoop`; заглушка `EventLoop : IEventLoop {}` в `client/Tools~/ContainerGenerator.Verify/Program.cs` пустая, не затронута;
- после изменения регистрации в `MetaScopeExtensions` перегенерировать контейнер (ContainerGenerator), проверить отсутствие `CINGR00*`.

**EditMode-тесты:** полный прогон, особенно `Internal/Tests/Editor/Container/*` (ScopeCodegenEmitTests трогает `IEventLoop`).

**Живые сценарии (Play Mode, консоль без новых ошибок):**
1. Старт → меню (проверка `ItchSaves`, `SceneServicesFactory` на сценах меню, проекции meta).
2. Вход в матч с ботом, раздача карт, игра до конца матча — после конца не должно быть `[Projection] No projection for type` (проверка `SharedBackendUser.Match`).
3. Выход из матча в меню и повторный вход (C1: никаких "... is not loaded"; порядок разборки).
4. Открыть и закрыть окна меню много раз (Profile, Settings, Play, Decks, Unlocks, UnlockSelection) и пару раз стопкой (C2: `_handles` не растёт; C3: нет `[InputConstraints] ... removed more times than added`). Размер `_handles` можно глянуть в отладчике.
5. Остановка Play во время раздачи карт: ожидаемо остаётся известная "Trying to listen terminated lifetime" (зона R2, см. C1), новых ошибок быть не должно.
6. Пауза/продолжение в матче (`Updater.Pause/Continue`).

## C2, доработка: `_handles` растёт на 1 за цикл «меню → матч → меню»

**Причина (подтверждена чтением).** `UIStateMachine` глобальная, `Base` живёт с глобальным скоупом. `MenuNavigation.OnClicked` создаёт вкладку как `EnterChild(_stateMachine.Base, child)`. Lifetime хэндла — `Base.InnerLifetime.Child()`, то есть глобальный. При выгрузке скоупа меню `_current.Exit()` никто не вызывает, поэтому хэндл последней вкладки (обычно `MenuPlay`) остаётся в `_handles`. Ключ — объект состояния выгруженного скоупа, и в новом меню он не совпадает с новым, отсюда +1 за цикл. Вместе с хэндлом утекали объект вкладки, подписка на `Base.IsVisible` и запись в `InputConstraintsStorage` (счётчик `Game` +1 за цикл: хэндл видим и ни разу не снимался). Колебание 3↔4 внутри меню — нормальная смена вкладки: старый хэндл удаляется, новый добавляется. Та же проблема была у `MenuUnlockSelection.Process` → `ProcessChild(Base, this)`: открытое окно выбора награды переживало выгрузку меню.

**Исправление: владелец хэндла обязателен в API.**
- `IUIStateMachine.CreateChild/CreateStackChild(IReadOnlyLifetime lifetime, IUIState parent, IUIState state)`. Все расширения в `UIStatesExtensions` (`EnterChild`, `ProcessChild` ×3, `ProcessStack` ×3, `Process`) принимают `lifetime` первым аргументом после машины.
- `UIStateHandle`: inner lifetime по-прежнему `parent.InnerLifetime.Child()` и дополнительно завершается вместе с `lifetime` владельца. Когда хэндл кончается раньше владельца, подписка на владельце снимается (`RemoveListener`): частая смена вкладок не копит слушателей на lifetime скоупа. По завершении хэндла срабатывают уже существующие слушатели: `InputConstraintsStorage.Remove`, `Completion`, удаление из `_handles` (C2).
- `MenuNavigation`: вкладка привязана к lifetime скоупа меню (`OnSetup`).
- `MenuUnlockSelection.Process(IReadOnlyLifetime lifetime, IAchievementTier tier)`. `MenuUnlocks` передаёт lifetime скоупа меню из `OnSetup`, а не экрана: поведение окна (не закрывается при смене вкладки) сохранено.

**Другие места.** Других вызовов `EnterChild/CreateChild/ProcessChild/ProcessStack/Process` и `Base` в клиенте нет: в GamePlay (пауза, окна, чат) UI state machine не используется. Заглушка `IUIStateMachine` в `client/Tools~/ContainerGenerator.Verify/Program.cs` пустая и не затронута. Файлы R2 не трогал.

**Изменённые файлы доработки:**
- `client/Assets/Common/Global/UI/StateMachines/IUIStateMachine.cs`
- `client/Assets/Common/Global/UI/StateMachines/UIStateMachine.cs`
- `client/Assets/Common/Global/UI/StateMachines/UIStatesExtensions.cs`
- `client/Assets/Common/Global/UI/StateMachines/Handles/UIStateHandle.cs`
- `client/Assets/Menu/Navigation/MenuNavigation.cs`
- `client/Assets/Menu/Unlocks/MenuUnlockSelection.cs`
- `client/Assets/Menu/Unlocks/MenuUnlocks.cs`

**Проверить в редакторе:**
- компиляция; `IMenuUnlockSelection` и `IUIStateMachine` поменяли сигнатуры, других реализаций нет;
- «меню → матч → меню» несколько раз: `_handles` после возврата в меню равен значению до первого матча (2: `Base` + `MenuPlay`);
- открыть окно выбора награды (Unlocks → тир) и выйти в матч не закрывая, если это возможно: окно закрывается вместе с меню, без ошибок;
- нет `[InputConstraints] Game is removed more times than added` и `Trying to listen terminated lifetime` из `UIStateHandle`.
