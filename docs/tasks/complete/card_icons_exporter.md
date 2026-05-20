## card_icons_exporter

### Что сделано
- Создан редакторский инструмент `CardIconsExporter` (`Tools/Editor/CardIconsExporter.cs`)
- Читает `Resources/cards-info.json`, находит иконки карт в `.png`, `.psd`, `.aseprite`
- Загружает через `AssetDatabase` с fallback на `Sprite`/`LoadAllAssetsAtPath`
- Экспортирует только `sprite.rect` через `RenderTexture` + `ReadPixels`
- Сохраняет в `docs/obsidian/game/cards/icons/{type}.png` с перезаписью
- Кнопка "Export Card Icons" добавлена в Project Tools Window

### Ключевые файлы
- `client/Assets/Tools/Editor/CardIconsExporter.cs` — логика экспорта
- `client/Assets/Tools/Editor/ProjectTools/ProjectToolsWindow.cs` — UI кнопка

### Заметки
- `Application.dataPath` = `Assets/`, для корня репо нужен `GetParent` дважды
- `.aseprite` файлы импортируются как sub-assets, `LoadAssetAtPath<Texture2D>` возвращает null
- Unity текстуры `isReadable = 0`, `EncodeToPNG` требует копирования через `RenderTexture`
- `.psd` и `.aseprite` могут быть атласами — загружать `Sprite`, не `Texture2D`, и читать `sprite.rect`
