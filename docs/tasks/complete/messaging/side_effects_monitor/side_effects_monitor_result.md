## Side Effects Monitor — Результат

### Статус: Завершено

### Что сделано
1. Добавлены DTOs: `SideEffectsStats` и `RetryQueueEntry` в SideEffectsStorage.cs
2. Расширен интерфейс `ISideEffectsStorage` четырьмя новыми методами: GetStats, GetRetryEntries, DropRetryEntry, RequeueRetryEntry
3. Реализованы все методы в `SideEffectsStorage` с SQL запросами
4. Создана Blazor страница `SideEffectsMonitor.razor` с route `/side-effects`
5. Добавлен роут в `ConsoleConstants.Pages`
6. Добавлена карточка на Home в `HomeInfrastructureSection.razor`

### Измененные файлы

| Файл | Что изменено |
|------|-------------|
| `backend/Infrastructure/Orleans/SideEffects/SideEffectsStorage.cs` | DTOs + 4 новых метода в интерфейсе и реализации |
| `backend/Console/Pages/SideEffects/SideEffectsMonitor.razor` | Новая страница мониторинга [новый файл] |
| `backend/Console/Common/ConsoleConstants.cs` | Добавлен роут SideEffects |
| `backend/Console/Pages/Home/HomeInfrastructureSection.razor` | Карточка навигации |
