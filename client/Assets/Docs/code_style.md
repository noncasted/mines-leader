# Стиль кода - Storytale Workshop

Этот документ описывает основные правила и соглашения для написания кода в проекте.

---

## 0. Неймспейсы

### Правило: верхнеуровневый неймспейс определяется по структуре папок до конкретной папки

Файлы во вложенных папках **НЕ** получают дополнительные части в неймспейс. Неймспейс определяется по фиксированной структуре папок.

### Примеры

```
Структура папок:
Assets/GamePlay/Level/Edit/
├── EditWindow/
│   ├── Collision/
│   │   └── ObjectCollidersSelector.cs
│   ├── Visual/
│   │   ├── Timelines/
│   │   │   └── TimelineGrid.cs
│   │   └── Transforms/
│   │       └── ObjectEditTransformView.cs
│   └── ObjectEditVisuals.cs
├── ObjectPalette/
│   └── Entries/
│       └── ObjectPaletteEntryView.cs
└── Setup/
    └── LevelEditScene.cs

Все файлы имеют один неймспейс:
namespace GamePlay.Level.Edit
```

### Почему не добавляем подуровни?

Вложенные папки (`EditWindow/Collision`, `EditWindow/Visual/Timelines`) **НЕ добавляют** части к неймспейсу:

```csharp
// ✓ Правильно: несмотря на глубокую вложенность папок
// Файл: Assets/GamePlay/Level/Edit/EditWindow/Collision/ObjectCollidersSelector.cs
namespace GamePlay.Level.Edit
{
    public class ObjectCollidersSelector { }
}

// ✓ Правильно: один неймспейс для всей области
// Файл: Assets/GamePlay/Level/Edit/EditWindow/Visual/Timelines/TimelineGrid.cs
namespace GamePlay.Level.Edit
{
    public class TimelineGrid : MonoBehaviour { }
}

// ✓ Правильно: один неймспейс для всей области
// Файл: Assets/GamePlay/Level/Edit/EditWindow/Visual/Transforms/ObjectEditTransformView.cs
namespace GamePlay.Level.Edit
{
    public class ObjectEditTransformView : MonoBehaviour { }
}

// ✗ Неправильно: лишние части в неймспейсе
// namespace GamePlay.Level.Edit.EditWindow.Collision  ✗ Плохо!
// namespace GamePlay.Level.Edit.Timelines.Entries     ✗ Плохо!
```

### Структурирование по модулям

Вложенные папки используются для организации кода, но не для создания новых неймспейсов. Неймспейсы определяются по **верхнеуровневым модулям**:

```
Модули в проекте:
- GamePlay.Level.Edit        ← Весь редактор уровня
- GamePlay.Level.PlayMode    ← Проигрывание уровня
- GamePlay.Level.Objects     ← Игровые объекты
- GamePlay.Services          ← Сервисы GamePlay
- Global.Audio               ← Глобальная система звука
- Global.Cameras             ← Глобальная система камер
```

Папки внутри модулей служат для логической организации, а не для создания новых неймспейсов.

---

## 1. Нейминг классов

### Правило: КОНТЕКСТ - ИМЯ - ТИП

Когда контекст класса не очевиден, используй трёхчастный названия:
- **КОНТЕКСТ**: область применения (ObjectPalette, Timeline)
- **ИМЯ**: описание класса (Entry, Pointer)
- **ТИП**: категория класса (View, Handler)

### Примеры

```csharp
// Правильно: контекст не очевиден
public class ObjectPaletteEntryView : MonoBehaviour
{
    [SerializeField] private Image _image;
    [SerializeField] private TMP_Text _name;
}

public class ObjectPaletteEntryPointerHandler : MonoBehaviour
{
    public IViewableProperty<bool> Press { get; }
}

// Правильно: контекст очевиден из окружения
public class TimelineGrid : MonoBehaviour
{
    public IViewableDelegate<float> PositionClicked { get; }
}

public class TimelineRunner : ITimelineRunner
{
    public IViewableProperty<float> CurrentTime { get; }
}
```

### Специальные правила

