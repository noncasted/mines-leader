## Menu UI Toolkit Migration — Рабочие заметки

### Статус: В работе (разделение сессии)

### Выполнено
- [x] Фаза 0.1 — Создана базовая USS тема `Menu/UI/Styles/MenuTheme.uss` (CSS переменные цветов + стили панели)
- [x] Фаза 0.2 — Создан PanelSettings asset `Menu/UI/MenuPanelSettings.asset` (ScaleWithScreenSize 1920x1080, match=1, sortingOrder=10)
- [x] Фаза 0.3 — Создан C# bridge `Global/UI/Toolkit/UIToolkitExtensions.cs` (ListenClick, ListenSubmit, Show/Hide, BindText)
- [x] Фаза 1.1 — Создан UXML layout `Menu/Main/MenuBottomBar.uxml`
- [x] Фаза 1.2 — Создан USS стили `Menu/Main/MenuBottomBar.uss`
- [x] Фаза 1.3 — Переписан `MenuNavigation.cs` на UI Toolkit (Q<Button>, UIDocument, ISettings)
- [x] Переписан `MenuPlay.cs` на UI Toolkit (добавлен IScopeSetup, Q<Button/Label>)
- [x] Переписан `MenuChatUI.cs` на UI Toolkit (TextField, FocusIn/Out callbacks)
- [x] Добавлен `UIToolkitExtensions.cs` и `NavButton.cs` в `Global.csproj`
- [x] Создан отдельный GameObject `BottomBar` (вне Canvas) с UIDocument + MenuNavigation + MenuPlay + MenuChatUI
- [x] Удалены старые uGUI объекты: `Canvas/Navigation` (с дочерними) и `Canvas/Play`
- [x] SceneServicesFactory._services пересканирован через OnReload()
- [x] Создан Runtime Theme `Menu/UI/UnityDefaultRuntimeTheme.tss` с импортом дефолтной темы Unity
- [x] Настроен PanelSettings: Theme Style Sheet = UnityDefaultRuntimeTheme, sortingOrder=10
- [x] Решена проблема UIDocument root height=0 через TSS: `.unity-ui-document__root { flex-grow: 1; }`
- [x] Решена проблема шрифта через TSS: `-unity-font: resource('DreiFraktur'); -unity-font-definition: initial;`
- [x] DreiFraktur.ttf скопирован в `Resources/DreiFraktur.ttf` для доступа через `resource()`
- [x] Кнопка Settings с иконкой шестерёнки (Unicode ⚙) — ISettings.Open()
- [x] Чат input — тонкая рамка (top: dark, bottom: highlight), border-radius 0
- [x] NavButton — кастомный класс (extends Button) с трёхслойной подложкой через static Texture2D
- [x] Подложка кнопок — highlight (4px top) + upper (46px) + lower (50px) через 1x100 текстуру
- [x] Hover/press через `-unity-background-image-tint-color` (текстура 1.25x ярче, normal tint 0.8)
- [x] Бар имеет ту же текстуру что и кнопки (устанавливается в MenuNavigation.OnSetup)
- [x] Разделители 4px, показывают фон бара
- [x] Стили панели `.panel` (для всплывающих окон) добавлены в MenuTheme.uss
- [x] Конвертация SettingsView на UI Toolkit — plain C# класс, overlay в bottom-bar-root
- [x] Settings: секции Audio/Effects/Video с разделителями и подписями
- [x] Settings: кнопки Cancel/Apply как NavButton (с backdrop текстурой)
- [x] Settings: обводка панели — бока/низ 2px, шапка 2px (было 4px), highlight 1px
- [x] Settings: VSync toggle — плоский (padding 2px, font-size 16px)
- [x] Фаза 3 — Cards/Deck Editor переписан на UI Toolkit
  - MenuDecks.cs: отдельный UIDocument + MenuCards.uxml, UI строится программно
  - DeckSlotElement, PoolCardElement — plain C# классы (VisualElement-based)
  - CardDragManipulator — PointerManipulator для drag-and-drop карт
  - Стили карт в MenuTheme.uss (cards-root, deck-slots, pool-card, drag-ghost)
  - Видимость через Show/Hide вместо gameObject.SetActive
- [x] Кнопка Play и выбор playmode — доведена визуально
  - Mode selection вынесен из bottom-bar (overflow: hidden обрезал popup)
  - Mode buttons теперь NavButton (вертикальный layout, 350px ширина)
  - Timer фиксированной ширины 100px, font-size 22px
  - Popup прижат к правому краю над play кнопкой
  - Play-area: play-button больше не absolute, а flex-grow: 1
