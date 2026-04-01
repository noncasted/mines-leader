## Задача: Графики прогрессии бенчмарков с пошаговыми снапшотами

### Цель
Во время выполнения бенчмарка собирать метрики каждые N% прогресса (шаги), сохранять дифференциальные значения в БД, и отображать в консоли мульти-линейный график по последним 10 прогонам — ось X = шаги прогресса, ось Y = значение метрики на шаге.

### Шаги реализации

1. Создать модель `BenchmarkSnapshot` — `backend/Benchmarks/Common/BenchmarkSnapshot.cs` [новый файл]
2. Создать таблицу `benchmark_snapshots` — `backend/Orchestration/Aspire/Startup/BenchmarkSetup.cs`
3. Добавить методы `SaveSnapshots()` / `GetSnapshots()` — `backend/Benchmarks/Common/BenchmarkStorage.cs`
4. Добавить сбор снапшотов в `ClusterTestNodeHandle` — метод `RecordSnapshot(double value)` — `backend/Benchmarks/Common/ClusterTestNodeHandle.cs`
5. Интегрировать автосохранение снапшотов в `ClusterTestRoot` после `Run()` — `backend/Benchmarks/Common/ClusterTestRoot.cs`
6. Автоматический сбор снапшотов в `RunConcurrentIterations` на каждом шаге итерации — `backend/Benchmarks/TestsExtensions.cs`
7. Создать компонент `BenchmarkProgressChart.razor` с мульти-линейным графиком — `backend/Console/Pages/Tests/BenchmarkProgressChart.razor` [новый файл]
8. Обновить `TestEntry.razor` — загрузка снапшотов и отображение графика — `backend/Console/Pages/Tests/TestEntry.razor`

### Ключевые файлы

| Файл | Роль в задаче |
|------|---------------|
| `backend/Benchmarks/Common/BenchmarkSnapshot.cs` | Новая модель: ResultId, StepIndex, StepPercent, MetricValue, DiffValue |
| `backend/Benchmarks/Common/BenchmarkStorage.cs` | Хранение и чтение снапшотов из PostgreSQL |
| `backend/Benchmarks/Common/ClusterTestNodeHandle.cs` | API для бенчмарков: `RecordSnapshot(double)` |
| `backend/Benchmarks/Common/ClusterTestRoot.cs` | Автосохранение снапшотов после Run() |
| `backend/Benchmarks/TestsExtensions.cs` | Авто-снапшоты в RunConcurrentIterations |
| `backend/Orchestration/Aspire/Startup/BenchmarkSetup.cs` | DDL для таблицы benchmark_snapshots |
| `backend/Console/Pages/Tests/BenchmarkProgressChart.razor` | Компонент с мульти-линейным CSS-графиком |
| `backend/Console/Pages/Tests/TestEntry.razor` | Интеграция графика в expanded секцию |

### Документация к прочтению
- `rules/BLAZOR.md` — создаём новый Razor-компонент с `[Parameter, EditorRequired]`

### Риски
- `RunConcurrentIterations` уже вызывает `ReportMetric` внутри — снапшоты должны использовать отдельный канал, не конфликтуя с финальной метрикой
- Для бенчмарков без `RunConcurrentIterations` (messaging, ms) снапшоты надо собирать вручную через `handle.RecordSnapshot()` — но это опционально, а не обязательно
- Таблица `benchmark_snapshots` может быть большой (20 снапшотов * N прогонов * 40+ бенчмарков) — нужен индекс по `result_id`
