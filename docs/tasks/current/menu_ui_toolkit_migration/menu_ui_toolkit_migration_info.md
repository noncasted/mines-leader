## Задача: Перенос меню UI на UI Toolkit

### Цель
Перенести текущий UI меню (uGUI Canvas + TextMeshPro + кастомный DesignButton) на UI Toolkit (USS/UXML + C# контроллеры). Сохранить пиксельный арт-стиль, drag-and-drop механику деки, систему навигации через state machine, и все интеграции с backend projections.

### Контекст
- Проект использует Unity 6.3 LTS + URP — поддерживаются UI Shader Graph и Custom Filters (blur, glow и т.д.)
- Текущий UI полностью на uGUI (Canvas, RectTransform, Button, Image, TMP_Text)
- Кастомная система: DesignButton (visual states), UIStateMachine (screen transitions), Lifetime (subscriptions)
- Пиксельный стиль: pixel font, pixel art иконки, пиксельные фоны
- Drag-and-drop для карт в deck editor
- 33 скрипта меню, ~6 экранов (Decks, Progression, Play, Cards, Settings, Shop)
- Screens Cards, Settings, Shop — стабы (ещё не реализованы)

### Возможности Unity 6.3 UI Toolkit (changelog analysis)

**Встроенные фильтры (6000.3.0):**
- USS свойства: `blur`, `tint`, `opacity`, `invert`, `grayscale`, `sepia`, `contrast`, `hue-rotate`
- Применяются к VisualElement и каскадируют на детей
- Кастомные фильтры через `FilterFunctionDefinition` asset + Material
- Использование: blur фона при модальных окнах (лутбокс), tint для disabled карт

**USS aspect-ratio (6000.3.0):**
- Свойство `aspect-ratio` для фиксированных пропорций — идеально для карт в deck editor
- Не нужны хаки с padding-bottom или фиксированными размерами

**UsageHints.LargePixelCoverage (6000.3.0):**
- Специальный GPU-оптимизированный шейдер для больших VisualElement
- Применить на фон меню и полноэкранные панели

**Image element в UI Builder (6000.3.0):**
- Image теперь полноценный UXML элемент с атрибутами
- Для иконок карт, маны, аватаров — удобнее чем через background-image

**RenderTexture → StyleBackground (6000.0.60):**
- Конвертер для динамического рендера в фон UI элемента
- Потенциально для live preview эффектов карт

**World Space UI (6000.2.0):**
- World-space input для UI Toolkit + uGUI interop
- Для chat bubble над 3D персонажами в лобби — можно перевести на UI Toolkit overlay
- `UIDocument sortingOrder` игнорируется в world-space (учесть)

**ScrollView.ScrollTo deferred (6000.3.0):**
- Автоматически откладывает scroll при dirty layout
- Не будет глючить при динамическом добавлении карт в pool

**ListView performance fixes (6000.2.0-6000.3.0):**
- Множество фиксов для AddItem/TryRemoveItem
- Memory leak fix при remove/add элементов
- Безопасно использовать для динамических списков карт

**PointerMoveEvent.deltaPosition fix (6000.0.28):**
- Исправлен inverted Y для touch input
- Критично для drag-and-drop карт — deltaPosition теперь корректный

**USS parser upgrade (6000.3.0):**
- Строже валидация — невалидный CSS вызовет ошибки
- Писать USS аккуратно, тестировать рано

**UI Shader Graph (6000.3.0, только URP):**
- Кастомные шейдеры на UI элементах через Shader Graph
- Шейдер каскадирует на дочерние элементы
- Hand-written .shader файлы официально НЕ поддерживаются для UI элементов
- Для custom filters (post-processing) — Material с любым шейдером принимается

### Шаги реализации

**Фаза 0. Инфраструктура UI Toolkit**

0.1. Создать базовую USS тему для пиксельного стиля — `client/Assets/Menu/UI/Styles/menu-theme.uss`
  - Pixel font подключение (`-unity-font` / `-unity-font-definition`)
  - Базовые цвета (тёмно-синий фон, фиолетовые панели из текущих скриншотов)
  - Общие классы: `.card`, `.button`, `.panel`, `.tab`
  - Использовать `aspect-ratio` для карт (фиксированные пропорции)
  - Фильтры: `.disabled { filter: grayscale(100%) opacity(0.5); }` для unowned карт
  - `.modal-backdrop { filter: blur(4px); }` для фона модальных окон

0.2. Создать Panel Settings asset — настройки pixel-perfect рендеринга
  - Scale Mode: Integer для pixel-perfect
  - Reference resolution matching текущему Canvas

0.3. Создать базовый C# bridge между Lifetime/ViewableProperty и UI Toolkit
  - Extension методы: `VisualElement.BindTo(ViewableProperty<T>, lifetime, action)`
  - Extension для кнопок: `Button.ListenClick(lifetime, action)` аналог текущего DesignButton
  - Extension для фильтров: `VisualElement.SetFilter(lifetime, ...)` для анимированных эффектов
  - Это критичная инфраструктура — на ней строятся все экраны

0.4. Адаптировать UIStateMachine для работы с UI Toolkit элементами
  - IUIState реализации должны уметь показывать/скрывать VisualElement вместо GameObject
  - Решить: IUIState получает VisualElement root, или оборачивает UXML Document?

0.5. Применить `UsageHints.LargePixelCoverage` на корневые фоновые элементы
  - Фон меню, полноэкранные панели — GPU-оптимизированный рендеринг

**Фаза 1. Нижняя навигация (самый простой экран)**

1.1. Создать UXML layout — `Menu_Navigation.uxml`
  - 4 кнопки: progression, cards, chat input, play
  - Горизонтальный flex layout

1.2. Создать USS стили — `Menu_Navigation.uss`
  - Пиксельные фоны через background-image
  - Hover/pressed состояния через :hover/:active псевдоклассы (замена DesignButton states)

1.3. Переписать `MenuNavigation.cs`
  - Заменить `[SerializeField] DesignButton` на `Q<Button>()` запросы к UXML
  - Сохранить [Inject] зависимости и state machine интеграцию
  - Использовать новый bridge для `ListenClick(lifetime, ...)`

**Фаза 2. Play/Matchmaking экран**

2.1. Создать UXML layout — `Menu_Play.uxml`
  - Кнопка Play с таймером
  - Панель выбора режима (TimeLimited / LastManStanding)

2.2. Создать USS стили — `Menu_Play.uss`

2.3. Переписать `MenuPlay.cs`
  - `TMP_Text _timer` → `Label` через Q<Label>()
  - `DesignButton _button` → `Button` через Q<Button>()
  - `GameObject _modeSelection` → `VisualElement.style.display`
  - Сохранить async flow с UniTask и lifetime management

**Фаза 3. Deck Editor (самый сложный экран)**

3.1. Создать UXML layout — `Menu_Decks.uxml`
  - Верхняя панель: deck index buttons + average mana
  - Средняя зона: deck card slots (5 слотов)
  - Нижняя зона: card pool grid

3.2. Создать USS стили — `Menu_Decks.uss`
  - Grid layout для card pool (замена LayoutRebuilder — UI Toolkit flex/grid справится автоматически)
  - Card стили: `aspect-ratio: 3/4;` для фиксированных пропорций карт
  - Unowned карты: `filter: grayscale(100%) opacity(0.5);` вместо CanvasGroup.alpha

3.3. Создать custom VisualElement для карты — `MenuCardElement.cs`
  - Отображает: Image (иконка), Label (имя, мана, описание)
  - Owned/unowned через USS класс `.unowned` с фильтром grayscale+opacity

3.4. Реализовать drag-and-drop через UI Toolkit Manipulators
  - Создать `CardDragManipulator` — замена `IBeginDragHandler/IDragHandler/IEndDragHandler`
  - UI Toolkit drag: `PointerDownEvent` → clone element → `PointerMoveEvent` → `PointerUpEvent` → drop
  - Drop target detection через `panel.Pick()` или пользовательскую логику

3.5. Переписать `MenuDecks.cs`
  - Заменить Instantiate(prefab, parent) на programmatic VisualElement creation
  - `_deckRoot`, `_poolRoot`, `_indexRoot` → Q<VisualElement>() контейнеры
  - Сохранить IDeckService, ICardsRegistry, ICardConfigs интеграцию

3.6. Заменить `MenuDeckCard.cs`, `MenuDeckPoolCard.cs`, `MenuDeckPoolSpot.cs`, `MenuDeckIndexButton.cs`
  - Каждый становится либо custom VisualElement, либо controller привязанный к UXML элементу

**Фаза 4. Progression экран**

4.1. Создать UXML layout — `Menu_Progression.uxml`
  - Progress bar (fill element с процентной шириной)
  - Timeline milestones (горизонтальный flex)
  - XP display

4.2. Создать USS стили — `Menu_Progression.uss`
  - Milestone состояния через USS классы: .locked, .reached, .available, .claimed

4.3. Переписать `MenuProgression.cs` и `ProgressionMilestone.cs`
  - Progress bar fill → `style.width = Length.Percent(progress)`
  - Milestone state → `AddToClassList("available")` / `RemoveFromClassList("locked")`
  - LootBoxChoicePanel → модальный VisualElement overlay вместо динамического GameObject
  - Фон при открытии лутбокса: `filter: blur(4px);` на основном контенте за модалкой

**Фаза 5. Chat UI**

5.1. Создать UXML layout для chat input — `Menu_Chat.uxml`
  - TextField элемент (замена TMP_InputField)

5.2. Переписать `MenuChatUI.cs`
  - `TMP_InputField` → `TextField` с RegisterCallback<KeyDownEvent>
  - Сохранить IViewableDelegate<string> MessageSend

**Фаза 6. Стабовые экраны (Cards, Settings, Shop)**

6.1. Создать минимальные UXML layouts для каждого
6.2. Реализовать при необходимости (сейчас они стабы)

**Фаза 7. Интеграция и очистка**

7.1. Заменить Menu.unity scene hierarchy
  - Убрать Canvas, добавить UIDocument компонент
  - Или: UIDocument на корневом GameObject, UXML загружает всё дерево

7.2. Удалить старые uGUI компоненты и префабы
  - Menu_Card.prefab, Menu_Deck_Index.prefab, Menu_PoolSpot.prefab и т.д.

7.3. Удалить или адаптировать DesignButton / DesignElement систему
  - Если используется только в меню — удалить
  - Если используется в gameplay UI — оставить, но для меню не использовать

7.4. Проверить pixel-perfect рендеринг на разных разрешениях

### Ключевые файлы

| Файл | Роль в задаче |
|------|---------------|
| `client/Assets/Menu/Decks/MenuDecks.cs` | Главный контроллер deck editor — полная переработка |
| `client/Assets/Menu/Decks/MenuDeckCard.cs` | Слот деки — замена на VisualElement |
| `client/Assets/Menu/Decks/MenuDeckPoolCard.cs` | Draggable карта — замена drag-drop на Manipulator |
| `client/Assets/Menu/Decks/MenuDeckPoolSpot.cs` | Контейнер пула — замена на VisualElement |
| `client/Assets/Menu/Decks/MenuDeckIndexButton.cs` | Кнопка выбора деки — замена на Button |
| `client/Assets/Menu/Main/Navigation/MenuNavigation.cs` | Навигация — переработка на UI Toolkit |
| `client/Assets/Menu/Main/Play/MenuPlay.cs` | Matchmaking — переработка UI части |
| `client/Assets/Menu/Screens/Progression/MenuProgression.cs` | Progression — переработка на VisualElement |
| `client/Assets/Menu/Screens/Progression/ProgressionMilestone.cs` | Milestone — замена на USS классы |
| `client/Assets/Menu/Social/Chat/UI/MenuChatUI.cs` | Chat input — TextField замена |
| `client/Assets/Global/UI/Design/Buttons/DesignButton.cs` | Кастомная кнопка — возможно удаление |
| `client/Assets/Global/UI/StateMachines/` | State machine — адаптация под VisualElement |
| `client/Assets/Menu/Common/Options/Menu.unity` | Главная сцена меню — реструктуризация hierarchy |

### Документация к прочтению
- `rules/MONOBEHAVIOUR.md` — MenuDecks и другие реализуют ISceneService + IScopeSetup
- `rules/LIFETIMES.md` — все подписки через Lifetime, нужен bridge для UI Toolkit
- `rules/REACTIVE.md` — ViewableProperty/EventSource используются повсюду
- `rules/CODE_STYLE.md` — member order, naming для новых файлов

### Риски

1. **Drag-and-drop** — самая сложная часть. UI Toolkit Manipulators работают иначе чем uGUI IBeginDragHandler. Нужен прототип до полной миграции deck editor. Плюс: `PointerMoveEvent.deltaPosition` inverted Y баг исправлен в 6000.0.28 — на 6.3.8 уже ok.

2. **Pixel-perfect рендеринг** — UI Toolkit рендерит через mesh, пиксели могут "плыть" при нецелых масштабах. Нужно протестировать с Panel Settings Integer scale mode рано.

3. **MonoBehaviour + UIDocument** — текущие контроллеры — MonoBehaviour с [SerializeField]. При переходе на UI Toolkit поля заменяются на Q<>() запросы. Нужно решить: контроллеры остаются MonoBehaviour (с UIDocument reference) или становятся чистыми C# классами?

4. **DesignButton в gameplay** — если DesignButton используется не только в меню, его нельзя удалять. Нужно проверить usage.

5. **Social/Players** — 3D персонажи в лобби (MenuPlayer*) рендерятся не через UI. Они остаются как есть, но chat bubble может мигрировать на UI Toolkit world-space overlay (поддержка world-space input есть с 6000.2.0).

6. **Порядок миграции** — начинать с навигации (самый простой), потом play, потом progression, deck editor последним. Это минимизирует риск — каждый шаг можно протестировать отдельно.

7. **USS parser строже в 6.3** — невалидный CSS вызовет ошибки. Тестировать USS рано, не копить стили.

8. **Версия Unity** — текущая 6.3.8. Все нужные фичи (фильтры, aspect-ratio, LargePixelCoverage, Image element, world-space) доступны с 6000.3.0. Обновление до 6.4 не требуется для миграции (см. секцию "Версия Unity").

### Версия Unity: 6.3.8 vs 6.4 — надо ли обновляться?

**Текущая: 6.3.8 (6000.3.8f1)**

**Что появится при обновлении до 6.4.x:**

| Фича / фикс | Версия | Нужно нам? |
|-------------|--------|------------|
| Создание UIDocument из Hierarchy context menu с авто-созданием UXML | 6000.4.0 | Удобство, не критично |
| Drag & drop UXML/USS из Project window в UI Builder | 6000.4.0 | Удобство, не критично |
| Кнопка "Edit" в UIDocument Inspector | 6000.4.0 | Удобство |
| Read-only ToggleButtonGroup, TabView, Tabs | 6000.4.0 | Нет |
| `resource(path#sub-asset-name)` для StyleSheet | 6000.4.0 | Потенциально полезно |
| **Удалён InputSystemEventSystem** | 6000.4.0 | Осторожно — проверить, не используем ли |
| **Удалён InputWrapper** | 6000.4.0 | Осторожно — проверить, не используем ли |
| tight-mesh sprite DynamicColor fix | 6000.3.13 | Нет (pixel art, не tight-mesh) |
| VectorImages uniform color fix | 6000.3.11 | Нет (не используем VectorImages) |
| SVG importer fixes | 6000.3.8-12 | Нет (не используем SVG) |
| simulate.Click для world-space в Test Framework | 6000.3.12 | Потенциально для тестов |

**Вывод: обновляться НЕ нужно.**

Между 6.3.8 и 6.4.2 для UI Toolkit:
- Нет новых фич, критичных для миграции — только QoL улучшения для UI Builder
- **Риск:** в 6.4.0 удалены `InputSystemEventSystem` и `InputWrapper` — это breaking change, может потребовать адаптации input handling
- Все баги ListView/ScrollView/drag уже пофикшены в 6.3.x

Рекомендация: оставаться на **6.3.8**, начать миграцию. Обновляться до 6.4 можно позже, если понадобится `resource(path#sub-asset)` или QoL фичи UI Builder.