- **Схемы** (данные для сериализации): всегда заканчиваются на `Scheme`
  ```csharp
  public class ObjectAnimationScheme { }
  public class ObjectBoundsScheme { }
  public class BaseObjectScheme : IObjectScheme { }
  ```

- **Узлы графа** (node graph): всегда заканчиваются на `Node`
  ```csharp
  public class ActionNode { }
  public class AnimationNode { }
  ```

---

## 2. Интерфейсы для сервисов

### Правило: интерфейс для всех публичных сервисов

Для каждого сервиса, регистрируемого в DI-контейнере (VContainer), создавай публичный интерфейс, если у класса есть публичное API.

### Примеры

```csharp
// ✓ Правильно: интерфейс для сервиса
public interface IAudioPlayer
{
    void PlaySound(AudioClip clip);
    void PlayLoopMusic(AudioClip clip);
}

public interface IAudioVolume
{
    IReadOnlyDictionary<AudioLine, float> Values { get; }
    IViewableProperty<bool> IsMuted { get; }

    void Mute();
    void Unmute();
    void SetVolume(AudioLine line, float volume);
}

// ✓ Правильно: интерфейс для track объектов
public interface IObjectAnimationTrack
{
    IViewableProperty<float> TotalDuration { get; }

    float RecalculateDuration();
    void Remove(IObjectAnimationFrame frame);
}

// ✓ Правильно: интерфейс для UI элементов
public interface ITimelineGrid
{
    IViewableDelegate<float> PositionClicked { get; }
}

public interface ITimelineRunner
{
    IViewableProperty<float> CurrentTime { get; }
    bool IsPlaying { get; }

    void SetTime(float time);
    void Play();
    void Switch();
    void Pause();
    void Stop();
}

// ✓ Правильно: интерфейс для pointer handler
public interface IObjectPaletteEntryPointerHandler
{
    IViewableProperty<bool> Press { get; }
}
```

---

## 3. Иммутабельное публичное API

### Правило: никакие публичные сеттеры, только IReadOnlyList/IReadOnlyDictionary

**Публичное API должно быть неизменяемым (immutable).** Все изменения должны происходить через методы, а не через прямой доступ.

### Примеры

```csharp
// ✓ Правильно: IReadOnlyList в интерфейсе
public interface ISpriteAnimationData
{
    IReadOnlyList<Sprite> Sprites { get; }
}

public class SpriteAnimationData : ISpriteAnimationData
{
    private readonly IReadOnlyList<Sprite> _sprites;

    public SpriteAnimationData(IReadOnlyList<Sprite> sprites, float time)
    {
        _sprites = sprites;
    }

    public IReadOnlyList<Sprite> Sprites => _sprites;
}

// ✓ Правильно: IReadOnlyDictionary в интерфейсе
public interface IAudioVolume
{
    IReadOnlyDictionary<AudioLine, float> Values { get; }
}

public class AudioVolume : IAudioVolume
{
    private Dictionary<AudioLine, float> _values = new();

    public IReadOnlyDictionary<AudioLine, float> Values => _values;

    public void SetVolume(AudioLine line, float volume)
    {
        _values[line] = volume;  // Изменение через метод, не через публичный сеттер
    }
}

// ✓ Правильно: Изменения только через методы
public interface ICurrentLevel
{
    IReadOnlyList<ILevelObject> Objects { get; }  // Только чтение
}

public class CurrentLevel : ICurrentLevel
{
    private readonly List<ILevelObject> _objects = new();

    public IReadOnlyList<ILevelObject> Objects => _objects;

    public void AddObject(ILevelObject obj)
    {
        _objects.Add(obj);  // Добавление через метод
    }
}

// ✗ Неправильно: публичный List вместо IReadOnlyList
public interface IWrongExample
{
    List<ILevelObject> Objects { get; }  // ✗ Плохо!
}
```

### Исключение: проект Schemes

В проекте `Schemes` разрешены публичные сеттеры, так как это чистые data классы для сериализации.

```csharp
// ✓ Правильно для Schemes: публичные сеттеры допустимы
public class ObjectAnimationScheme
{
    public string Name { get; set; }
    public ObjectAnimationType Type { get; set; }
    public List<ITrack> Tracks { get; set; }
    public ObjectAnimationPlayType PlayType { get; set; }
}
```

