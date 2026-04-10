## Задача: API для запуска и анализа бенчмарков

### Цель
Добавить REST API эндпоинты в ConsoleGateway для запуска бенчмарков и получения истории результатов, а также создать скилл `/benchmark-analyse`, который через эти эндпоинты запускает бенчмарки и интерпретирует результаты.

### Шаги реализации

**Часть 1 — API эндпоинты:**

1. Создать `BenchmarkEndpoints.cs` в ConsoleGateway — `backend/Orchestration/ConsoleGateway/BenchmarkEndpoints.cs` [новый файл]
   - `GET /api/benchmarks` — список всех бенчмарков (Title, Group, MetricName, LastResult)
   - `GET /api/benchmarks/group/{group}` — список бенчмарков группы
   - `POST /api/benchmarks/{title}/run` — запустить конкретный бенчмарк, вернуть результат
   - `POST /api/benchmarks/group/{group}/run` — запустить всю группу, вернуть результаты
   - `GET /api/benchmarks/{title}/history` — история из `BenchmarkStorage.GetAll(title)`
   - `GET /api/benchmarks/group/{group}/history` — история всех бенчмарков группы

2. Зарегистрировать эндпоинты в `Program.cs` — `backend/Orchestration/ConsoleGateway/Program.cs`
   - Добавить `app.AddBenchmarkEndpoints()` перед `MapRazorComponents`

**Часть 2 — Формат ответа:**

3. Создать DTO-модели для API ответов в `BenchmarkEndpoints.cs` (вложенные record'ы):
   - `BenchmarkInfo` — Title, Group, MetricName, LastResult
   - `BenchmarkRunResult` — Title, Success, MetricValue, MetricName, DurationMs, ErrorMessage
   - `BenchmarkHistoryEntry` — Id, Date, Duration, Success, Records (count/time), MetricValue (вычисленный)
   - Формат: plain text или JSON — читаемый для CLI/скилла

**Часть 3 — Скилл:**

4. Создать скилл `/benchmark-analyse` — `.claude/skills/benchmark-analyse/SKILL.md` [новый файл]
   - Запускает Aspire проект через `dotnet run --project backend/Orchestration/Aspire/Aspire.csproj`
   - Вызывает API эндпоинты через `curl`/`WebFetch`
   - Интерпретирует: тренды, регрессии, аномалии, сравнение групп

### Ключевые файлы

| Файл | Роль в задаче |
|------|---------------|
| `backend/Orchestration/ConsoleGateway/Program.cs` | Регистрация API эндпоинтов |
| `backend/Orchestration/MetaGateway/IdentityEndpoints.cs` | Паттерн для minimal API endpoints |
| `backend/Benchmarks/Common/IClusterTest.cs` | Интерфейс бенчмарков (Group, Title, Start) |
| `backend/Benchmarks/Common/BenchmarkStorage.cs` | Чтение истории из БД |
| `backend/Benchmarks/Common/BenchmarkResult.cs` | Модель результата последнего запуска |
| `backend/Benchmarks/Common/BenchmarkMetricsHandle.cs` | BenchmarkState/BenchmarkRecord — модели в БД |
| `backend/Common/Extensions/OperationProgress.cs` | IOperationProgress для запуска тестов |
| `backend/Benchmarks/TestGroups.cs` | Константы групп (State, Messaging, Infrastructure) |

### Документация к прочтению
- `rules/CODE_STYLE.md` — стиль кода для новых файлов
- `rules/BLAZOR.md` — контекст ConsoleGateway (хотя API — не Blazor, но в том же проекте)

### Риски
- **Бенчмарки запускаются долго** (секунды-минуты) — API эндпоинт запуска должен быть синхронным (ждать завершения), т.к. скилл будет ждать результат
- **Aspire запуск** — проект запускается через `dotnet run --project backend/Orchestration/Aspire/Aspire.csproj`, но нужен PostgreSQL (Aspire создаёт контейнер автоматически). Скилл должен проверять, запущен ли уже кластер, перед запуском
- **Параллельный запуск бенчмарков** — `IClusterTest` — синглтоны, параллельный запуск одного теста невозможен. Группу можно запускать последовательно
- **ConsoleGateway.csproj** не ссылается напрямую на Benchmarks — но получает `IClusterTest` через DI (регистрация в `SetupConsole()`), `BenchmarkStorage` регистрируется в `AddBase()`. API эндпоинты смогут инжектить оба через `[FromServices]`
