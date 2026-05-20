## Задача: Восстановление превью карт в меню (Canvas/Decks)

### Цель
Восстановить функциональность превью карт при наведении в экране колод (Menu/Decks). Ранее работало на UI Toolkit, после миграции на Canvas-префабы превью перестало работать.

Требования пользователя:
1. На `Menu_Card.prefab` добавить `UIElementPointerHandler` и добавить его в `MenuDeckPoolSpot` как сериализуемое поле.
2. Слушать ивент `IsHovered` хендлера и показывать рядом с картой превью.
3. Создать префаб всплывающего окна с Render Texture поля.
4. Добавить этот префаб на сцену `Menu` под `Canvas/Decks`.
5. Сделать отображение превью при наведении.

### Контекст
- Превью генерируется бэкендом как `CardPreviewBundle` и прилетает через `InitialCardPreviews` projection.
- Система превью уже реализована: `IMenuCardPreviewPlayer`, `MenuCardPreviewPlayer`, `MenuBoard`, `MenuCardPreviewCache`.
- `MenuBoard` находится в отдельной сцене `Menu_Board.unity` и рендерит в `MenuBoardPreview.renderTexture` (512x512).
- Старая реализация использовала UI Toolkit: `RegisterPreviewHover` + `ShowPreview`/`HidePreview` в `MenuDecks`.
- Новая реализация должна работать с Canvas-префабами (`Menu_Card.prefab`, `Menu_PoolSpot.prefab`).

### Шаги реализации

**1. Подготовка префаба карточки**
  1.1. Добавить `UIElementPointerHandler` компонент на root GameObject в `Menu_Card.prefab`.
  1.2. Добавить `[SerializeField] private UIElementPointerHandler _pointerHandler` в `MenuDeckPoolSpot.cs`.
  1.3. Обновить `Menu_PoolSpot.prefab` — настроить ссылку `_pointerHandler` на компонент из вложенного `Menu_Card`.

**2. Компонент всплывающего окна превью**
  2.1. Создать `MenuCardPreviewPopup.cs` — MonoBehaviour с `RawImage` для `RenderTexture` и методами `Show`/`Hide`.
  2.2. Добавить файл в `Menu.csproj`.

**3. Интеграция hover + превью в MenuDecks**
  3.1. Вернуть `IMenuCardPreviewPlayer` в `Construct` `MenuDecks.cs`.
  3.2. В `OnInitialized` подписаться на `IsHovered` каждого `MenuDeckPoolSpot.PointerHandler`.
  3.3. При hover: вызвать `_previewPlayer.Play(type)`, показать popup с `PreviewTexture`.
  3.4. При leave: вызвать `_previewPlayer.Stop()`, скрыть popup.
  3.5. Позиционировать popup рядом с картой (overflow-safe).

**4. Префаб и сцена popup**
  4.1. Создать префаб `Menu_CardPreviewPopup.prefab` с `RawImage` + `RectTransform`.
  4.2. Добавить экземпляр префаба на сцену `Menu.unity` под `Canvas/Decks`.
  4.3. Настроить `MenuDecks._previewPopup` ссылку на экземпляр.

### Ключевые файлы

| Файл | Роль в задаче |
|------|---------------|
| `client/Assets/Menu/Decks/MenuDeckPoolSpot.cs` | Добавить сериализуемое поле `_pointerHandler` |
| `client/Assets/Menu/Decks/MenuDecks.cs` | Hover-подписки, показ/скрытие popup, позиционирование |
| `client/Assets/Menu/Screens/Cards/Preview/MenuCardPreviewPopup.cs` | [новый файл — добавить в Menu.csproj] Логика popup |
| `client/Assets/Menu/Artwork/Deck/Menu_Card.prefab` | Добавить UIElementPointerHandler компонент |
| `client/Assets/Menu/Artwork/Deck/Menu_PoolSpot.prefab` | Обновить ссылку `_pointerHandler` в MenuDeckPoolSpot |
| `client/Assets/Menu/Common/Options/Menu.unity` | Добавить popup-объект на сцену |

### Документация к прочтению
- `.agents/docs/COMMON_LIFETIMES.md` — Lifetime, Advise, подписки и cleanup
- `.agents/docs/COMMON_REACTIVE_BASICS.md` — IViewableProperty, EventSource
- `.agents/docs/COMMON_CONTAINER.md` — VContainer DI, ISceneService, IScopeSetup
- `.agents/docs/UI_MENU.md` — Menu UI Toolkit / Canvas паттерны

### Риски
- `Menu_PoolSpot.prefab` содержит `Menu_Card.prefab` как вложенный префаб (PrefabInstance). Изменения в `Menu_Card.prefab` должны корректно прокидываться через `stripped` references.
- Popup должен иметь правильный `Canvas`/`RectTransform` и слой для рендера поверх остальных элементов.
- Позиционирование popup должно учитывать границы экрана (overflow-safe).
