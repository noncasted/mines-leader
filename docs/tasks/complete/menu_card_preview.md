## Menu card preview

### Что сделано

- Hover над карточкой в `MenuDecks` показывает popup с RenderTexture-рендером реальной доски `Menu_Board`, на которой циклически проигрывается эффект карты.
- Backend на старте генерирует `CardPreviewBundle` (initial layout + `ICardActionData[]` + final snapshot) для 22 field-modifying карт через `CardPreviewGenerator` + `BoardLayoutParser` (DSL `t m _ x f g`). Раздаёт клиентам как `InitialCardPreviews` projection при connect.
- Клиент переиспользует **полный** боевой `ICardActionSync` pipeline через menu-стабы (`MenuPreviewGameContext/Player/Camera`, `MenuPreviewVfxFactory`) + reflection-based `MenuCardActionSyncRegistry` — все card-specific эффекты (ZipZap lightning, Smoke, Blackout, Frost, ThermalVision) работают автоматически без дублирования кода.
- Переключение превью: terminate lifetime + full cleanup (cell effects, reticles, VFX) безусловно в `Play`/`Stop`, `RunSerialAsync` ждёт unwind старого task перед запуском нового цикла.

### Ключевые файлы

- `shared/Protocol/CardPreviewBundle.cs` — DTO
- `backend/Game/GamePlay/Boards/BoardLayoutParser.cs` — DSL parser + `Capture(IBoard)`
- `backend/Game/GamePlay/CardPreviews/{CardPreviewGenerator,CardPreviewScenarios,PreviewPlayer}.cs` — генерация бандлов на старте
- `client/Assets/Menu/Screens/Cards/Preview/MenuBoard.cs` — сцена-сервис с доской и RT
- `client/Assets/Menu/Screens/Cards/Preview/MenuCardPreviewPlayer.cs` — цикл проигрывания
- `client/Assets/Menu/Screens/Cards/Preview/Sync/MenuCardActionSyncRegistry.cs` — reflection-диспатч action → sync
- `client/Assets/Menu/Decks/MenuDecks.cs` — hover handlers + popup

### Заметки

- `PreviewPlayer` — fail-fast stub: любая карта, трогающая Mana/Hand/Deck/Stash/Moves/Actions, падает с `NotSupportedException` при генерации. Добавляя новую карту в `CardPreviewScenarios`, если она читает эти свойства — расширить stub или убрать карту из сценариев.
- Все preview-доски используют `Guid.Empty` как OwnerId; `MenuPreviewGameContext.GetPlayer(Guid.Empty)` всегда возвращает единственный stub-игрок.
- `_Max`-варианты карт auto-expand через `Enum.TryParse` из `_Normal` сценариев; фильтруются в `MenuLoopExtensions` при регистрации `AddCardActionSync` чтобы избежать VContainer conflict.
- `UserProjectionSourceGenerator` эмитит `GeneratedUserProjections` в каждый проект с analyzer — при добавлении ProjectReference на Game в MetaGateway пришлось подключить Generators.csproj тоже (иначе CS0433). TODO: `internal` / per-assembly namespace.
- `AddCardActionSync` / `AddCardActionSyncResolver` теперь на `IBuilder` (не `IEntityBuilder`) — работает и в scope builder.
