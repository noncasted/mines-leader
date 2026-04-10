# Console UI - Отчёт о выполненных задачах

10 консольных страниц + startup overlay. Задачи описаны как спецификации - реальный код частично присутствует в diff (домашние страницы, константы, инфраструктурные хуки), но большинство .razor файлов новых страниц ещё не созданы физически.

## Что подтверждено в diff

### Startup Overlay
- `ClusterFeatures.cs` - расширение состояния кластера

### Общие изменения
- `ConsoleConstants.cs` - добавлены 9 новых маршрутов
- `Home.razor` - расширена домашняя страница
- `HomeGameSection.razor` - карточки Live Matches, Match History, Card Analytics
- `HomeInfrastructureSection.razor` - карточки Side Effects, Cluster Health
- `Configs.razor`, `Features.razor` - мелкие правки (вероятно audit log hooks)
- `InfrastructureOptions.razor` - обновления
- `Benchmarks.razor` - переработан под новый фреймворк

### Live Matches Monitor (#1)
- `SessionsCollection.cs` - добавлен `GetOverviews()` (+34 строки)
- `Session.cs` - расширены метаданные сессии

### Matchmaking Dashboard (#2)
- `Matchmaking.cs` - `MatchmakingStats`, counters _matchesCreated/_botMatchesCreated (+75 строк)

### Connected Users (#3)
- `ConnectedUsers.cs` - `ConnectedUserInfo` DTO, tracking ConnectedAt (+28 строк)

### Side Effects Monitor (#4)
- `SideEffectsStorage.cs` - `GetStats()`, `GetRetryEntries()`, `DropRetryEntry()`, `RequeueRetryEntry()` (+238 строк)

## Что описано в задачах, но отсутствует в diff

Следующие страницы описаны как спецификации, но их .razor файлы не видны в текущем diff:
- #5 Cluster Health (ClusterHealth.razor)
- #6 Match History Browser (MatchHistory.razor + MatchCollection)
- #7 Card Balance Analytics (CardAnalytics.razor)
- #8 Debug Panel (DebugPanel.razor)
- #9 State Explorer (StateExplorer.razor)
- #10 Audit Log (AuditLog.razor)

Backend-подготовка для них частично есть (StatesRegistry, SideEffectsStorage API), но UI ещё не реализован.
