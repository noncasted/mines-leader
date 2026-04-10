## Channel Sequence Catch-Up — Рабочие заметки

### Статус: Завершено

### Заметки

### [21:10] RuntimeChannel.cs — файл был обновлён
Файл содержал tracing (System.Diagnostics, activity) которого не было в первоначальном анализе. Адаптировал изменения.

### [21:12] Реализация ring buffer
- Sequence number инкрементируется синхронно ДО await в Publish() — безопасно для [AlwaysInterleave]
- Buffer инициализируется в OnActivateAsync из config
- CatchUp проверяет entry.Sequence == seq для защиты от stale entries после wrap-around

### [21:14] Observer catch-up в Resubscribe
- CatchUp вызывается только если LastSeenSequence > 0 (не первый resubscribe)
- Replay через await ObserverSource.Send(msg) — пропускает через тот же SequencedMessage unwrap
- CS4014 warning зафиксирован: добавлен await

### [21:15] Build
Infrastructure.csproj — Build succeeded, 0 errors, pre-existing warnings only.
