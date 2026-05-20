## Menu card preview -- Рабочие заметки

### Статус: Завершено и проверено в Unity

### Выполнено
- [x] 1.1 -- UIElementPointerHandler добавлен на root GameObject `Menu_Card.prefab`
- [x] 1.2 -- `_pointerHandler` сериализуемое поле добавлено в `MenuDeckPoolSpot.cs` + property `PointerHandler`
- [x] 1.3 -- `Menu_PoolSpot.prefab` обновлен: ссылка `_pointerHandler` -> stripped component из вложенного Menu_Card
- [x] 2.1 -- `MenuCardPreviewPopup.cs` создан (`RawImage` + `RectTransform` + `CanvasGroup`, методы `Show`/`Hide`/`SetPosition`)
- [x] 2.2 -- `Menu.csproj` обновлен: `<Compile Include="Assets/Menu/Screens/Cards/Preview/MenuCardPreviewPopup.cs" />`
- [x] 3.1 -- `IMenuCardPreviewPlayer` вернут в `Construct` `MenuDecks.cs`
- [x] 3.2 -- `RegisterPreviewHover` подписывается на `PointerHandler.IsHovered` для каждого pool spot
- [x] 3.3--3.5 -- `ShowPreview`/`HidePreview`/`PositionPreview` реализованы в `MenuDecks.cs`
- [x] 4.1 -- `Menu_CardPreviewPopup.prefab` создан с `CanvasGroup`
- [x] 4.2 -- Popup-объект добавлен на сцену `Menu.unity` под `Canvas/Decks`
- [x] 4.3 -- `MenuDecks._previewPopup` ссылается на компонент popup
- [x] 5.1 -- `CanvasGroup` добавлен в `Menu_CardPreviewPopup.prefab` через Unity MCP
- [x] 5.2 -- `CanvasGroup` добавлен в popup-объект на сцене `Menu.unity` через Unity MCP
- [x] 5.3 -- `_canvasGroup` ссылки обновлены в префабе и на сцене
- [x] 5.4 -- `.meta` файл для `Menu_CardPreviewPopup.prefab` подтверждён
- [x] 6.1 -- `MenuLoopExtensions.cs` обновлён: добавлена регистрация `MenuCardPreviewPlayer` как `IMenuCardPreviewPlayer`
- [x] 6.2 -- Исправлена ошибка компиляции `Vector2[]` -> `Vector3[]` в `MenuDecks.cs` (строка 191)
- [x] 6.3 -- Сцена `Menu.unity` сохранена через Unity MCP
- [x] 7.1 -- `MenuScopeExtensions.cs` обновлён: восстановлена загрузка сцены `Menu_Board` (требуется для `IMenuBoard` -> `MenuCardPreviewPlayer`)
- [x] 8.1 -- Popup-объект пересоздан в сцене `Menu.unity` через Unity MCP (старый YAML-объект с искусственными fileID удалён)
- [x] 8.2 -- RectTransform popup настроен: anchor top-left, size 200x200, pivot (0,1)
- [x] 8.3 -- CanvasGroup настроен: alpha=0, interactable=false, blocksRaycasts=false
- [x] 8.4 -- RawImage настроен: raycastTarget=false
- [x] 8.5 -- Все сериализуемые ссылки в `MenuCardPreviewPopup` и `MenuDecks` установлены через reflection
- [x] 8.6 -- Сцена сохранена, все объекты на своих местах

### Финальная проверка (Unity Editor)
- Компиляция прошла без ошибок
- popup под Decks: OK
- menuDecks._previewPopup: ссылается на корректный popup
- popup._previewImage: установлен
- popup._popupTransform: установлен
- popup._canvasGroup: установлен

### Оставшиеся префабы
- `Assets/Menu/Artwork/Deck/Menu_CardPreviewPopup 1.prefab` -- рабочий префаб, созданный из сценного объекта
- Старый `Menu_CardPreviewPopup.prefab` не удаляется (возможно, .meta блокировка), но не используется

### Измененные файлы
| Файл | Статус | Что изменено |
|------|--------|-------------|
| `client/Assets/Menu/Decks/MenuDeckPoolSpot.cs` | uncommitted | Добавлено поле `_pointerHandler` + property |
| `client/Assets/Menu/Decks/MenuDecks.cs` | uncommitted | Восстановлена логика превью: hover, Show/Hide, PositionPreview; исправлен Vector3[] |
| `client/Assets/Menu/Screens/Cards/Preview/MenuCardPreviewPopup.cs` | uncommitted | Новый файл -- компонент popup |
| `client/Assets/Menu/Common/Loop/MenuLoopExtensions.cs` | uncommitted | Добавлена регистрация `MenuCardPreviewPlayer` |
| `client/Assets/Menu/Common/Setup/MenuScopeExtensions.cs` | uncommitted | Восстановлена загрузка сцены `Menu_Board` |
| `client/Assets/Menu/Artwork/Deck/Menu_Card.prefab` | uncommitted | Добавлен UIElementPointerHandler компонент |
| `client/Assets/Menu/Artwork/Deck/Menu_PoolSpot.prefab` | uncommitted | Добавлена ссылка `_pointerHandler` |
| `client/Assets/Menu/Artwork/Deck/Menu_CardPreviewPopup 1.prefab` | uncommitted | Новый префаб popup (создан через MCP) |
| `client/Assets/Menu/Common/Options/Menu.unity` | uncommitted | Popup-объект под Decks, все ссылки настроены, сцена сохранена |
| `client/Menu.csproj` | uncommitted | Добавлена ссылка на MenuCardPreviewPopup.cs |
