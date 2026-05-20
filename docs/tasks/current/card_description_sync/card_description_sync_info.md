## Задача: Синхронизация описаний карт с конфигами

### Цель
Создать систему для синхронизации числовых эффектов в описаниях карт с backend-конфигами через runtime-разрешение маркап-токенов.

1. В `shared/Configs/CardConfigOptions.cs` добавить интерфейсы:
   - `IDurationalCardConfig { int TurnsDuration { get; } }`
   - `IDamageCardConfig { int Damage { get; } }`
   - `IHealCardConfig { int Heal { get; } }`
   - Применить их на все карты, у которых есть длительность, урон или лечение.
   - Исправить несоответствия между описаниями и значениями в конфигах (Frost_Max, Blackout).

2. В описаниях карт (`client/Assets/Resources/cards-info.json`) заменить хардкод-числа на маркап-токены:
   - `{ROUNDS}` для длительности
   - `{DAMAGE}` для урона
   - `{HEAL}` для лечения

3. Создать `CardDescriptionProvider` рядом с `CardsRegistry.cs`, который:
   - Берёт шаблон описания из JSON
   - Проходит по всем конфигам
   - Проверяет `is IDurationalCardConfig`, `is IDamageCardConfig`, `is IHealCardConfig`
   - Делает `Replace` токенов на реальные значения из конфигов
   - Предоставляет `GetDescription(CardType)` для потребителей

4. Подключить `CardDescriptionProvider` ко всем местам отображения карт:
   - `CardDataView` (игровой UI)
   - `CardRevealView` (анимация получения карты)
   - `MenuDeckPoolCard` (билдер колод — пул карт)
   - `MenuDeckCard` (билдер колод — слот колоды)

5. Задокументировать все эффекты в `.agents/docs/CARD_EFFECTS.md` для агентов.

### Контекст
Описания карт в `cards-info.json` содержат хардкод-числа ("for 2 rounds", "take 1 damage"), которые расходятся с backend-конфигами. Нужна единая точка истины — конфиги — и runtime-подстановка значений в шаблоны описаний.

### Шаги реализации

**1. Интерфейсы и применение в конфигах**
  1.1. Добавить `IDurationalCardConfig`, `IDamageCardConfig`, `IHealCardConfig` в `shared/Configs/CardConfigOptions.cs`
  1.2. Применить `IDurationalCardConfig` к: `Smoke`, `FogOfWar`, `Lockdown`, `ChaosFog`, `Frost`, `Blackout`, `SoulLink`
  1.3. Применить `IDamageCardConfig` к: `OpponentBomb`
  1.4. Применить `IHealCardConfig` к: `Medic`
  1.5. Сделать `Smoke.Duration` и `FogOfWar.Duration` settable (сейчас read-only expression)
  1.6. Добавить `Damage = 1` в `OpponentBomb`
  1.7. Добавить `Heal = 1` в `Medic`
  1.8. Исправить `Frost_Max` → `Duration = 2`
  1.9. Исправить `Blackout.Normal` → `Duration = 1` (соответствует описанию)

**2. Маркап в JSON-описаниях**
  2.1. Обновить `client/Assets/Resources/cards-info.json`: заменить числа на `{ROUNDS}`, `{DAMAGE}`, `{HEAL}`

**3. CardDescriptionProvider**
  3.1. Создать `client/Assets/Meta/Cards/CardDescriptionProvider.cs` [новый файл — добавить в Meta.asmdef]
  3.2. Зарегистрировать в `client/Assets/Meta/MetaServicesExtensions.cs`

**4. Интеграция с UI**
  4.1. Обновить `CardDataView.cs` — инжектить `ICardDescriptionProvider`
  4.2. Обновить `CardRevealView.cs` — инжектить `ICardDescriptionProvider`
  4.3. Обновить `MenuDeckPoolCard.cs` — инжектить `ICardDescriptionProvider`, добавить `ResolvedDescription`
  4.4. Обновить `MenuDeckCard.cs` — использовать `_currentCard.ResolvedDescription`

**5. Документация**
  5.1. Создать `.agents/docs/CARD_EFFECTS.md` с описанием всех эффектов и интерфейсов

### Ключевые файлы

| Файл | Роль в задаче |
|------|---------------|
| `shared/Configs/CardConfigOptions.cs` | Источник истины для числовых эффектов карт |
| `client/Assets/Resources/cards-info.json` | Шаблоны описаний с маркап-токенами |
| `client/Assets/Meta/Cards/CardDescriptionProvider.cs` | Runtime-разрешение токенов в значения из конфигов |
| `client/Assets/Meta/Cards/CardsRegistry.cs` | Источник шаблонов описаний |
| `client/Assets/GamePlay/Cards/Entities/View/CardDataView.cs` | UI отображения карты в игре |
| `client/Assets/GamePlay/Cards/Entities/View/CardRevealView.cs` | UI анимации получения карты |
| `client/Assets/Menu/Decks/MenuDeckPoolCard.cs` | UI карты в пуле билдера колод |
| `client/Assets/Menu/Decks/MenuDeckCard.cs` | UI слота колоды в билдере |
| `client/Assets/Meta/MetaServicesExtensions.cs` | Регистрация DI |

### Документация к прочтению
- `.agents/docs/COMMON_CONTAINER.md` — VContainer DI, инжекция в MonoBehaviour
- `.agents/docs/COMMON_REACTIVE_BASICS.md` — BackendProjection, Value
- `.agents/docs/CODE_STYLE_FULL.md` — member order, naming

### Риски
- `Smoke` и `FogOfWar` имеют `Duration` как read-only expression-bodied property — нужно переделать в settable
- `MenuDeckCard` не использует DI напрямую — нужно передать resolved description через `MenuDeckPoolCard`
- `ICardConfigs.Value` может быть `null` до получения данных с backend — `CardDescriptionProvider` должен gracefully fallback на raw template