- [ ] Фаза 0.4 — Адаптация UIStateMachine для UI Toolkit (не начато)
- [ ] Фаза 4 — Progression экран (следующий шаг)
- [ ] Фаза 5-7 — Остальные экраны (не начато)

### Текущий момент остановки

Play кнопка и выбор playmode полностью работают в Play Mode. Mode selection popup появляется вертикально справа над play кнопкой (350px ширина, NavButton кнопки). Timer фиксированной ширины. Следующий шаг — **Фаза 4: Progression экран**.

**Что нужно для Progression (из info.md Фаза 4):**
- Создать UXML layout `Menu_Progression.uxml` — progress bar, timeline milestones, XP display
- Создать USS стили `Menu_Progression.uss` — milestone состояния (.locked, .reached, .available, .claimed)
- Переписать `MenuProgression.cs` — progress bar fill через `style.width = Length.Percent()`, milestone state через USS классы
- Переписать `ProgressionMilestone.cs` — замена на USS классы вместо uGUI компонентов
- LootBoxChoicePanel — модальный overlay (как Settings)

**Ключевые файлы для Progression:**
- `client/Assets/Menu/Screens/Progression/MenuProgression.cs` — контроллер
- `client/Assets/Menu/Screens/Progression/ProgressionMilestone.cs` — milestone компонент
- Нужно также прочитать текущие реализации чтобы понять data flow и projections

### Важные находки

1. **USS не поддерживает linear-gradient** — нельзя задать несколько цветов на одном элементе. Решение: background-image с программно сгенерированной текстурой 1x100px.

2. **`-unity-background-image-tint-color`** — умножает цвета, может только затемнять (≤255). Для hover-осветления текстура должна быть ярче целевых цветов (1.25x), а normal tint = 0.8.

3. **Button requires `align-self: stretch`** — без этого кнопка имеет высоту по контенту (текст), а не по контейнеру. Hover работал только на узкой полоске текста.

4. **NavButton extends Button** — единственный работающий подход. VisualElement + Clickable manipulator и VisualElement с дочерней Button — оба варианта НЕ работали (hover/press только на тексте). Наследование Button гарантирует нативный hover/active на всей площади.

5. **Дочерние элементы внутри Button в UXML** — НЕ работает для hover. Элементы рендерятся, но hover по-прежнему только на тексте.

