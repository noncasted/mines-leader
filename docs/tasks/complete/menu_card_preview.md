## Menu card preview

### Что сделано
- Hover над карточкой в `MenuDecks` показывает popup с RenderTexture реальной доски `Menu_Board`; эффект карты крутится по циклу.
- Backend генерирует `CardPreviewBundle` (layout + `ICardActionData[]` + snapshot) для field-modifying карт и отдаёт как `InitialCardPreviews`.
- Клиент гоняет полный боевой `ICardActionSync` через menu-стабы и `MenuCardActionSyncRegistry`.
- После миграции на Canvas: `UIElementPointerHandler` на `Menu_Card`, popup под `Canvas/Decks`, сцена `Menu_Board` снова грузится в menu scope.

### Ключевые файлы
- `backend/Game/GamePlay/CardPreviews/{CardPreviewGenerator,CardPreviewScenarios,PreviewPlayer}.cs`
- `client/Assets/Menu/Decks/Preview/MenuCardPreviewPlayer.cs`
- `client/Assets/Menu/Decks/Preview/MenuCardPreviewPopup.cs`
- `client/Assets/Menu/Decks/MenuDecks.cs`

### Заметки
- `PreviewPlayer` — fail-fast stub: карты, трогающие Mana/Hand/Deck/Stash/Moves/Actions, падают при генерации.
- Все preview-доски используют `Guid.Empty` как OwnerId.
- `_Max`-варианты auto-expand из `_Normal` сценариев; фильтруются при `AddCardActionSync`, чтобы не было VContainer conflict.
