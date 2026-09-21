# Общая архитектура

## Три слоя

```mermaid
graph TB
    subgraph Client["Client (Unity3D)"]
        UI[UI / Scenes]
        Net[Network Layer]
        DI[Generated Container]
    end

    subgraph Shared["Shared"]
        Proto[Protocol / MemoryPack]
        Models[Domain Models]
        Configs[Configurations]
    end

    subgraph Backend["Backend (.NET Orleans)"]
        subgraph Meta["Meta"]
            Users[Users]
            Matches[Matches]
            Bots[Bots]
        end
        subgraph Game["Game"]
            Sessions[Sessions]
            GamePlay[GamePlay]
            Board[Board + Cards]
        end
        subgraph Infra["Infrastructure"]
            Orleans[Orleans Core]
            Msg[Messaging]
            State[State Management]
            Tx[Transactions]
        end
        subgraph Orch["Orchestration"]
            Aspire[.NET Aspire]
            Coord[Coordinator]
        end
    end

    DB[(PostgreSQL)]

    Client <-->|WebSocket + MemoryPack| Backend
    Client -.->|uses| Shared
    Backend -.->|uses| Shared
    Backend <-->|ADO.NET| DB
```

---

## Клиент (Unity3D)

| Компонент | Технология | Описание |
|-----------|-----------|----------|
| DI | Свой контейнер (Roslyn codegen) | `ContainerBuilder` + сгенерированный `IContainer`; VContainer только в бенчмарках |
| Сервисы | MonoBehaviour + ISceneService | Регистрация в скоупе через `Create()`; setup — отдельные `IScopeSetup*` |
| Реактивность | EventSource / ViewableProperty / ViewableList | Наблюдаемые события и состояния |
| Ресурсы | Lifetime | Управление подписками и очисткой |
| Async | UniTask | Асинхронные операции |
| Сеть | WebSocket + MemoryPack | Бинарный протокол |

### Скоупы (Scopes)

```mermaid
graph TD
    I[Internal Scope] --> G[Global Scope]
    G --> M[Meta Scope]
    M --> L[Game Loop Scope]
    L --> Menu[Menu Scope]
    L --> GP[GamePlay Scope]
```

| Скоуп | Назначение | Сервисы |
|-------|-----------|---------|
| **Internal** | Корень DI, каталог ассетов, лоадеры скоупов | ServiceScopeLoader, ServiceScopeSceneLoader, Options |
| **Global** | Глобальная инфраструктура | Audio, Camera, Input, BackendClient, Settings |
| **Meta** | Авторизация и пользователь | Auth, User, MetaBackend, Matchmaking |
| **Menu** | Главное меню | MenuLoop, Navigation, Social, Decks |
| **GamePlay** | Активная игра | Board, Players, Cards, Sync, UI |

Подробнее: [[client-scenes|Клиент: сцены]], [[client-common|Клиент: Common]], [[client-dev-tools|инструменты разработки]]

---

## Бэкенд (.NET Orleans)

### Проекты (подпапки backend/)

| Проект | Назначение |
|--------|-----------|
| `Game/` | Игровые сессии, геймплей, доска, карты, бот |
| `Meta/` | Пользователи, матчи, рейтинг, прогрессия, колоды |
| `Infrastructure/` | Orleans-ядро: стейты, транзакции, messaging |
| `Orchestration/` | Aspire, конфигурация, запуск кластера |
| `Common/` | Общие утилиты, StatesLookup |
| `Tests/` | Тесты |
| `Cluster/` | Конфиг-стейты кластера |

### Сервисы Aspire

```mermaid
graph LR
    PG[(PostgreSQL)] --> Silo
    Silo[Silo<br>Orleans host] --> Coord[Coordinator]
    Silo --> MetaGW[Meta Gateway]
    Silo --> GameGW[Game Gateway]
    Silo --> Console[Console<br>Blazor]
```

| Сервис | Роль |
|--------|------|
| **Silo** | Хост Orleans грейнов |
| **Coordinator** | Настройка кластера, конфиги, боты |
| **Meta Gateway** | REST API для авторизации, матчмейкинга |
| **Game Gateway** | WebSocket-сервер для игровых сессий |
| **Console** | Админ-панель (Blazor) |

Порядок запуска: Silo -> (Coordinator, Meta, Game, Console) параллельно.

### Грейны

7 основных грейнов. Подробнее: [[backend-grains|Orleans грейны]]

### Messaging

3 паттерна обмена сообщениями. Подробнее: [[backend-messaging|Messaging]]

### Транзакции

Кастомные ACID-транзакции. Подробнее: [[backend-transactions|Транзакции]]

---

## Shared

Общие модели и протоколы, используемые клиентом и бэкендом:

| Группа | Содержимое |
|--------|-----------|
| `Domain/` | Enums: CardType, GameMatchType, SessionType, CharacterType |
| `Protocol/` | Базовые сетевые сообщения, union types, MemoryPack |
| `Backend/` | Протоколы: авторизация, матчмейкинг, проекции |
| `Session/` | Протоколы: сессия, игроки, объекты |
| `Game/` | Модели: доска, клетки, карты, состояния, снапшоты |
| `Configs/` | Конфигурации: карты, режимы, боты, рейтинг |

Подробнее: [[shared-protocol|Shared протокол]]

---

## Поток данных: от клика до обновления

```mermaid
sequenceDiagram
    participant C as Client
    participant GW as Game Gateway
    participant S as Session
    participant G as Grain

    C->>GW: SharedGameAction.Open (WebSocket)
    GW->>S: CommandDispatcher routes
    S->>S: OpenCellCommand.Execute()
    S->>S: Board.Reveal() + cascade
    S->>S: Health.TakeDamage() if mine
    S->>C: PropertyUpdate (Board, Health)
    S->>S: Check win conditions
    opt Игра окончена
        S->>G: Match.OnComplete() [Transaction]
        G->>G: Rating + Progression + History
    end
```
