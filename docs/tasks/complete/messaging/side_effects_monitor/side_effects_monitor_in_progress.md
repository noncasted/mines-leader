## Side Effects Monitor — Рабочие заметки

### Статус: Завершено

### Заметки

### [20:58] Исследование SideEffectsStorage
- Три таблицы: `side_effects_queue`, `side_effects_processing`, `side_effects_retry_queue`
- Payload хранится как JSONB с `$type` для десериализации
- Console.csproj уже ссылается на Infrastructure.csproj — ISideEffectsStorage доступен напрямую через DI
- Razor SDK проект — новые .razor файлы автоматически компилируются, не нужен ручной .csproj entry

### [21:00] Реализация stats методов
- GetStats() — один SQL запрос с тремя subselects (COUNT)
- GetRetryEntries() — SELECT из retry_queue с парсингом $type из JSON payload
- ExtractTypeName() — простой string parsing для извлечения имени типа без полной десериализации
- DropRetryEntry() и RequeueRetryEntry() — стандартные DELETE/INSERT операции

### [21:02] Blazor страница
- Использовал ComponentBase (не UiComponent) — данные из SQL, не reactive подписки
- Timer auto-refresh каждые 5 секунд + IDisposable для cleanup
- Три карточки-счетчика + таблица retry entries + кнопки действий
- Паттерн _isOperating для блокировки кнопок во время операций

### [21:03] Сборка
- 0 новых ошибок. 4 pre-existing ошибки в BotManagement.razor и PlayersWidget.razor (не связаны)
