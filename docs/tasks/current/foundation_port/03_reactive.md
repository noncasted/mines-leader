# 03. Lifetime / Reactive (R1-R2)

A = `/projects/peace-data-reborn`, B = этот репозиторий.

Сокращения:
- **Bb** = `backend/Common/Reactive`. Побайтно совпадает с A `0a67a0b08^`.
- **Bc** = `client/Assets/Common/Internal/Runtime/Common/Reactive`. Почти совпадает с A `0a67a0b08^`. Отличия: в A были
  исправления после форка, которых нет в B (`LifetimeAnalytics`, рекурсия `ViewableDictionary`, проверка `Pending` в
  `WaitTrue`/`WaitFalse`).

Источники в A:
- `1f2a53f69` (F2.22): thread-safe Lifetime, Intersect, WaitInvoke, ClampMax, EventSource.
- `4eaf0cb1c` (F2.42): TerminatedLifetime, bounding во View, Remove/Clear коллекций.
- `16297eeeb` (F2.43): Dispose словаря, равный Set, EventSource по entry.
- `3bdde2d39`: тесты.

Описания багов: `/projects/peace-data-reborn/docs/roadmap/foundation/02_bugs.md`, разделы F2.22, F2.42, F2.43.

## R1. Backend Lifetime/Reactive

- **Приоритет:** P1. **Волна:** 2, первой в волне.
- **Шаги:**
  1. Bb совпадает с A на старте этапа, поэтому накладываются патчи `1f2a53f69`, `4eaf0cb1c`, `16297eeeb` по
     `backend/Common/Reactive` (`git diff ... | git apply`).
  2. Перенести тесты `backend/Tools/Tests/Reactive/LifetimeTests.cs`, `LifetimeBoundsTests.cs`,
     `ReactiveCollectionsTests.cs`.
  3. **Аудит View-bounding.** После `4eaf0cb1c` lifetime значения в `View` пересекается с lifetime подписчика. В A
     пришлось править вызывающий код (`backend/Game/Session/Objects/Services/LobbyService.cs`,
     `MatchListingService.cs`). В B пройти все `View(` на backend-коллекциях и `LifetimedValue`, особенно в
     `backend/Game` (сессии, объекты сессии) и `backend/Meta`. Найти места, где lifetime значения нужен дольше
     подписки.
- **Список фиксов:**
  - потокобезопасный `Lifetime`;
  - `Listen(null)` падает сразу;
  - утечка в `Intersect`;
  - `TerminatedLifetime` вызывает callback сразу и отдает отмененный Token;
  - bounding во View;
  - Dictionary: `Remove` и `Dispose` завершают lifetime записей, рекурсия перебора при TSource == TView;
  - List: `Remove` убирает lifetime, `Clear` вызывает `base.Clear()`;
  - `Set` не уведомляет при равном значении;
  - `EventSource` удаляет свою запись, а не первый равный делегат.
- **Готово:** тесты зеленые, полный `dotnet test` зеленый, места аудита записаны в `R1/handoff.md`.

## R2. Клиентские Lifetime/Reactive

- **Приоритет:** P1. **Волна:** 4. Нужен редактор B: компиляция и EditMode-тесты `Internal.Tests`.
- **Шаги:**
  1. Патчи `1f2a53f69`, `4eaf0cb1c`, `16297eeeb` по Bc. Хунки с `LifetimeAnalytics` / `CreateIntersection`
     накладываются только вместе с аналитикой. Аналитику (Lifetime Graph) переносить не обязательно; без нее Intersect
     строится через `new Lifetime()`. Обязателен guard в `Lifetime.Terminate`:
     `_terminate != null && _parent != null`.
  2. Исправления из A после форка, которых нет в B:
     - Bc `DataTypes/Dictionaries/ViewableDictionary.cs`: перебор через `IEnumerable<KeyValuePair<TKey, TSource>>`
       уходит в рекурсию до stack overflow при TSource == TView. В A это `private Dictionary<TKey, TSource> Source => this;`.
     - Bc `Events/LifetimedValueExtensions.cs`: в `WaitTrue`/`WaitFalse` нет
       `if (completion.Task.Status != UniTaskStatus.Pending) return;`. После завершения ожидания подписка живет
       дальше, следующая смена значения бросает исключение.
  3. Проверить вызовы `TerminatedLifetime` (`grep -rn TerminatedLifetime client/Assets`): поведение `Listen`
     меняется, callback теперь вызывается сразу.
  4. **Аудит View-bounding** в `client/Assets`, как в R1. В A правился `LevelFloor`.
  5. Тесты в `client/Assets/Common/Internal/Tests/Editor`: дописать кейсы в `EventSourceTests.cs`, `LifetimeTests.cs`;
     скопировать `LifetimedValueTests.cs`, `LifetimeBoundsTests.cs`. `.meta` не копировать, пусть Unity создаст свои.
- **Готово:**
  - компиляция без ошибок;
  - EditMode-тесты зеленые;
  - Play Mode без новых ошибок: меню, матч с ботом, выход;
  - места аудита записаны в `R2/handoff.md`.
