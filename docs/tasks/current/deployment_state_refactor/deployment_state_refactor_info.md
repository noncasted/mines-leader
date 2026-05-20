## Задача: Переделать LiveState в DeploymentState с персистентностью

### Цель
1. Переделать `LiveState<T>` так, чтобы помимо синхронизации через канал он также сохранял состояние в базу, как это делает `AddressableState<T>`.
2. Использовать `DeploymentId` в качестве ключа для чтения/записи состояния в базе.
3. При старте (смене деплоя) проверять, есть ли уже стейт в базе для данного `DeploymentId`. Если есть — загружать его. Если нет — использовать базовое значение.
4. При `SetValue` сначала писать стейт в базу, затем отправлять уведомление и новый стейт через канал.
5. Переименовать `LiveState<T>` в `DeploymentState<T>`, а `ILiveState<T>` в `IDeploymentState<T>`.
6. Обновить все точки использования: регистрацию в DI, консольные razor-страницы, коллекции сессий, матчмейкинг, подключенных пользователей, тесты.

### Контекст
- `LiveState<T>` сейчас живет в `backend/Cluster/Deploy/LiveState.cs`.
- Он реализует `IDeployAware` — получает `OnDeployChanged` при смене деплоя.
- Сейчас `SetValue` только публикует в канал `LiveStateChannelId<T>`, но не пишет в БД.
- `AddressableState<T>` (`backend/Infrastructure/Data/State/AddressableState.cs`) демонстрирует нужный паттерн: читает из `StateStorage` при старте, пишет в `StateStorage`, затем публикует в канал.
- `AddressableState` использует обертку `AddressableStateValue` (реализует `IDirectStateValue`) для хранения сериализованного JSON.
- Для `DeploymentState` состояние деплой-специфично, поэтому ключ в БД должен содержать `DeploymentId`.
- Таблица `cluster` уже существует и используется для кластерного состояния (`DeployState`, `ClusterFeaturesState`) — подходит для хранения deployment state.
- `IOrleans` (с `StateStorage` и `Serializer`) уже зарегистрирован в DI через `AddOrleansUtils`.

### Шаги реализации

**1. Создать новый DeploymentState**
  1.1. Переименовать `backend/Cluster/Deploy/LiveState.cs` → `DeploymentState.cs`.
  1.2. Переименовать `ILiveState<T>` → `IDeploymentState<T>`, `LiveState<T>` → `DeploymentState<T>`, `LiveStateChannelId<T>` → `DeploymentStateChannelId<T>`.
  1.3. Добавить в конструктор `DeploymentState<T>` зависимость от `IOrleans` (для `StateStorage` и `Serializer`).
  1.4. В `OnDeployChanged` создавать `StateIdentity` с ключом `{deployId:N}:{typeof(T).Name}`, типом `deployment_state`, таблицей `cluster`.
  1.5. В `OnDeployChanged` читать `AddressableStateValue` из `StateStorage`. Если `IsInitialized` — десериализовать `T` и установить. Затем подписаться на канал.
  1.6. В `SetValue` сначала установить локально, затем сериализовать в `AddressableStateValue`, записать в `StateStorage.Write`, затем опубликовать в канал.
  1.7. Обновить `ListenChannel` / `PublishChannel` на работу с `AddressableStateValue` (как в `AddressableState`).
  1.8. Обновить `LiveStateExtensions` → `DeploymentStateExtensions`, `AddLiveState` → `AddDeploymentState`.

**2. Обновить регистрацию**
  2.1. `backend/Cluster/Monitoring/MonitoringExtensions.cs` — заменить `AddLiveState` на `AddDeploymentState`.

