# R2. Клиентские Lifetime/Reactive — handoff

Статус: review. Правки не закоммичены (worktree `agent-af4a1084fcab4e5e0`, база `main` 7f2d9550). Редактор не
использовался: проверка чтением кода и сборкой `dotnet build` по копиям `.csproj` из основного дерева (см. "Проверка").

## Шаг 0: подтверждение (чтением кода)

Bc (`client/Assets/Common/Internal/Runtime/Common/Reactive`) против A `0a67a0b08^`: отличаются `Lifetime.cs`,
`LifetimeExtensions.cs` (в B нет `LifetimeAnalytics`), `ViewableDictionary.cs` (рекурсия перебора),
`LifetimedValueExtensions.cs` (нет проверки `Pending`). Остальное совпадает. Все баги из списка R1 в Bc есть:

| Баг | Где в B до фикса |
|---|---|
| Lifetime не потокобезопасен | `Lifetime.Listen/Terminate/Token` без lock |
| `Listen(null)` молча игнорируется | `Lifetime.Listen`: `if (callback == null) return;` |
| утечка в `Intersect` | ранний `Terminate` пересечения не снимает подписки с источников |
| `TerminatedLifetime` | `Listen` ничего не делает (подписка живёт вечно), `Token` = `None` |
| нет bounding во View | `View`/`ViewNotNull`/`AdviseTrue(lt)`/list/dictionary `View` отдают lifetime значения как есть |
| Dictionary | `Remove` не убирает lifetime из карты (повторный `Add` ключа бросает), `Dispose` не завершает lifetime записей, перебор через `IEnumerable<KeyValuePair<TKey,TSource>>` рекурсивен при TSource == TView |
| `EventSource` | снимает первый равный делегат, а не свою подписку; `Advise(null)` принимается |
| `LifetimedValue(default)` | `Set(default)` пропускается как равное → `ValueLifetime == null` для `false`, `0` |
| `AddClampMax` | слушатели видят значение больше максимума |
| `WaitTrue/WaitFalse` | после завершения ожидания следующая смена значения снова зовёт `TrySetResult/TrySetCanceled` |
| `WaitInvoke` | при отмене подписка на источник не снимается |

## Что перенесено

Bc приведён к A HEAD (последнее состояние после `1f2a53f69`, `4eaf0cb1c`, `16297eeeb` и фиксов после форка) копией
файлов, кроме аналитики:

- `Lifetimes/Lifetime.cs` — потокобезопасный (lock на экземпляре), `Listen(null)` бросает, `TerminateCallback`
  (одна подписка на родителя), guard `_terminate != null && _parent != null` в `Terminate`. **Без** `LifetimeAnalytics`,
  `AnalyticsId`, `CreateIntersection` (Lifetime Graph не переносился).
- `Lifetimes/LifetimeExtensions.cs` — `WaitInvoke` снимает обе подписки, `Child` через `TerminateCallback`,
  `Intersect` без утечки; пересечение строится через `new Lifetime()` вместо `CreateIntersection`.
- `Lifetimes/TerminatedLifetime.cs` — `Listen` вызывает callback сразу (null бросает), `Token` отменён.
- `Events/EventSource.cs` — подписка-запись `Subscription`, удаление по ссылке, `Advise(null)` бросает.
- `Events/LifetimedValue.cs` — значение и lifetime задаются в конструкторе.
- `Events/LifetimedValueExtensions.cs` — bounding во `View`/`ViewNotNull`/`AdviseTrue(lt)`, проверка `Pending` в
  `WaitTrue/WaitFalse`.
- `DataTypes/Dictionaries/ViewableDictionary.cs` — `Remove` (неизвестный ключ бросает `KeyNotFoundException`),
  `Dispose`, перебор через `Source`.
- `DataTypes/Dictionaries/ViewableDictionaryExtensions.cs`, `DataTypes/Lists/ViewableListExtensions.cs` — bounding.
- `DataTypes/Properties/ViewablePropertyExtensions.cs` — `AddClampMax/IncreaseClampMax` без промежуточного значения.

Результат: все файлы Bc побайтно равны A HEAD, кроме `Lifetime.cs`/`LifetimeExtensions.cs` (только вырезанная
аналитика). `ViewableList.cs` в A для клиента не менялся; в B `Remove` уже убирает lifetime, `Clear` вызывает
`base.Clear()`.