---

## 4. Порядок членов класса

### Правило: конструктор ВСЕГДА первый, затем поля, затем свойства и методы

Порядок в классе:
1. **Конструктор** (или `[Inject] private void Construct()` для MonoBehaviour)
2. **Поля** в порядке: `private readonly`, затем `private`, затем `public`
3. **Свойства** (property accessors)
4. **Методы** (public затем private)

### Примеры

```csharp
// ✓ Правильно: TimelineRunner (не MonoBehaviour)
public class TimelineRunner : ITimelineRunner, IScopeSetup
{
    // 1. Конструктор ПЕРВЫМ
    public TimelineRunner(
        IUpdater updater,
        IObjectEditVisualContext context,
        TimelineControls controls)
    {
        _updater = updater;
        _context = context;
        _controls = controls;
    }

    // 2. Поля (private readonly сначала)
    private readonly IUpdater _updater;
    private readonly IObjectEditVisualContext _context;
    private readonly TimelineControls _controls;
    private readonly ViewableProperty<float> _currentTime = new(0);

    // 3. Поля (private)
    private bool _isLocked;
    private ILifetime _playingLifetime;
    private ObjectAnimationDirectHandle _playingDirectHandle;

    // 4. Свойства
    public IViewableProperty<float> CurrentTime => _currentTime;
    public bool IsPlaying => _playingLifetime != null && !_playingLifetime.IsTerminated;

    // 5. Методы (public)
    public void OnSetup(IReadOnlyLifetime lifetime) { }
    public void SetTime(float time) { }
    public void Play() { }
    public void Pause() { }

    // 6. Методы (private)
    private void OnChange() { }
}

// ✓ Правильно: TimelineGrid (MonoBehaviour)
[DisallowMultipleComponent]
public class TimelineGrid : MonoBehaviour, ISceneService, ITimelineGrid
{
    [SerializeField] private GameObject _markerPrefab;
    [SerializeField] private ResponsiveContainer _container;

    // 1. [Inject] Construct ПЕРВЫЙ метод после полей
    [Inject]
    private void Construct(IObjectEditVisualContext context, ITimelineMetrics metrics)
    {
        _context = context;
        _metrics = metrics;
    }

    // 2. Поля (private readonly)
    private readonly List<GameObject> _markerPool = new();
    private readonly List<GameObject> _labelPool = new();
    private readonly ViewableDelegate<float> _timeClicked = new();

    // 3. Поля (private)
    private int _usedMarkers;
    private int _usedLabels;
    private float _gridPixelSize;
    private IObjectEditVisualContext _context;
    private ITimelineMetrics _metrics;

    // 4. Свойства
    public IViewableDelegate<float> PositionClicked => _timeClicked;

    // 5. Методы (public)
    public void Create(IScopeBuilder builder) { }
    public void OnSetup(IReadOnlyLifetime lifetime) { }
    public void OnPointerClick(PointerEventData eventData) { }

    // 6. Методы (private)
    private void RegenerateGrid(float totalDuration) { }
    private void UpdateContentSize() { }
}

// ✓ Правильно: PlayerRotation (не MonoBehaviour)
public class PlayerRotation : IPlayerRotation, IUpdatable
{
    // 1. Конструктор ПЕРВЫМ
    public PlayerRotation(IUpdater updater)
    {
        _updater = updater;
    }

    // 2. Поля (private readonly)
    private readonly ViewableProperty<Vertical> _vertical = new(Vertical.Down);
    private readonly ViewableProperty<Horizontal> _horizontal = new(Horizontal.Right);
    private readonly ViewableProperty<Angle> _angle = new(new Angle());
    private readonly IUpdater _updater;

    // 3. Свойства
    public IViewableProperty<Angle> Angle => _angle;
    public IViewableProperty<Vertical> VerticalSight => _vertical;
    public IViewableProperty<Horizontal> HorizontalSight => _horizontal;

    // 4. Методы (public)
    public void Start(IReadOnlyLifetime lifetime) { }
    public void SetForced(float angle) { }
    public void OnUpdate(float delta) { }
}
```

