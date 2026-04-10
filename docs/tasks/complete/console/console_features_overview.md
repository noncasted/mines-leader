# Console Features — Master Plan

Общий план по добавлению новых фич в админ-консоль проекта.

## Текущее состояние консоли

| Раздел | Страницы | Статус |
|--------|----------|--------|
| Home | Dashboard, виджеты, навигация | Готово |
| Configs | 9 редакторов (карты, режимы, рейтинг, боты) | Готово |
| Bots | Создание, редактирование деков | Готово |
| Players | PlayersWidget (Home) + UserDashboard | Готово |
| Features | 3 тоггла (коннекты, матчмейкинг, side effects) | Готово |
| Infrastructure | Side effects, queues, транзакции, task balancer | Готово |
| Benchmarks | Запуск, история, графики | Готово |
| Match | Просмотр конкретного матча | Готово |

## Новые задачи (порядок приоритета)

### Tier 1 — Высокий приоритет

| # | Задача | Файл | Backend готовность |
|---|--------|------|--------------------|
| 1 | Live Matches Monitor | `01_live_matches_monitor.md` | SessionsCollection уже есть |
| 2 | Matchmaking Dashboard | `02_matchmaking_dashboard.md` | Нужен stats API в Matchmaking |
| 3 | Connected Users Monitor | `03_connected_users_monitor.md` | IConnectedUsers уже есть |
| 4 | Side Effects Monitor | `04_side_effects_monitor.md` | ISideEffectsStorage API готов |

### Tier 2 — Средний приоритет

| # | Задача | Файл | Backend готовность |
|---|--------|------|--------------------|
| 5 | Cluster Health | `05_cluster_health.md` | IServiceDiscovery уже есть |
| 6 | Match History Browser | `06_match_history_browser.md` | Нужен глобальный индекс матчей |
| 7 | Card Balance Analytics | `07_card_balance_analytics.md` | Нужен сбор статистики |

### Tier 3 — Dev Tools

| # | Задача | Файл | Backend готовность |
|---|--------|------|--------------------|
| 8 | Debug Panel | `08_debug_panel.md` | CheatCommands уже есть |
| 9 | State Explorer | `09_state_explorer.md` | StateStorage.Read уже есть |
| 10 | Audit Log | `10_audit_log.md` | Нет инфраструктуры |

## Общие изменения для всех задач

Каждая задача затрагивает:
- `backend/Console/Common/ConsoleConstants.cs` — добавить роут
- `backend/Orchestration/ConsoleGateway/Shared/MainLayout.razor` — навигация (если отдельная страница)
- `backend/Console/Pages/Home/HomeGameSection.razor` или `HomeInfrastructureSection.razor` — карточка на Home

## Рекомендуемый порядок реализации

1. **Batch 1** (минимальные backend-изменения): #1 Live Matches, #3 Connected Users, #5 Cluster Health
2. **Batch 2** (нужен новый backend API): #2 Matchmaking Dashboard, #4 Side Effects Monitor
3. **Batch 3** (нужна новая инфраструктура): #6 Match History, #7 Card Analytics
4. **Batch 4** (dev tools): #8 Debug Panel, #9 State Explorer, #10 Audit Log
