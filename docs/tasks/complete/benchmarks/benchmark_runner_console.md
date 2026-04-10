## Задача: BenchmarkRunner — управление запусками бенчмарков из консоли

### Цель
Создать серверный `BenchmarkRunner` сервис, который хранит состояние всех запущенных бенчмарков в памяти (группы, прогресс, `IOperationProgress`), чтобы UI при перезагрузке страницы видел текущие запуски. Добавить отмену бенчмарков (при отмене результат не сохраняется в БД). Переименовать папку `Console/Pages/Tests/` в `Console/Pages/Benchmark/` и компоненты соответственно.

### Шаги реализации

1. **Создать `BenchmarkRunner`** — `backend/Benchmarks/Common/BenchmarkRunner.cs` [новый файл]
   - Singleton-сервис, хранит `Dictionary<string, BenchmarkRunInfo>` (title -> run info)
   - `BenchmarkRunInfo`: `IClusterTest`, `IOperationProgress`, `CancellationTokenSource`, `DateTime StartedAt`
   - Методы: `Start(IClusterTest)`, `Cancel(string title)`, `GetRunning()`, `IsRunning(string title)`
   - При `Start` — создаёт `OperationProgress` + `CancellationTokenSource`, запускает `Test.Start()` в фоне, по завершении убирает из словаря
   - При `Cancel` — вызывает `CancellationTokenSource.Cancel()`, убирает из словаря, НЕ сохраняет в БД

2. **Добавить поддержку `CancellationToken` в `IClusterTest` и `BenchmarkRoot`** — `backend/Benchmarks/Common/IClusterTest.cs`, `backend/Benchmarks/Common/BenchmarkRoot.cs`
   - `IClusterTest.Start(IOperationProgress, CancellationToken)` — добавить параметр
   - В `BenchmarkRoot.Start()` — проверять `token.IsCancellationRequested` перед сохранением в БД
   - Прокинуть `CancellationToken` в `BenchmarkNodeHandle` и через `Run(handle, payload)`

3. **Зарегистрировать `BenchmarkRunner` как singleton** — `backend/Orchestration/Extensions/ProjectsSetupExtensions.cs`

4. **Переименовать папку и компоненты** — `backend/Console/Pages/Tests/` -> `backend/Console/Pages/Benchmark/`
   - `Tests.razor` → `Benchmarks.razor`
   - `TestEntry.razor` → `BenchmarkEntry.razor`
   - `BenchmarkHistory.razor` — остаётся
   - `BenchmarkProgressChart.razor` — остаётся

5. **Рефакторить `Benchmarks.razor` (бывший `Tests.razor`)** — использовать `BenchmarkRunner`
   - Инжектить `BenchmarkRunner` вместо прямого вызова `Test.Start()`
   - При инициализации — получать текущие запуски из `BenchmarkRunner.GetRunning()`
   - Отображать уже запущенные бенчмарки с текущим прогрессом

6. **Рефакторить `BenchmarkEntry.razor` (бывший `TestEntry.razor`)** — кнопка Cancel + подключение к `BenchmarkRunner`
   - Если бенчмарк запущен — показывать кнопку "Cancel" вместо "Run"
   - Cancel вызывает `BenchmarkRunner.Cancel(title)`
   - При инициализации — проверять `BenchmarkRunner.IsRunning(title)` и подключаться к существующему `IOperationProgress`

7. **Обновить `BenchmarkEndpoints`** — `backend/Orchestration/ConsoleGateway/BenchmarkEndpoints.cs`
   - Использовать `BenchmarkRunner` для запуска через API
   - Добавить `POST /{title}/cancel` endpoint

### Ключевые файлы

| Файл | Роль в задаче |
|------|---------------|
| `backend/Benchmarks/Common/BenchmarkRoot.cs` | Базовый класс запуска — добавить CancellationToken, условное сохранение |
| `backend/Benchmarks/Common/IClusterTest.cs` | Интерфейс — расширить Start() с CancellationToken |
| `backend/Benchmarks/Common/BenchmarkStorage.cs` | Хранилище результатов — вызывается условно при отмене |
| `backend/Benchmarks/Common/BenchmarkNodeHandle.cs` | Прокинуть CancellationToken для distributed nodes |
| `backend/Console/Pages/Tests/Tests.razor` | Главная страница → переименовать + подключить BenchmarkRunner |
| `backend/Console/Pages/Tests/TestEntry.razor` | Компонент записи → переименовать + Cancel кнопка |
| `backend/Orchestration/ConsoleGateway/BenchmarkEndpoints.cs` | REST API → использовать BenchmarkRunner |
| `backend/Orchestration/Extensions/ProjectsSetupExtensions.cs` | DI регистрация BenchmarkRunner |
| `backend/Common/Extensions/OperationProgress.cs` | IOperationProgress — добавить статус Cancelled |

### Документация к прочтению
- `rules/BLAZOR.md` — рефакторинг Blazor компонентов, UiComponent паттерн
- `rules/CODE_STYLE.md` — порядок членов, именование при создании нового класса

### Риски
- `BenchmarkRoot.Run()` — абстрактный метод, все реализации бенчмарков не принимают `CancellationToken` сейчас. Нужно решить: прокидывать токен до каждого бенчмарка или проверять только на уровне `BenchmarkRoot.Start()` (между `Run()` и `Storage.Write()`). Второй вариант проще — отмена просто не сохраняет результат, но бенчмарк доработает до конца.
- Переименование папки `Tests/` → `Benchmark/` — нужно проверить, что нет других ссылок на старые пути (проверено: `@using` в `_Imports.razor`, ссылки из `Home`)
- `BenchmarkRunner` должен быть потокобезопасным — бенчмарки запускаются параллельно