---

## 5. Схемы в проекте Schemes

### Правило: все что сериализуется в JSON - только в папке Schemes

**Только в `Assets/Schemes/`** должны находиться:
- Все data классы с публичными сеттерами (для JSON сериализации)
- Enum'ы, используемые в схемах
- Иерархии схем (наследование)

### Примеры

```csharp
// ✓ Правильно: схема в папке Schemes
// Assets/Schemes/Objects/ObjectAnimationScheme.cs
namespace Schemes
{
    public enum ObjectAnimationType
    {
        Idle,
        Move,
        Morf,
        Generic
    }

    public enum ObjectAnimationPlayType
    {
        Forward,
        Loop
    }

    public class ObjectAnimationScheme
    {
        public ObjectAnimationScheme(
            string name,
            ObjectAnimationType type,
            List<ITrack> tracks,
            ObjectAnimationPlayType playType = ObjectAnimationPlayType.Forward)
        {
            Name = name;
            Type = type;
            Tracks = tracks;
            PlayType = playType;
        }

        public string Name { get; set; }
        public ObjectAnimationType Type { get; set; }
        public List<ITrack> Tracks { get; set; }
        public ObjectAnimationPlayType PlayType { get; set; }

        public interface ITrack { }
        public class SpriteTrack : ITrack { }
        public class MorfTrack : ITrack { }
        public class AudioTrack : ITrack { }
    }
}

// ✓ Правильно: другие схемы
namespace Schemes
{
    public class BaseObjectScheme : IObjectScheme
    {
        public IdentityScheme Identity { get; set; }
        public IVisualObjectScheme Visuals { get; set; }
        public string IconPath { get; set; }
    }

    public class ObjectBoundsScheme { }
    public class IdentityScheme { }
}
```

---

## 6. Обёртки над схемами

### Правило: не модифицируй сырые Schemes, создавай обёртку

Никогда не работай с `ObjectAnimationScheme` напрямую в игровом коде. Создавай обёртку (`ObjectAnimation`), которая работает со схемой и добавляет логику.

### Примеры

```csharp
// ✓ Правильно: обёртка над схемой
public class ObjectAnimation
{
    // Конструктор принимает схему
    public ObjectAnimation(ObjectAnimationScheme scheme)
    {
        _scheme = scheme;
        _tracks = new ViewableList<IObjectAnimationTrack>();
    }

    public ObjectAnimation(ObjectAnimationScheme scheme, IBookAssets assets)
    {
        _scheme = scheme;
        _tracks = new ViewableList<IObjectAnimationTrack>();

        var tracks = scheme.ParseTracks(assets);
        foreach (var track in tracks)
            _tracks.Add(track);
    }

    // Поля: сырая схема остаётся приватной
    private readonly ObjectAnimationScheme _scheme;
    private readonly ViewableList<IObjectAnimationTrack> _tracks;

    // Публичное API: работа через обёртку, не через _scheme напрямую
    public IViewableList<IObjectAnimationTrack> Tracks => _tracks;
    public string Name => _scheme.Name;
    public ObjectAnimationType Type => _scheme.Type;
    public ObjectAnimationPlayType PlayType
    {
        get => _scheme.PlayType;
        set => _scheme.PlayType = value;
    }

    public float GetMaxDuration()
    {
        return _tracks.Max(t => t.TotalDuration.Value);
    }

    public ObjectAnimationSpriteTrack AddSpriteTrack()
    {
        var track = new ObjectAnimationSpriteTrack();
        _tracks.Add(track);
        return track;
    }
}

// ✗ Неправильно: работа со схемой напрямую
public class WrongObjectAnimationUser
{
    private ObjectAnimationScheme _scheme;

    public void BadMethod()
    {
        // ✗ Плохо: обращение к схеме напрямую
        var tracks = _scheme.Tracks;
        _scheme.PlayType = ObjectAnimationPlayType.Loop;
    }
}

// ✓ Правильно: использование обёртки
public class CorrectObjectAnimationUser
{
    private ObjectAnimation _animation;

    public void GoodMethod()
    {
        // ✓ Хорошо: работа через обёртку
        var tracks = _animation.Tracks;
        _animation.PlayType = ObjectAnimationPlayType.Loop;
    }
}
```

