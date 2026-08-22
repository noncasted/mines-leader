---
task: prefab_catalog
updated: 2026-08-21
---

## Snapshot

| Шаг | Статус | Evidence | Блокер |
|-----|--------|----------|--------|
| 1 Inspector + metadata | [x] | `PrefabCatalogInspector` via `finishedDefaultHeaderGUI`; нет `CustomEditor` / `SaveAndReimport`; `userData` key `prefabCatalog` | — |
| 2 Runtime types + generator | [x] | `Tools/GeneratePrefabsCatalog` → `[PrefabCatalogGenerator] Generated 1 prefab group(s).`; `Prefabs` partial; addressables group `Prefabs_GamePlay` only `GamePlay.asset` (GUID address) | — |
| 3 LoadPrefabGroup + lifetime | [x] | `PrefabCatalogExtensions.LoadPrefabGroup` copies sprite lifetime wiring; `EnsureLoaded` only throws | — |
| 4 Migrate GamePlay consumers | [x] | Grep `GamePrefabs` in `client/Assets` = 0; `BoardConstructor` без `_cellPrefab`; GamePlay.dll compiled | — |

## Заметки

- Контракт: `prefab_catalog_info.md`. Если код и файл расходятся — побеждает info.
- Не начинать slice 4, пока 1–3 не зелёные: миграция без генератора оставит пустые ссылки.
- PrefabBuilder `Tools/GeneratePrefabs` не трогать по имени меню; каталог — `Tools/GeneratePrefabsCatalog`.

### [2026-08-21] Resume /workflow — начинаем реализацию

Повторный `/workflow` на существующую папку: `_result.md` пустой, все 4 шага pending. Идём по плану.

Находки относительно spec:
- `Cell.prefab` переехал в `GamePlay/Prefabs/Boards/Cell.prefab` (не `Boards/Options/`).
- Карты: `GamePlay/Prefabs/Cards/Card_Local.prefab`, `Card_Remote.prefab`, `DeckCard.prefab`.
- `StashCard` — `GamePlay/Prefabs/StashCard.prefab`.
- `AvatarMovesView` / `AvatarTurnPointView.cs` удалены. `PlayerTurnPoint.prefab` остался, но тип `AvatarTurnPointView` больше не компилируется. Opt-in в группу GamePlay с пустым `componentType` (GameObject), иначе `LoadGroup` упадёт на missing script и сломает всю группу.
- `GamePrefabs` больше не содержит AvatarTurnPoint в C# (в `.asset` ещё есть мёртвые поля).
- Потребители `GamePrefabs`: `CardFactory`, `DeckView`, `StashView`, `GamePlayServicesExtensions`. `PvP` и `Single` оба идут через `AddDefaultGamePlayServices`.

### [2026-08-21 17:07] PrefabImporter internal

В Unity 6000 `PrefabImporter` inaccessible (`CS0122`). Inspector берёт `AssetImporter` (покрывает PrefabImporter) и root `GameObject` prefab asset (`IsPersistent` + `IsPartOfPrefabAsset` + `parent == null`).

### [2026-08-21 17:10] Generator

`InitializeOnLoadMethod` delayCall написал `Prefabs.Catalog.cs` + `GamePlayPrefabs.cs` + `Groups/GamePlay.asset`. Address = `b0a1aef60c0591454860b3b7d718dc15`. Второй generate не переписал C# (нет второго лога `PrefabsCatalogClassGenerator] Generated`).

### [2026-08-21 17:13] Миграция

`GamePrefabs.cs` / `.asset` удалены. `LoadPrefabGroup(Prefabs.GamePlay)` в `GamePlayServicesExtensions` и `PvPScopeExtensions`. `BoardConstructor.Build` → `Prefabs.GamePlay.Cell`. Editor Preview грузит группу через `AssetDatabase`, если catalog не loaded. Play mode vs-bot не прогоняли: MCP bridge не подключён к живому Editor.

### [2026-08-22] Regenerate on prefab delete

Generate шёл только с domain reload / меню / чекбокса инспектора. Удаление `.prefab` не вызывало скан. Добавлен `PrefabCatalogPostprocessor` (delete/move любого `.prefab`, import только `included`). Пустая группа чистит C# в consumer-папке (`Generated/PrefabCatalog`), не только Tools.Runtime. `isCompiling` больше не глотает отложенный generate.

### [2026-08-21 21:34] Typed Root

Call site больше не делает `.As<T>()`. Генератор пишет группу в сборку Root-типа (`Assets/GamePlay/Generated/PrefabCatalog/`). `Prefabs.GamePlay.CardLocal` имеет тип `CardScopeEntity`. Addressables остаются в `PrefabGroup.LoadAsset`, чтобы GamePlay не ссылался на ResourceManager. GamePlay.dll скопирован после правки.
