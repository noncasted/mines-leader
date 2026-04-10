## Задача: Benchmark Regression Detection

### Цель
Добавить автоматическое обнаружение регрессий производительности при запуске бенчмарков. Сейчас фреймворк полностью рабочий (37 бенчмарков, 5 групп, автосохранение в PostgreSQL, UI с историей), но сравнение результатов только визуальное. Нет механизма baseline pinning, нет автоматического alerting при деградации, нет side-by-side сравнения runs. Эти фичи уже перечислены в `feature.md` как Ideas.

### Контекст
- Результаты хранятся в `state_benchmark` (PostgreSQL через Orleans State system)
- `BenchmarkStorage.GetAll(name)` возвращает всю историю, отсортированную по дате DESC
- `BenchmarkHistoryEntryDto` содержит `MetricValue` (ops/s или ms) и `Samples` (временная серия)
- API endpoints: `/api/benchmarks/{title}/history`, `/api/benchmarks/{title}/run`
- `BenchmarkOptions`: MetricStep=50, Samples=50, CollectStep=0.02s
- MetricValue вычисляется как `totalCount / duration.TotalSeconds`

### Шаги реализации

**1. Baseline pinning — возможность отметить run как baseline**
  1.1. Добавить поле `IsBaseline: bool` в `BenchmarkState` с `[Id(N)]` — `backend/Tools/Benchmarks/Common/BenchmarkResult.cs`
  1.2. Добавить API endpoint `POST /api/benchmarks/{id}/set-baseline` — `backend/Orchestration/ConsoleGateway/BenchmarkEndpoints.cs`
  1.3. В `BenchmarkStorage` — метод `GetBaseline(name)` для получения baseline run — `backend/Tools/Benchmarks/Common/BenchmarkStorage.cs`

**2. Regression detection — автоматическое сравнение с baseline**
  2.1. Создать `BenchmarkComparison` — логика сравнения MetricValue текущего run vs baseline — `backend/Tools/Benchmarks/Common/BenchmarkComparison.cs` [новый файл — добавить в Benchmarks.csproj]
       - Для throughput (ops/s): regression если текущий < baseline * (1 - threshold)
       - Для latency (ms): regression если текущий > baseline * (1 + threshold)
       - Default threshold: 10%
  2.2. В `BenchmarkRunner` после завершения run — вызвать comparison, записать результат — `backend/Tools/Benchmarks/Common/BenchmarkRunner.cs`
  2.3. Добавить поля в `BenchmarkState`: `BaselineMetricValue`, `RegressionPercent`, `IsRegression` — `backend/Tools/Benchmarks/Common/BenchmarkResult.cs`

**3. Regression threshold configuration**
  3.1. Добавить `RegressionThreshold` в `BenchmarkOptions` (default 0.10) — `backend/Tools/Benchmarks/Common/BenchmarkOptions.cs`
  3.2. Опционально: per-benchmark threshold override через `IClusterTest` interface — `backend/Tools/Benchmarks/Common/IClusterTest.cs`

**4. API и UI интеграция**
  4.1. Добавить endpoint `GET /api/benchmarks/{title}/compare` — сравнение последнего run vs baseline — `backend/Orchestration/ConsoleGateway/BenchmarkEndpoints.cs`
  4.2. В history response добавить regression info (delta %, is regression) — `backend/Orchestration/ConsoleGateway/BenchmarkEndpoints.cs`

### Ключевые файлы

| Файл | Роль в задаче |
|------|---------------|
| `backend/Tools/Benchmarks/Common/BenchmarkResult.cs` | BenchmarkState — добавить baseline/regression поля |
| `backend/Tools/Benchmarks/Common/BenchmarkStorage.cs` | GetAll, Write — добавить GetBaseline |
| `backend/Tools/Benchmarks/Common/BenchmarkRunner.cs` | Запуск бенчмарков — добавить comparison после run |
| `backend/Tools/Benchmarks/Common/BenchmarkOptions.cs` | Constants — добавить RegressionThreshold |
| `backend/Tools/Benchmarks/Common/IClusterTest.cs` | Interface — опционально добавить threshold override |
| `backend/Tools/Benchmarks/Common/BenchmarkMetricsHandle.cs` | Сбор метрик — Collect() возвращает BenchmarkState |
| `backend/Orchestration/ConsoleGateway/BenchmarkEndpoints.cs` | API endpoints для baseline и comparison |
| `backend/Tools/Benchmarks/feature.md` | Ideas list — обновить статус после реализации |
| `backend/Tools/Benchmarks/Benchmarks.csproj` | Добавить новый файл BenchmarkComparison.cs |

### Документация к прочтению
- `rules/ORLEANS_STATE.md` — добавление полей в state class ([Id(N)] sequencing)
- `rules/CODE_STYLE.md` — member order, naming

### Риски
- **Flaky results**: Бенчмарки чувствительны к нагрузке на машину. Один и тот же бенчмарк может давать разброс 5-15%. Threshold 10% может давать false positives. Рассмотреть: среднее по N последним runs вместо single comparison.
- **State migration**: Добавление полей в `BenchmarkState` — новые `[Id(N)]` не сломают чтение старых записей (Orleans сериализация tolerant к новым полям), но старые записи будут иметь default values (IsBaseline=false, RegressionPercent=0).
- **Metric direction**: Некоторые бенчмарки меряют throughput (выше = лучше), другие latency (ниже = лучше). Нужен способ указать direction в `IClusterTest`.
