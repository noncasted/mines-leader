## Benchmark Regression Detection — Результат

### Статус: Завершено

### Что сделано
- Baseline pinning: IsBaseline поле в BenchmarkState, SetBaseline API endpoint, GetBaseline() в Storage
- BenchmarkComparison.cs: MetricDirection enum, Compare() с threshold 10%
- Автоматическое сравнение после run в BenchmarkRoot с warning при регрессии
- API: POST set-baseline, GET compare, regression info в history DTO
- CalculateMetricValue() метод для единообразного вычисления метрик

### Измененные файлы

| Файл | Что изменено |
|------|-------------|
| `backend/Tools/Benchmarks/Common/BenchmarkComparison.cs` | Новый файл — логика сравнения |
| `backend/Tools/Benchmarks/Common/BenchmarkMetricsHandle.cs` | +4 поля [Id(6-9)], +CalculateMetricValue() |
| `backend/Tools/Benchmarks/Common/BenchmarkOptions.cs` | +RegressionThreshold = 0.10 |
| `backend/Tools/Benchmarks/Common/BenchmarkStorage.cs` | +GetById(), +GetBaseline() |
| `backend/Tools/Benchmarks/Common/BenchmarkRoot.cs` | Comparison после run |
| `backend/Orchestration/ConsoleGateway/BenchmarkEndpoints.cs` | +set-baseline, +compare, +regression fields |

### Исправления после review
- HIGH: Формула метрики дублировалась 5 раз — добавлен CalculateMetricValue()
- HIGH: Несогласованный fallback (stopwatch.ElapsedMilliseconds vs 0) — унифицирован
- MEDIUM: Compare endpoint делал 2 скана вместо 1 — используем states.FirstOrDefault
