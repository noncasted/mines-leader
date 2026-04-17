## Menu UI Toolkit Migration

### Что сделано
- Меню переведено с uGUI на UI Toolkit: BottomBar (Navigation + Play + Chat), Settings overlay, Decks (Cards) и Progression экраны целиком перерисованы.
- Новый C# bridge `Global/UI/Toolkit/` (UIToolkitExtensions, NavButton) — `ListenClick`/`BindText`/`Show`/`Hide`, Button-subclass с 3-слойной подложкой через runtime Texture2D.
- Единая инфраструктура ассетов: `Menu/UI/MenuPanelSettings.asset`, `UnityDefaultRuntimeTheme.tss`, `Styles/MenuTheme.uss` — 512×288 art-native, DreiFraktur через `resource()`, тёмно-фиолетовая палитра.
- Drag-and-drop карт в deck editor — `CardDragManipulator` (PointerManipulator), замена uGUI IBeginDragHandler.
- Settings — plain C# класс без MonoBehaviour, overlay в `bottom-bar-root`, секции Audio/Effects/Video с NavButton Cancel/Apply.

### Ключевые файлы
- `client/Assets/Global/UI/Toolkit/UIToolkitExtensions.cs`, `NavButton.cs` — reusable bridge
- `client/Assets/Menu/UI/MenuPanelSettings.asset`, `UnityDefaultRuntimeTheme.tss`, `Styles/MenuTheme.uss`
- `client/Assets/Menu/UI/Main/MenuBottomBar.uxml`/`.uss` — navigation + play popup (вне bottom-bar чтобы не обрезать overflow)
- `client/Assets/Menu/Decks/MenuDecks.cs` + `CardDragManipulator.cs` + `MenuCards.uxml`
- `client/Assets/Global/Settings/SettingsView.cs` — overlay-паттерн для модальных окон

### Заметки
- Задача частично не завершена: Фазы 0.4 (UIStateMachine под VisualElement), 5 (Chat standalone), 6 (стабы Cards/Shop) — не в scope. Migrations для Progression и Decks сделаны как separate commits (a5a8101c, 000d08ed).
- Кастомная кнопка `NavButton extends Button` — единственный рабочий способ получить hover/active на всей площади (VisualElement + Clickable и Button-с-детьми дают hover только на тексте).
- `transition` shorthand и `url()` для шрифтов — USS-парсер Unity 6.3 отклоняет. Шрифт только через `resource('FontName')` в TSS + `-unity-font-definition: initial`.
- `overflow: hidden` на bottom-bar обрезает `position: absolute; bottom: 100%` popup. Решение — выносить popup в sibling-контейнер `bottom-bar-root`.
- `SceneServicesFactory._services` — серилазированный массив, новый MonoBehaviour в сцене не подхватится пока не вызвать `OnReload()` или не добавить fileID вручную в YAML.