**3. Обновить все использования `ILiveState<T>`**
  3.1. `backend/Console/Game/Matches/LiveMatches.razor` — `ILiveState<LiveMatchesData>` → `IDeploymentState<LiveMatchesData>`.
  3.2. `backend/Console/Game/Matchmaking/MatchmakingDashboard.razor` — `ILiveState<MatchmakingLiveData>` → `IDeploymentState<MatchmakingLiveData>`.
  3.3. `backend/Console/Home/ConnectedUsersWidget.razor` — `ILiveState<ConnectedUsersLiveData>` → `IDeploymentState<ConnectedUsersLiveData>`.
  3.4. `backend/Game/Global/SessionsCollection.cs` — `ILiveState<LiveMatchesData>` → `IDeploymentState<LiveMatchesData>`.
  3.5. `backend/Orchestration/MetaGateway/Matchmaking/Matchmaking.cs` — `ILiveState<MatchmakingLiveData>` → `IDeploymentState<MatchmakingLiveData>`.
  3.6. `backend/Orchestration/MetaGateway/UserFlow/ConnectedUsers.cs` — `ILiveState<ConnectedUsersLiveData>` → `IDeploymentState<ConnectedUsersLiveData>`.

**4. Обновить тесты**
  4.1. `backend/Tools/Tests/Meta/ConnectedUsersTests.cs` — `ILiveState<ConnectedUsersLiveData>` → `IDeploymentState<ConnectedUsersLiveData>`.
  4.2. `backend/Tools/Tests/Meta/MatchmakingTests.cs` — `ILiveState<MatchmakingLiveData>` → `IDeploymentState<MatchmakingLiveData>`.

### Ключевые файлы

| Файл | Роль в задаче |
|------|---------------|
| `backend/Cluster/Deploy/LiveState.cs` | Основной файл для переделки и переименования |
| `backend/Cluster/Monitoring/MonitoringExtensions.cs` | Регистрация LiveState в DI |
| `backend/Infrastructure/Data/State/AddressableState.cs` | Эталонный паттерн персистентности |
| `backend/Console/Game/Matches/LiveMatches.razor` | Использует `ILiveState<LiveMatchesData>` |
| `backend/Console/Game/Matchmaking/MatchmakingDashboard.razor` | Использует `ILiveState<MatchmakingLiveData>` |
| `backend/Console/Home/ConnectedUsersWidget.razor` | Использует `ILiveState<ConnectedUsersLiveData>` |
| `backend/Game/Global/SessionsCollection.cs` | Использует `ILiveState<LiveMatchesData>` |
| `backend/Orchestration/MetaGateway/Matchmaking/Matchmaking.cs` | Использует `ILiveState<MatchmakingLiveData>` |
| `backend/Orchestration/MetaGateway/UserFlow/ConnectedUsers.cs` | Использует `ILiveState<ConnectedUsersLiveData>` |
| `backend/Tools/Tests/Meta/ConnectedUsersTests.cs` | Мокает `ILiveState` |
| `backend/Tools/Tests/Meta/MatchmakingTests.cs` | Мокает `ILiveState` |

### Документация к прочтению
- `.agents/docs/COMMON_ORLEANS.md` — `Grain`, `State<T>`, `IStateValue`, `StateCollection`, `IOrleans`, `AddressableDictionary`, messaging
- `.agents/docs/COMMON_REACTIVE_BASICS.md` — `EventSource`, `ViewableProperty`, `ViewableList`
- `.agents/docs/CODE_STYLE_FULL.md` — member order, naming, braces, `NoAwait`

### Риски
- Изменение типа канала с `T` на `AddressableStateValue` требует, чтобы все подписчики канала (`DeploymentState<T>` на всех нодах) ожидали `AddressableStateValue`. Поскольку все подписчики — это те же `DeploymentState<T>`, это безопасно.
- Ключ в БД должен быть уникальным для каждой комбинации `DeploymentId + T`. Формат `{deployId:N}:{typeof(T).Name}` обеспечивает это.
- `SetValue` теперь пишет в БД — возможно снижение производительности при частых обновлениях. Но пользователь явно запросил такое поведение.
- При записи в БД ошибка будет прокинута наружу (fail-fast), в отличие от старого `LiveState`, который логировал и проглатывал. Это согласуется с `AddressableState`.
