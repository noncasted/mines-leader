## Prefab Catalog — Результат

### Статус: Завершено

### Что сделано
- Inspector-модуль на ручных `.prefab` (Project, root asset): Catalog / Group / Root, persist в `importer.userData` key `prefabCatalog`, merge JSON.
- Генератор `Tools/GeneratePrefabsCatalog` пишет `PrefabGroupAsset` + typed C# + Addressables (`Prefabs_{Group}`, Pack Together, address = GUID ассета группы).
- `Prefabs` стал `partial`: PrefabBuilder `GlobalCamera` и каталог `Prefabs.GamePlay` сосуществуют.
- `builder.LoadPrefabGroup(Prefabs.GamePlay)` — Retain на BeforeBuild, Release на BeforeDispose. Getter только `EnsureLoaded()`, никогда не грузит.
- Свойства группы типизированы Root из инспектора: `Prefabs.GamePlay.CardLocal` — `CardScopeEntity`. Сгенерированный класс живёт в сборке потребителя (`GamePlay`), Addressables — в `PrefabGroup` (Tools.Runtime).
- `GamePrefabs` удалён. Cell / CardLocal / CardRemote / DeckCard / StashCard инстансятся из `Prefabs.GamePlay.*`.

### Измененные файлы

| Файл | Что изменено | Шаг | Evidence |
|------|-------------|-----|----------|
| `client/Assets/Tools/Editor/PrefabCatalog/*` | Inspector, metadata, groups json, generator, addressables sync | 1–2 | compile Tools.Editor.dll; нет CustomEditor/SaveAndReimport |
| `client/Assets/Tools/Runtime/PrefabCatalog/PrefabGroup.cs` | Retain/Release/EnsureLoaded | 2 | copy SpriteGroup |
| `client/Assets/Tools/Runtime/PrefabCatalog/PrefabEntry.cs` | `As<T>()` + implicit GameObject | 2 | — |
| `client/Assets/Tools/Runtime/PrefabCatalog/PrefabGroupAsset.cs` | Get + component validation | 2 | GamePlay.asset 6 entries |
| `client/Assets/Tools/Runtime/PrefabCatalog/PrefabCatalogExtensions.cs` | `LoadPrefabGroup` | 3 | copy SpriteBuilderExtensions |
| `client/Assets/Tools/Runtime/PrefabCatalog/Generated/Prefabs.Catalog.cs` | `partial Prefabs.GamePlay` | 2 | generator |
| `client/Assets/Tools/Runtime/PrefabCatalog/Generated/GamePlayPrefabs.cs` | typed getters | 2 | generator; second run skip-write |
| `client/Assets/Tools/Runtime/PrefabBuilder/Prefabs.cs` | `partial class Prefabs` | 2 | GlobalCamera still present |
| `client/Assets/Tools/Editor/PrefabBuider/PrefabsClassGenerator.cs` | emit `partial` | 2 | — |
| `client/Assets/GamePlay/Services/GamePlayServicesExtensions.cs` | LoadPrefabGroup, drop RegisterAsset | 4 | GamePlay.dll |
| `client/Assets/GamePlay/Loop/PvP/PvPScopeExtensions.cs` | LoadPrefabGroup | 4 | GamePlay.dll |
| `client/Assets/GamePlay/Generated/PrefabCatalog/GamePlayPrefabs.cs` | typed getters по Root | 4 | GamePlay.dll |
| `client/Assets/GamePlay/Cards/Services/Factory/CardFactory.cs` | `Prefabs.GamePlay.CardLocal` is `CardScopeEntity` | 4 | GamePlay.dll |
| `client/Assets/GamePlay/Cards/Deck/DeckView.cs` | catalog instantiate | 4 | GamePlay.dll |
| `client/Assets/GamePlay/Cards/Stash/StashView.cs` | catalog instantiate | 4 | GamePlay.dll |
| `client/Assets/GamePlay/Boards/Root/BoardConstructor.cs` | catalog Cell; no `_cellPrefab` | 4 | GamePlay.dll |
| `client/Assets/GamePlay/Prefabs/GamePrefabs.cs` | deleted | 4 | grep GamePrefabs in Assets = 0 |
| six `.prefab.meta` | `prefabCatalog` userData, group GamePlay | 4 | generator picked them up |

### Отличия от плана
- `PrefabImporter` в Unity 6000 internal — inspector матчит `AssetImporter` + persistent prefab-asset root GameObject.
- Пути префабов: `Prefabs/Boards/Cell.prefab`, `Prefabs/Cards/*`, не `Boards/Options`.
- `AvatarMovesView` / `AvatarTurnPointView` уже удалены. `PlayerTurnPoint` в каталоге с пустым `componentType` (GameObject), иначе `LoadGroup` падает на missing script.
- Editor Preview в `BoardConstructor` читает `PrefabGroupAsset` через AssetDatabase, если группа не loaded.
- Typed Root: generated group class is in the consumer assembly (`namespace GamePlay { class Prefabs }`), not Tools.Runtime, to avoid Tools → GamePlay cycle. Call sites in `GamePlay.*` resolve `Prefabs` via parent namespace.

### Нерешенные вопросы
- Play mode vs-bot (карты + доска 16×16) не прогоняли: MCP for Unity не был подключён к запущенному Editor.
