## Benchmark Regression Detection — Рабочие заметки

### Статус: Завершено

### Реализованные изменения

**BenchmarkMetricsHandle.cs** — добавлены поля в BenchmarkState:
- [Id(6)] IsBaseline: bool
- [Id(7)] BaselineMetricValue: double
- [Id(8)] RegressionPercent: double
- [Id(9)] IsRegression: bool

**BenchmarkOptions.cs** — добавлена константа:
- RegressionThreshold = 0.10 (10%)

**BenchmarkStorage.cs** — добавлены методы:
- GetById(Guid id) — поиск run по ID
- GetBaseline(string benchmarkName) — получение последнего baseline

**BenchmarkComparison.cs** (новый файл) — логика сравнения:
- MetricDirection enum (HigherIsBetter / LowerIsBetter)
- BenchmarkComparison.Compare() — вычисляет delta и флаг регрессии
- BenchmarkComparisonResult — результат сравнения

**BenchmarkRoot.cs** — интеграция comparison после запуска:
- Перед сохранением state получает baseline, вычисляет comparison
- Логирует warning при обнаружении регрессии

**BenchmarkEndpoints.cs** — новые endpoints:
- POST /api/benchmarks/{id}/set-baseline — пометить run как baseline
- GET /api/benchmarks/{title}/compare — сравнить последний run с baseline
- BenchmarkHistoryEntryDto дополнен полями: IsBaseline, BaselineMetricValue, RegressionPercent, IsRegression
- Новый DTO: BenchmarkCompareDto

### Решения

- GetById итерирует через ReadAll — нет прямого lookup по Guid в IStateStorage
- MetricDirection = HigherIsBetter для всех бенчмарков по умолчанию (throughput ops/s)
- Ошибки в Console.csproj (BotManagement.razor, PlayersWidget.razor) — pre-existing, не связаны с задачей
- BenchmarkComparison.cs не требует добавления в .csproj — SDK-style проект включает все .cs файлы автоматически
