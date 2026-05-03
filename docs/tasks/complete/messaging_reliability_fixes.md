## Исправление надежности Messaging

### Что сделано
- Исправлено подтверждение доставки в `DurableQueue`: при нуле успешных доставок observer-ам сообщение не считается обработанным и остается в retry pipeline.
- Переведены `DurableQueueClient` и `RuntimeChannelClient` на явные local handler tables с reference counting: `Terminate()` чистит transport observer и Orleans reference только при удалении последнего handler.
- Добавлены async overloads `ListenDurableQueue`/`ListenChannel` с `Func<T, Task>`; delivery loop корректно await-ит асинхронные обработчики.
- `RuntimeChannel` получил gap detection при `lastSeenSequence > currentSequence`, FIFO-by-grain-turn ordering (убран `[AlwaysInterleave]`), live-delivery buffering во время catch-up и фильтрацию дубликатов по sequence.
- `RuntimePipe` получил observer identity, `UnbindObserver`, `Ping()`, cleanup при termination и разделение application vs transport failures для retry policy.
- `SideEffectsStorage.Write` теперь пробрасывает storage failure caller-у; rollback failure логируется отдельно.
- Добавлен targeted regression tests: all-subscribers-fail, terminated listener no-ack, catch-up gap, pipe cleanup, retry boundary.

### Ключевые файлы
| Файл | Роль |
|------|------|
| `backend/Infrastructure/Messaging/Queues/DurableQueue.cs` | Delivery acknowledgement, observer cleanup |
| `backend/Infrastructure/Messaging/Queues/DurableQueueClient.cs` | Local handler ref-counting, async delivery |
| `backend/Infrastructure/Messaging/Channels/RuntimeChannel.cs` | Ordering, gap detection, live buffering |
| `backend/Infrastructure/Messaging/Channels/RuntimeChannelClient.cs` | Catch-up/live flow, reset handling |
| `backend/Infrastructure/Messaging/Pipes/MessagePipeClient.cs` | Retry classification, observer cleanup |
| `backend/Infrastructure/Orleans/SideEffects/SideEffectsStorage.cs` | Failure surfacing |
| `backend/Tools/Tests/Messaging/*.cs` | Regression tests |

### Заметки
- Live-delivery buffering выбран вместо channel epoch/generation, чтобы избежать protocol rewrite.
- Application handler failures в `RuntimePipe` помечаются через wrapped exception message, не новый тип — чтобы не тянуть Orleans serialization.
- Урок добавлен в `CLAUDE_MISTAKES.md`: нельзя держать local delivery lock через Orleans observer binding/catch-up — deadlock.
