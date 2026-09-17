# Mines Leader

Competitive multiplayer minesweeper с карточной механикой. Два игрока соревнуются на параллельных минных полях, используя карты для атаки и защиты.

## Навигация

### Игра
- [[game/overview|Обзор игры]] — ключевые концепции и терминология
- [[game/game-modes|Игровые режимы]] — Single, TimeLimited, LastManStanding
- [[game/cards|Карточки]] — 10 карт с эффектами и параметрами
- [[game/board|Игровое поле]] — доска, клетки, мины, каскадное открытие
- [[game/match-flow|Цикл матча]] — от поиска игры до результата
- [[game/bot-system|Бот-система]] — AI-противник и стратегии
- [[game/card-ideas|Идеи карточек]] — анализ и предложения новых карт

### Архитектура
- [[architecture/overview|Общая архитектура]] — клиент + бэкенд + shared
- [[architecture/backend-grains|Orleans грейны]] — грейны, стейты, коллекции
- [[architecture/backend-messaging|Messaging]] — DurableQueue, RuntimePipe, RuntimeChannel
- [[architecture/backend-transactions|Транзакции]] — ACID-транзакции и StateCollection
- [[architecture/client-common|Клиент: Common]] — сборки, старт, скоупы, контейнер, каталоги
- [[architecture/client-dev-tools|Клиент: инструменты разработки]] — каталоги, биндинги, контейнер, профайлер, моки
- [[architecture/client-scenes|Клиент: сцены]] — Unity сцены, скоупы, DI
- [[architecture/client-network|Клиент: сеть]] — WebSocket, MetaBackend, проекции
- [[architecture/client-webgl-build-size|Клиент: размер WebGL-билда]] — встроенные пакеты, вырезанные модули, что повторить при обновлении Unity
- [[architecture/shared-protocol|Shared протокол]] — MemoryPack, модели, конфиги

### Гайды
- [[guides/nginx setup|Настройка nginx]] — nginx, SSL, .NET, Aspire
- [[guides/Claude code setup|Claude Code]] — настройка Claude Code в Docker

## Стек технологий

| Слой | Технологии |
|------|-----------|
| **Клиент** | Unity3D, свой codegen-контейнер, UniTask, кастомный реактивный фреймворк |
| **Бэкенд** | .NET, Orleans (распределённая акторная модель), PostgreSQL |
| **Shared** | MemoryPack (сериализация), общие модели и протоколы |
| **Оркестрация** | .NET Aspire, nginx |