6. **Текстура в editor preview не показывается** — текстура создаётся в runtime (C# конструктор NavButton). В UI Builder editor preview кнопки без фона. Это нормально.

7. **TSS файл — критичен**. `UnityDefaultRuntimeTheme.tss` — единственное место где можно задать стили для UIDocument root (`.unity-ui-document__root`), потому что root element находится ВНЕ UXML дерева.

8. **Шрифт через TSS**: `-unity-font: resource('DreiFraktur'); -unity-font-definition: initial;` — обязательно `initial` для `-unity-font-definition`.

9. **`resource()` в USS** загружает из `Resources/`. Путь без расширения.

10. **`url()` в USS** для шрифтов — НЕ работает в Unity 6.3. Ломает весь USS файл.

11. **transition shorthand не работает в USS** — Unity USS парсер отклоняет.

12. **SceneServicesFactory** — при добавлении MonoBehaviour на сцену нужно вызвать `factory.OnReload()`.

13. **Canvas ScreenSpaceOverlay** на sort order 0 перекрывает UI Toolkit. Решение: PanelSettings.sortingOrder = 10.

14. **PanelSettings**: ScaleMode = ScaleWithScreenSize, Reference = 1920x1080, Match = 1 (height).

15. **НЕ удалять `client/Assets/Resources/`!** — содержит критичные файлы.

16. **Панель для всплывающих окон** — стили .panel в MenuTheme.uss, UXML структура:
    ```xml
    <VisualElement class="panel">
        <VisualElement class="panel-mid">
            <VisualElement class="panel-highlight" />
            <VisualElement class="panel-header-upper" />
            <VisualElement class="panel-header-lower" />
            <VisualElement class="panel-body"><!-- content --></VisualElement>
        </VisualElement>
    </VisualElement>
    ```

17. **Точные цвета UI** (предоставлены пользователем):
    - Bar upper/highlight: rgb(96, 97, 124)
    - Bar divider/upper: rgb(70, 68, 100)
    - Bar lower: rgb(58, 50, 78)
    - Chat/input bg: rgb(33, 23, 47)
    - Panel border outer: rgb(56, 49, 78)
    - Panel border mid: rgb(69, 67, 100)
    - Panel highlight: rgb(120, 118, 150)
    - Panel header upper: rgb(95, 90, 125)
    - Panel header lower: rgb(75, 70, 105)

18. **SettingsView как overlay** — plain C# класс (не MonoBehaviour) находит UIDocument через `FindFirstObjectByType`, добавляет overlay в `bottom-bar-root`. Стили из MenuTheme.uss доступны, т.к. overlay внутри UXML-дерева. Lifetime для cleanup подписок.

19. **Settings секции и NavButton кнопки** — разделители `.settings-divider` (1px highlight), подписи секций `.settings-section-label`, Cancel/Apply как NavButton с separator между ними. Шрифт кнопок 22px (28px вылезал за пределы).

20. **Panel border proportions** — `.panel` и `.panel-mid` padding: `1px 2px 2px 2px` (top тоньше, бока/низ толще). Header upper/lower: 2px (было 4px — слишком толсто).

21. **overflow: hidden на bottom-bar обрезает popup** — элементы с `position: absolute; bottom: 100%` внутри bottom-bar невидимы. Решение: вынести popup (mode-selection, timer) за пределы bottom-bar, в bottom-bar-root как отдельные siblings. Структура UXML: `play-popup` контейнер → mode-selection + timer → bottom-bar.

22. **Timer дергает размер** — пиксельный шрифт DreiFraktur имеет разную ширину для разных цифр. Решение: фиксированная ширина (width: 100px) + text-align center.

### Измененные файлы (uncommitted)

| Файл | Статус | Что изменено |
|------|--------|-------------|
| `client/Assets/Global/UI/Toolkit/NavButton.cs` | новый | Button subclass с static Texture2D backdrop (1x100, 3 цвета), public Backdrop |
| `client/Assets/Global/UI/Toolkit/UIToolkitExtensions.cs` | новый | Bridge: ListenClick(Button), ListenSubmit, Show/Hide, BindText |
| `client/Assets/Menu/Main/MenuBottomBar.uxml` | новый | UXML: NavButton элементы, chat, play-popup (mode-selection + timer вне bottom-bar) |
| `client/Assets/Menu/Main/MenuBottomBar.uss` | новый | USS: nav-button, chat-input, play-area, mode-selection (вертикальный 350px), timer (100px fixed) |
| `client/Assets/Menu/UI/Styles/MenuTheme.uss` | новый | CSS переменные + стили панели + стили Settings overlay + settings секции + cards стили |
| `client/Assets/Menu/UI/MenuPanelSettings.asset` | новый | ScaleWithScreenSize 1920x1080, sortOrder=10 |
| `client/Assets/Menu/UI/UnityDefaultRuntimeTheme.tss` | новый | Runtime Theme: default import, flex-grow root, DreiFraktur font |
| `client/Assets/Resources/DreiFraktur.ttf` | новый | Шрифт для UI Toolkit |
| `client/Assets/Global/Settings/SettingsView.cs` | rewritten | Plain C# класс: overlay с секциями Audio/Effects/Video, NavButton Cancel/Apply |
| `client/Assets/Global/Settings/SettingsExtensions.cs` | modified | Убрана инстанциация prefab, `new SettingsView()` |
| `client/Assets/Global/Settings/SettingsPrefabDefinitions.cs` | deleted | Больше не нужен (UI строится программно) |
| `client/Assets/Menu/Decks/MenuDecks.cs` | rewritten | UI Toolkit: UIDocument + UXML, программное построение карт, drag-and-drop |
| `client/Assets/Menu/Decks/CardDragManipulator.cs` | новый | PointerManipulator для drag-and-drop карт в deck slots |
| `client/Assets/Menu/Decks/MenuCards.uxml` | новый | UXML layout для Cards screen |
| `client/Assets/Menu/Decks/MenuCards.uss` | новый | USS стили (reference, основные стили в MenuTheme.uss) |
| `client/Assets/Menu/Main/Navigation/MenuNavigation.cs` | modified | UIDocument + Q<Button>(), ISettings |
| `client/Assets/Menu/Main/Play/MenuPlay.cs` | modified | Q<Button>(), .text, IScopeSetup |
| `client/Assets/Menu/Social/Chat/UI/MenuChatUI.cs` | modified | TMP_InputField -> TextField, FocusIn/Out callbacks |
| `client/Assets/Menu/Common/Options/Menu.unity` | modified | Удалены Canvas/Navigation и Canvas/Play, добавлен BottomBar GameObject |

### Что делать далее

1. **Фаза 4 — Progression экран** — UXML layout, USS стили, переписать MenuProgression.cs и ProgressionMilestone.cs
2. **Фаза 0.4 — UIStateMachine адаптация** для UI Toolkit (IUIState с VisualElement)
3. **Фаза 5-7** — Chat, стабы, интеграция и очистка
