## Задача: Декларативный генератор Unity-префабов из кода

### Цель
При перезагрузке редактора автоматически сканировать классы с `[PrefabDefinition]`, выполнять их `Define()` методы через fluent API (`PrefabBuilder`), сохранять результат как `.prefab` в `Resources/Generated/`, и генерировать per-asmdef статичные классы-агрегаторы для типизированного доступа.

### API спецификация

**Пример использования:**
```csharp
[PrefabDefinition]
public static class PlayerPrefabDef {
    public static void Define(PrefabBuilder builder) {
        builder
            .Name("Player")
            .WithComponent<SpriteRenderer>(out var sr)
            .SetSerialized<SpriteRenderer>("m_Sprite", "Sprites/player")
            .WithComponent<MyComponent>()
            .SetSerialized<MyComponent>("targetRenderer", sr)
            .WithChildObject("Gun", child => {
                child.WithComponent<SpriteRenderer>()
                     .SetSerialized<SpriteRenderer>("m_Sprite", "Sprites/gun");
            });
    }
}
```

**Методы PrefabBuilder:**
- `Name(string)` — имя префаба и GameObject
- `WithComponent<T>(out T)` — добавить компонент, вернуть ссылку
- `WithComponent<T>(Action<T>)` — добавить + настроить публичные поля
- `SetSerialized<T>(fieldName, value)` — записать в `[SerializeField]` через `SerializedObject` API
- `WithChildObject(name, Action<PrefabBuilder>)` — дочерний объект

**Генератор:**
- Триггер: `[InitializeOnLoadMethod]` при editor reload
- Для каждого `[PrefabDefinition]` класса определяет asmdef через путь к файлу
- Группирует по asmdef — генерирует агрегатор в заданную папку
- Идемпотентность: пересоздаёт префаб заново, не патчит
- Warning в консоли если `.prefab` был изменён вручную после последней генерации

**Не делаем (пока):**
- Reverse-builder (редактор — код)
- AutoWire по типу
- Typed-обёртки (пока `GameObject`)
- Prefab variants

### Шаги реализации

1. **Создать `PrefabDefinitionAttribute`** — `client/Assets/Internal/Common/PrefabBuilder/PrefabDefinitionAttribute.cs` [новый файл — добавить в Internal.csproj]
2. **Создать `PrefabBuilder`** (runtime fluent API) — `client/Assets/Internal/Common/PrefabBuilder/PrefabBuilder.cs` [новый файл — добавить в Internal.csproj]
3. **Создать Editor asmdef** — `client/Assets/Tools/PrefabBuilder/Editor/Tools.PrefabBuilder.Editor.asmdef` [новый файл] — ссылки на Internal + Tools.Editor
4. **Создать `PrefabGenerator`** (сканирование + генерация .prefab) — `client/Assets/Tools/PrefabBuilder/Editor/PrefabGenerator.cs` [новый файл — добавить в новый .csproj]
5. **Создать `PrefabsClassGenerator`** (генерация per-asmdef агрегаторов) — `client/Assets/Tools/PrefabBuilder/Editor/PrefabsClassGenerator.cs` [новый файл — добавить в новый .csproj]
6. **Создать папку `Resources/Generated/`** — `client/Assets/Resources/Generated/` + `.gitkeep`
7. **Агрегаторы** — генерируются автоматически кодом из шага 5:
   - `client/Assets/GamePlay/Common/Prefabs/GamePlayPrefabs.cs` (создаст папку если нет)
   - `client/Assets/Menu/Common/Prefabs/MenuPrefabs.cs`
   - `client/Assets/Global/Common/Prefabs/GlobalPrefabs.cs`

### Ключевые файлы

| Файл | Роль в задаче |
|------|---------------|
| `client/Assets/Internal/Common/PrefabBuilder/` | Пустая папка — уже создана, сюда пойдут runtime-классы |
| `client/Assets/Internal/Internal.asmdef` | Runtime-сборка, видимая всем asmdef — атрибут и builder живут здесь |
| `client/Internal.csproj` | Регистрация новых .cs файлов Internal |
| `client/Assets/Tools/EditorTools/Tools.Editor.asmdef` | Существующий editor asmdef — reference для нового editor asmdef |
| `client/Assets/Resources/` | Существующая папка Resources — создаём подпапку Generated/ |
| `client/Assets/GamePlay/Common/` | НЕ существует — нужно создать для агрегатора |
| `client/Assets/Menu/Common/` | Существует — агрегатор ляжет в подпапку Prefabs/ |
| `client/Assets/Global/Common/` | НЕ существует — нужно создать для агрегатора |

### Документация к прочтению
- `rules/CODE_STYLE.md` — порядок членов, naming, braces same line

### Риски
- **`SetSerialized<T>` работает только в Editor** — `PrefabBuilder` содержит Editor-only API (`SerializedObject`), но живёт в runtime-сборке Internal. Нужно либо: (a) разделить builder на runtime-часть (описание) и editor-часть (применение через SerializedObject), либо (b) использовать `#if UNITY_EDITOR` в PrefabBuilder. Вариант (a) чище, но сложнее. Предлагаю (b) — `PrefabBuilder` полностью под `#if UNITY_EDITOR`, т.к. `Define()` вызывается только из editor-кода.
- **Папки `GamePlay/Common/` и `Global/Common/` не существуют** — генератор должен создавать их автоматически при первой генерации.
- **asmdef → папка агрегатора маппинг** — нужна конфигурация маппинга (asmdef name → output path), иначе генератор не знает куда класть агрегатор. Предлагаю хардкод-словарь в `PrefabsClassGenerator`.
- **Идемпотентность при отсутствии изменений** — нужно сравнивать хеш/содержимое `.prefab` перед перезаписью, чтобы не триггерить лишний AssetDatabase.Refresh.