Тесты (`client/Assets/Common/Internal/Tests/Editor`), равны A HEAD:
- `EventSourceTests.cs` (+3), `LifetimeTests.cs` (+7: `Listen_Null_Throws`, `Intersect_*`, `WaitInvoke_*`,
  `Terminate_RacingWithListenAndToken_LosesNothing`);
- новые `LifetimedValueTests.cs` (6), `LifetimeBoundsTests.cs` (18) — добавлены `git add -N`, `.meta` создаст Unity.
  Аналитику тесты не используют.

Документация агентов (`.claude/docs`): из патчей A перенесены строки в `COMMON_LIFETIMES.md` (правила Listen(null),
потокобезопасность, Intersect, WaitInvoke; раздел про завершённый lifetime переписан; добавлен раздел "Continuations
Run Inside Terminate"), `COMMON_REACTIVE_BASICS.md`, `COMMON_REACTIVE_VALUES.md`, `COMMON_REACTIVE_COLLECTIONS.md`.
Ссылки на номера A (F2.42) убраны.

## Что не перенесено

- `LifetimeAnalytics` / Lifetime Graph (`Lifetimes/Analytics`) — по задаче необязательно, тянет окно редактора.
- Правка A в `LevelFloor.cs` — игровой код A, в B нет.
- Правки бэкенда и тесты бэкенда — R1.

## Аудит (шаги 3-4)

### TerminatedLifetime (поведение `Listen` изменилось: callback сразу)

| Место | Вывод |
|---|---|
| `ViewableList.Add/Insert` после `Dispose` | Подписки на возвращённый lifetime снимаются сразу вместо вечной жизни. Правок нет |
| `GameObjectLifetime.GetValidLifetime` (объект неактивен и `Start` не вызван) | Раньше подписки на таком lifetime жили вечно, теперь снимаются сразу; `.Child()` от него сразу завершён, а следующий `Listen` на этом child даст "Trying to listen terminated lifetime". Вызовы `GetObjectLifetime()`: `DesignElement` (из `OnEnable`, объект активен), `CellView`, `CellEffects.AddEffect`, `GameRandom.Create/ResetViews`, `GameCamera.SetSize`, `ListenClick(button, listener)` без lifetime (вызовов нет). Все вызываются на активных объектах — правок нет, но в Play Mode смотреть на ошибки от `GameRandom`/`CellEffects` |

### View-bounding (lifetime значения теперь ∩ lifetime подписчика)

Двухаргументные `View/ViewNotNull/AdviseTrue(lt)` в `client/Assets` (остальные 40 вызовов — с одноаргументным
обработчиком, не затронуты):

| Место | Вывод |
|---|---|
| `GamePlay/Loop/Context/MatchEventLoop.cs:59,77` (`stateLifetime`, `playerLifetime`) | Используются внутри подписки, дольше не нужны. Правок нет (файл в зоне C1, не трогал) |
| `GamePlay/Players/UI/Buffs/PlayerBuffsList.cs:44` | `itemLifetime.Listen(RemoveView)` теперь срабатывает и при конце `Bind`-lifetime: иконки баффов удаляются при разборке, раньше висели до удаления записи. Желаемо. Смотреть, что `HideInfo` при разборке не трогает уничтоженный объект |
| `Menu/Unlocks/MenuUnlocks.cs:47` | Ручное пересечение `rowLifetime.Child()` + `screenLifetime.Listen` теперь избыточно, но корректно. Правок нет |

Равный `Set` и так не уведомлял на клиенте — поведение не менялось. `ViewableDictionary.Remove` в клиентском коде не
вызывается (единственный словарь — `PlayerModifiers`, только индексатор).

## Ошибка "Trying to listen terminated lifetime" при остановке Play во время раздачи

**Причина.** `UpdatableActionBase` завершает `_completion` **успехом** (`TrySetResult`) и когда кончился предикат, и
когда завершился lifetime (`_lifetime.Listen(Dispose)`). `UniTaskCompletionSource` вызывает продолжение синхронно,
то есть прямо внутри `Lifetime.Terminate`. При `LoadedScope.Dispose` → `_scopeLifetime.Terminate()` lifetime карты уже
помечен завершённым, а `CardLocalSpawn.Execute` продолжается после `await RunUpdateAction(...)` и вызывает
`_idle.Enter()` → `OccupyLifetime()` → `_lifetime.Child()` на завершённом lifetime карты (ошибка 1), затем
`RunUpdateAction(lifetime)` → `lifetime.Child()` (ошибка 2), и т.д. То же в `CardRemoteSpawn` (это ×4 из журнала
оркестратора) и в `CardLocalDrag` (возврат карты в руку после неудачного использования).

**Почему не в LoadedScope.** Порядок разборки тут ни при чём: при любом порядке завершение state-lifetime синхронно
продолжает анимацию, и она переводит карту в новое состояние на завершённом родителе. Правка C1 (asset groups после
`Terminate`) эту ошибку не уберёт и не усилит. В зону C1 ничего передавать не нужно.

**Почему не в UpdatableActionBase.** Правильнее было бы отменять (`TrySetCanceled`) при конце lifetime, но существующий
код на это опирается как на нормальное завершение: `CardDropArea.Show` ждёт `RunUpdateAction(stateLifetime, ...)`, и
`CardLocalDrag` завершает `useLifetime` при конце хода, ожидая, что `TryUse` вернёт результат и карта вернётся в руку.
Смена семантики сломает этот сценарий — это не "небольшое" исправление.

**Исправление (в коде карт).** После `await` анимации, которая переводит карту в следующее состояние, проверяется
lifetime своего состояния: если он завершён (состояние перехватили или карта разбирается), переход не делается. Это
явная ветка управления, не заглушка ошибки (тот же приём уже есть в `CardLocalStash.Enter`):
- `GamePlay/Cards/Entities/States/Local/Spawn/CardLocalSpawn.cs` — `SetSpawning(false)` как раньше, затем
  `if (lifetime.IsTerminated == true) return;` перед `_idle.Enter()`;
- `GamePlay/Cards/Entities/States/Remote/Spawn/CardRemoteSpawn.cs` — то же перед `_idle.Enter()`;
- `GamePlay/Cards/Entities/States/Local/Drag/CardLocalDrag.cs` — проверка после `useLifetime.Terminate()` (иначе
  `RunUpdateAction` на завершённом lifetime) и после анимации возврата перед `idle.Enter()`.

Побочно это чинит скрытый баг: если состояние спавна/драга перехватывалось другим (`OccupyLifetime`), старая анимация
по завершении переводила карту в Idle поверх нового состояния.

Ошибка в `Lifetime.Listen` на завершённом lifetime оставлена как есть (fail loud), как в A.

## Проверка

- Редактор не использовался. `.csproj` в worktree нет, поэтому скопировал `*.csproj`/`client.sln` из основного дерева
  во временную папку worktree (пути `Packages/` перенаправлены в основное дерево), собрал и удалил копии и `obj/Temp`.
- `dotnet build`: `Internal.Tests` (вместе с `Internal`, новые тесты добавлены в копию csproj), `GamePlay`,
  `GamePlay.Editor`, `Menu`, `Meta`, `Flow`, `Global.Editor` — 0 ошибок. `Internal.Editor` не собрался из-за файла
  `WebGlDeploy.cs`, которого нет в worktree (незакоммиченный файл основного дерева) — не связано с R2; его
  использования Lifetime (`new Lifetime()`) API не меняют.
- Бэкенд и shared не менялись.

## Что проверить в редакторе

1. Компиляция без ошибок (Unity сгенерирует `.meta` для `LifetimedValueTests.cs`, `LifetimeBoundsTests.cs`).
2. EditMode, сборка `Internal.Tests`: классы `LifetimeTests`, `LifetimeBoundsTests`, `LifetimedValueTests`,
   `EventSourceTests`, затем весь `Internal.Tests`. `Terminate_RacingWithListenAndToken_LosesNothing` многопоточный
   (50 раундов), занимает заметное время.
3. Play Mode:
   - меню → матч с ботом → **остановка Play во время раздачи карт**: ошибок "Trying to listen terminated lifetime" от
     `CardLocalIdle`/`CardRemoteIdle` нет;
   - матч: раздача, наведение и драг карты, использование, отмена драга (карта возвращается в руку), конец хода во
     время драга (карта возвращается), баффы на панели игрока появляются и исчезают;
   - выход из матча и повторный вход: нет новых ошибок, иконки баффов не остаются;
   - меню: колоды (драг карты в колоду), разблокировки, профиль.
   - Ошибки от `GameRandom`/`CellEffects` "Trying to listen terminated lifetime" означали бы неактивный объект при
     `GetObjectLifetime()` (см. аудит) — раньше такие подписки молча жили вечно.
