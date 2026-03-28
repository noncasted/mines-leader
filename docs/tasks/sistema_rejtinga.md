## Задача: Система рейтинга

### Цель
Реализовать персональный рейтинг игрока: хранить историю изменений (Win/Loss-записи) в отдельном грейне, начислять/снимать фиксированные значения из конфига при завершении матча, отправлять проекцию клиенту.

### Шаги реализации

1. Создать `RatingOptions` — `shared/Configs/RatingOptions.cs` [новый файл]
2. Добавить `RatingProjection` и зарегистрировать в Union — `shared/Backend/SharedBackendUser.cs`
3. Создать интерфейс записи — `backend/Meta/Users/Rating/IUserRatingRecord.cs` [новый файл]
4. Создать Win/Loss-записи — `backend/Meta/Users/Rating/UserRatingRecords.cs` [новый файл]
5. Создать грейн `UserRating` + состояние `UserRatingState` — `backend/Meta/Users/Rating/UserRating.cs` [новый файл]
6. Добавить `UserRating` в `StatesLookup` — `backend/Common/Lookups/StatesLookup.cs`
7. Зарегистрировать `UserRatingState` в `AddStates()` — `backend/Orchestration/Extensions/ProjectsSetupExtensions.cs`
8. Добавить свойство `Rating` в `UserHandle` — `backend/Meta/Users/Common/UserHandle.cs`
9. Создать `IRatingConfig` / `RatingConfigState` — `backend/Cluster/Configs/RatingConfigState.cs` [новый файл]
10. Зарегистрировать `RatingConfigState` — `backend/Cluster/Configs/ConfigsExtensions.cs`
11. Добавить инициализацию конфига рейтинга — `backend/Cluster/Configs/ClusterConfigsSetup.cs`
12. Начислять рейтинг в `OnComplete` — `backend/Meta/Matches/Match.cs`
13. Добавить редактор конфига рейтинга в консоль — `backend/Console/Pages/Configs/Configs.razor`

### Ключевые файлы

| Файл | Роль в задаче |
|------|---------------|
| `backend/Meta/Users/Progression/UserProgression.cs` | Эталонный паттерн — грейн с историей записей, копируем структуру для рейтинга |
| `backend/Meta/Users/Progression/IUserProgressionRecord.cs` | Интерфейс записи — повторяем аналогично для `IUserRatingRecord` |
| `backend/Cluster/Configs/BotConfigState.cs` | Паттерн `AddressableState` — как создать `IRatingConfig` |
| `backend/Cluster/Configs/ConfigsExtensions.cs` | Регистрация конфигов — добавить `RatingConfigState` |
| `backend/Cluster/Configs/ClusterConfigsSetup.cs` | Инициализация из JSON — добавить `config.rating` |
| `backend/Meta/Matches/Match.cs` | Точка начисления рейтинга — вызов `winner.Rating.AddRecord(...)` |
| `shared/Backend/SharedBackendUser.cs` | Проекции пользователя — добавить `RatingProjection` и зарегистрировать в Union |
| `backend/Common/Lookups/StatesLookup.cs` | Реестр состояний — добавить `UserRating` с таблицей `state_user_rating` |

### Документация к прочтению
- `rules/ORLEANS_GRAINS.md` — создаём новый грейн `UserRating`
- `rules/ORLEANS_STATE.md` — шаги добавления нового состояния (3 обязательных шага: StatesLookup + AddStates + регистрация)

### Риски
- `IUserRatingRecord` — нужно зарегистрировать как union-тип Orleans (`[GenerateSerializer]` на каждом подклассе Win/Loss), иначе сериализация списка полиморфных объектов сломается — ровно как это сделано для `IUserProgressionRecord`
- `Match.cs` сейчас инжектит `IOptions<ProgressionOptions>`, но рейтинг-конфиг должен идти через `IRatingConfig` (AddressableState) — значение берём синхронно через `.Value.WinRating`, не через `IOptions`
- В `Configs.razor` нужно добавить и `@inject IRatingConfig`, и поле `_ratingValue`, и вызов `RatingConfig.SetValue(...)` в `Save()`, и `SaveConfig("config.rating", _ratingValue)` в `UpdateJson()`