---

## 7. Реактивность вместо событий

### Правило: используй ViewableProperty/ViewableDelegate/ViewableList вместо event Action

Запрещено использовать:
- ~~`event Action`~~ ❌
- ~~`UnityEvent`~~ ❌

Используй вместо этого типы из `Internal/Common/Reactive`:
- `ViewableProperty<T>` - для проксирования значения с уведомлением об изменении
- `ViewableDelegate<T>` - для invoke-событий
- `ViewableList<T>` - для коллекций с уведомлением об изменении

### Примеры

```csharp
// ✗ Неправильно: event Action
public class BadExample
{
    public event Action<bool> ClickedChanged;  // ✗ Плохо!

    public void OnClicked()
    {
        ClickedChanged?.Invoke(true);
    }
}

// ✓ Правильно: ViewableProperty для булевского значения
public class GoodExample
{
    private readonly ViewableProperty<bool> _isPressed = new();

    public IViewableProperty<bool> Press => _isPressed;

    public void OnPointerDown(PointerEventData eventData)
    {
        _isPressed.Set(true);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _isPressed.Set(false);
    }
}

// Использование:
public class Consumer
{
    public void Start()
    {
        var handler = new GoodExample();

        // Подписка на изменения
        handler.Press.View(lifetime, value =>
        {
            if (value)
                Debug.Log("Pressed!");
        });
    }
}

// ✓ Правильно: ViewableDelegate для простого события
public class TimelineGridExample
{
    private readonly ViewableDelegate<float> _timeClicked = new();

    public IViewableDelegate<float> PositionClicked => _timeClicked;

    public void OnPointerClick(PointerEventData eventData)
    {
        var time = CalculateTime(eventData);
        _timeClicked.Invoke(time);
    }
}

// ✓ Правильно: ViewableProperty для float значения
public class TimelineRunnerExample
{
    private readonly ViewableProperty<float> _currentTime = new(0);

    public IViewableProperty<float> CurrentTime => _currentTime;

    public void SetTime(float time)
    {
        _currentTime.Set(time);
    }
}

// ✓ Правильно: ViewableList для коллекции
public class TimelineExample
{
    private readonly ViewableList<TimelineEntry> _entries = new();

    public IViewableList<TimelineEntry> Entries => _entries;

    public void AddEntry(TimelineEntry entry)
    {
        _entries.Add(entry);
    }

    public void RemoveEntry(TimelineEntry entry)
    {
        _entries.Remove(entry);
    }
}

// ✓ Правильно: Подписка на изменения
public class Observer
{
    public void Subscribe(IViewableProperty<float> time, IReadOnlyLifetime lifetime)
    {
        // View вернёт начальное значение и подпишется на изменения
        time.View(lifetime, currentTime =>
        {
            Debug.Log($"Time changed to: {currentTime}");
        });
    }

    public void SubscribeToList(IViewableList<TimelineEntry> entries, IReadOnlyLifetime lifetime)
    {
        // Подписка на изменения коллекции
        entries.View(lifetime, view =>
        {
            view.Changed += item => Debug.Log($"Entry: {item}");
        });
    }
}
```

---

## Резюме правил

| Правило | Описание |
|---------|---------|
| **Неймспейсы** | Один неймспейс для верхнеуровневого модуля, папки не добавляют части к неймспейсу |
| **Нейминг** | КОНТЕКСТ-ИМЯ-ТИП (если контекст не очевиден) |
| **Интерфейсы** | Интерфейс для каждого сервиса с публичным API |
| **Иммутабельность** | Только `IReadOnlyList<T>`, `IReadOnlyDictionary<K,V>` в публичном API |
| **Порядок** | Конструктор → Поля → Свойства → Методы |
| **Схемы** | Только в `Assets/Schemes/` для JSON сериализации |
| **Обёртки** | Всегда wrap схемы в бизнес-классы (ObjectAnimation, и т.д.) |
| **Реактивность** | ViewableProperty, ViewableDelegate, ViewableList вместо event/UnityEvent |
