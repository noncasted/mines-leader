## Messaging reliability fixes — Рабочие заметки

### Статус: В работе

### Заметки

### 04:49 Инициализация workflow
Создан план исправления по результатам read-only аудита `backend/Infrastructure/Messaging`. Кодовые файлы не менялись; создан только workspace задачи в `docs/tasks/current/messaging_reliability_fixes/`.

### 04:57 Старт реализации
Начинаю с чтения релевантных правил и текущей реализации queue/channel/pipe/side-effects, затем добавлю/обновлю targeted tests и внесу минимальные исправления без большого rewrite.

### 05:02 Находки по текущей реализации
Подтвердились ключевые проблемы из аудита: `DurableQueue.Push` не бросает ошибку при `0` успешных доставках после падения observers; `DurableQueueClient`/`RuntimeChannelClient` удаляют только callback из `EventSource`, но transport observer остается в grain; `Listener.Resubscribe()` сам ловит exceptions, поэтому внешний `AdaptiveInterval.RecordFailure()` не вызывается; `RuntimePipe` не unbind-ит observer при termination; `SideEffectsStorage.Write(ISideEffect)` логирует storage failure, но возвращает успешный `Task`.

### 05:08 Основные исправления в Messaging
Перевел queue/channel clients на явные local handler tables с reference counting: последний terminated lifetime удаляет local handler, Orleans object reference и observer в grain. `Resubscribe()` теперь пробрасывает ошибки наружу, а `RecordFailure()` вызывается в общем retry loop. `DurableQueue.Push` считает successful/failed deliveries и бросает ошибку при `0` successful deliveries. `RuntimePipe` получил observer id, `UnbindObserver`, liveness `Ping()` и cleanup на lifetime termination. `SideEffectsStorage.Write(ISideEffect)` теперь rethrow-ит storage failure после rollback.

### 05:11 Targeted tests
Добавлены/обновлены tests для all-subscribers-fail в `DurableQueue`, no-ack после terminated listener, async listener awaiting для durable queue/channel, `CatchUp(lastSeen > currentSequence)` gap, `RuntimePipe.Exists` после termination и no-retry для application handler failure.

### 05:30 Review fixes and verification
Первичный `code-reviewer` нашел blocker/major issues: orphan concurrent subscribers on initial subscribe failure, `Min()` race on empty resubscribe dictionaries, stale `LastSeenSequence` after channel reset, potential catch-up/live deadlock after removing `AlwaysInterleave`, and rollback masking original `SideEffectsStorage.Write` failure. Исправлено: cleanup now removes shared listener only when last handler is gone; resubscribe loops use a single snapshot; channel observer buffers live deliveries during catch-up instead of blocking grain `Publish`; reset gap sets `LastSeenSequence` back to `0`; rollback failure logs warning and rethrows original write exception. Повторная проверка `code-reviewer` marked all five points as pass, with only non-blocking caveats.

### 05:31 Knowledge base update
Добавил lesson в `docs/db/docs/CLAUDE_MISTAKES.md`: нельзя держать local delivery lock через Orleans `AddObserver`/`CatchUp`, если `Publish` может синхронно вызвать `observer.Send`; вместо этого live delivery нужно буферизовать и flush-ить после catch-up.
