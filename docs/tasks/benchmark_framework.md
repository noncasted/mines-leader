## Задача: Фреймворк бенчмаркинга с историей и аналитикой

### Цель
Преобразовать существующий тестовый фреймворк (IClusterTest / ClusterTestRoot) в полноценный фреймворк бенчмаркинга с замерами метрик (MessagesPerSecond, OperationsPerSecond и т.д.), сохранением результатов в PostgreSQL, и UI в консоли с историей запусков, динамикой изменения метрик и сравнением с предыдущими запусками.

### Шаги реализации

**Фаза 1 — Backend: модель данных и хранение результатов**

1. Создать `BenchmarkResult` state класс с метриками — `backend/Benchmarks/Common/BenchmarkResult.cs` [новый файл — добавить в Benchmarks.csproj]
2. Добавить `BenchmarkResult` в StatesLookup — `backend/Common/Lookups/StatesLookup.cs`
3. Зарегистрировать state в AddStates() — `backend/Orchestration/Extensions/ProjectsSetupExtensions.cs`
4. Создать `BenchmarkStorage` — `backend/Benchmarks/Common/BenchmarkStorage.cs` [новый файл]
5. Зарегистрировать `BenchmarkStorage` в DI — `backend/Orchestration/Extensions/ProjectsSetupExtensions.cs`

**Фаза 2 — Backend: расширение IClusterTest для метрик**

6. Расширить `IClusterTest` — `backend/Benchmarks/Common/IClusterTest.cs`
7. Расширить `ClusterTestRoot<T>` — `backend/Benchmarks/Common/ClusterTestRoot.cs`
8. Расширить `ClusterTestNodeHandle` — `backend/Benchmarks/Common/ClusterTestNodeHandle.cs`
9. Обновить каждый бенчмарк с вызовом `ReportMetric()`

**Фаза 3 — Console UI: страница бенчмарков**

10. Переименовать/переделать страницу Tests -> Benchmarks — `backend/Console/Pages/Tests/Tests.razor`
11. Переделать TestEntry -> BenchmarkEntry — `backend/Console/Pages/Tests/TestEntry.razor`
12. Создать `BenchmarkHistory.razor` — `backend/Console/Pages/Tests/BenchmarkHistory.razor` [новый файл]
13. Обновить `ConsoleConstants.cs` — `backend/Console/Common/ConsoleConstants.cs`
14. Обновить навигацию — `backend/Console/Pages/Home/HomeInfrastructureSection.razor`

**Фаза 4 — Трекинг и документация**

15. Создать `feature.md` — `backend/Benchmarks/feature.md` [новый файл]

### Ключевые файлы

| Файл | Роль в задаче |
|------|---------------|
| `backend/Benchmarks/Common/IClusterTest.cs` | Интерфейс бенчмарка — расширить метриками |
| `backend/Benchmarks/Common/ClusterTestRoot.cs` | Базовый класс — добавить замеры и сохранение |
| `backend/Benchmarks/Common/ClusterTestNodeHandle.cs` | Handle — проксировать метрики |
| `backend/Common/Lookups/StatesLookup.cs` | Регистрация таблицы результатов |
| `backend/Orchestration/Extensions/ProjectsSetupExtensions.cs` | DI регистрация |
| `backend/Console/Pages/Tests/Tests.razor` | Главная страница бенчмарков |
| `backend/Console/Pages/Tests/TestEntry.razor` | Компонент бенчмарка |
| `backend/Console/Common/ConsoleConstants.cs` | Роуты |

### Риски
- SQL vs Orleans state: результаты лучше хранить через прямой SQL (IDbSource) для агрегатных запросов
- 37 файлов бенчмарков нужно обновить — базовый класс автоматизирует часть, но messaging нужно вручную
