# Benchmarks - Отчёт о выполненных задачах

## 1. Benchmark Framework
**Цель:** Превратить тестовый фреймворк в полноценную систему бенчмарков с метриками и историей.

**Что сделано:**
- `BenchmarkResult` state class для хранения метрик (MessagesPerSecond, OperationsPerSecond)
- `BenchmarkStorage` сервис для PostgreSQL persistence
- Расширение `IClusterTest` для сбора метрик
- UI переименован Tests -> Benchmarks, добавлен `BenchmarkHistory` компонент

**Файлы (подтверждено в diff):** `IClusterTest.cs`, `BenchmarkRoot.cs`, `BenchmarkStorage.cs`, `Benchmarks.razor`

## 2. REST API & Analysis
**Цель:** REST API для запуска бенчмарков и получения истории.

**Что сделано:**
- 6 эндпоинтов в `BenchmarkEndpoints`: list all, list by group, run single, run group, history, group history
- DTO: BenchmarkInfo, BenchmarkRunResult, BenchmarkHistoryEntry

**Файлы (подтверждено):** `BenchmarkEndpoints.cs` (87 строк изменений), `ConsoleGateway/Program.cs`

## 3. BenchmarkRunner Service
**Цель:** Server-side сервис для управления запущенными бенчмарками + отмена.

**Что сделано:**
- `BenchmarkRunner` singleton с in-memory state всех запущенных тестов
- CancellationToken поддержка в `IClusterTest.Start()` и `BenchmarkRoot`
- Cancel endpoint + кнопка в UI
- Папка переименована Tests/ -> Benchmark/

**Файлы (подтверждено):** `BenchmarkRunner.cs`, `BenchmarkRoot.cs`, `Benchmarks.razor`

## 4. Progress Snapshots
**Цель:** Сбор метрик каждые N% прогресса, отображение chart по последним 10 запускам.

**Что сделано:**
- `BenchmarkSnapshot` модель, таблица `benchmark_snapshots`
- Автоматический snapshot collection в `RunConcurrentIterations`
- `BenchmarkProgressChart.razor` - multi-line CSS chart

**Файлы (подтверждено):** `BenchmarkStorage.cs`, `BenchmarkMetricsHandle.cs`

## 5. Regression Detection
**Цель:** Автоматическое обнаружение регрессий производительности.

**Что сделано:**
- Baseline pinning: `IsBaseline` поле, `SetBaseline` API
- `BenchmarkComparison.cs`: MetricDirection enum, Compare() с порогом 10%
- Автоматическое сравнение в `BenchmarkRoot` с warning log при регрессии
- API: `POST set-baseline`, `GET compare`
- `CalculateMetricValue()` - унификация расчёта метрик (устранено 5x дублирование)

**Файлы (подтверждено):** `BenchmarkMetricsHandle.cs`, `BenchmarkOptions.cs`, `BenchmarkStorage.cs`, `BenchmarkRoot.cs`, `BenchmarkEndpoints.cs`

## Также в diff

- Удалены 4 устаревших стресс-теста: `MessagePipeSendResponseStressTest`, `MessagePipeSendStressTest`, `MessagingDirectQueueStressTest`, `MessagingTransactionalQueueStressTest`
- Обновлены оставшиеся тесты под новый API
