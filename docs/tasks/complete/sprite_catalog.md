## Sprite Catalog

### Что сделано
- Art под `Assets/Art` каталогизируется как `Sprites.Cards.Trebuchet`: inspector-модуль пишет `userData`, генератор эмитит C# + `SpriteGroupAsset`.
- Runtime-сборка `Tools.SpriteBuilder`: `SpriteGroup.Retain/Release`, getters только `EnsureLoaded()` (никогда не грузят).
- Загрузка только через `builder.LoadSpriteGroup(Sprites.X)` — Retain на `AddBeforeBuild`, Release на `AddBeforeDispose`.

### Ключевые файлы
- `client/Assets/Tools/Editor/SpriteBuilder/` — inspector, generator, addressables sync
- `client/Assets/Tools/Runtime/SpriteBuilder/SpriteGroup.cs`
- `client/Assets/Tools/Runtime/SpriteBuilder/SpriteBuilderExtensions.cs`

### Заметки
- `ISpriteAnimationData` живёт в `Tools.SpriteBuilder`, иначе цикл `Internal → SpriteBuilder → Common.Animations → Internal`.
- Time/Color анимации — только в asset; смена не переписывает C#.
- Addressable unit — один `SpriteGroupAsset` на группу, address = GUID ассета.
