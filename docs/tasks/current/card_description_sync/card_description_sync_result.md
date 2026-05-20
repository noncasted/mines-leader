## Синхронизация описаний карт — Результат

### Статус: Завершено

### Что сделано
1. Добавлены 22 интерфейса эффектов в `shared/Configs/CardConfigOptions.cs`:
   - `IDurationalCardConfig`, `IDamageCardConfig`, `IHealCardConfig`
   - `IAreaSizeCardConfig`, `ISearchRadiusCardConfig`, `IDrainAmountCardConfig`, `IChainCardConfig`
   - `IExtraMovesCardConfig`, `IDrawCountCardConfig`, `IMovesReductionCardConfig`
   - `IManaGainCardConfig`, `IHpCostCardConfig`, `ICoinTossCardConfig`, `IManaRangeCardConfig`
   - `IDiscountCardConfig`, `ICostIncreaseCardConfig`, `IWinLoseDrawCardConfig`, `IGamblersRuinCardConfig`
   - `IRandomSizeCardConfig`, `IRandomLengthCardConfig`, `ILengthCardConfig`
   - `IMinesRangeCardConfig`, `IPeekCountCardConfig`
2. Интерфейсы применены ко всем 55 классам карт
3. Все read-only expression-bodied свойства (`Duration`, `ExtraMoves`, `DrawCount`, `Discount`, `HpCost`, `SearchRadius`) переделаны в settable
4. Удалены дублирующие `Duration` свойства — оставлен только `TurnsDuration`
5. Обновлён `client/Assets/Resources/cards-info.json` — 30+ описаний обновлены маркап-токенами
6. Обновлён `CardDescriptionProvider` — поддерживает все 25+ токенов
7. Проект `shared` и `backend/Game` компилируются без ошибок
8. Все 634 backend-тестов проходят
9. Обновлена документация `.agents/docs/CARD_EFFECTS.md`

### Измененные файлы

| Файл | Что изменено |
|------|-------------|
| `shared/Configs/CardConfigOptions.cs` | 22 новых интерфейса, применены ко всем картам, удалены дублирующие Duration |
| `client/Assets/Resources/cards-info.json` | 30+ описаний с маркап-токенами |
| `client/Assets/Meta/Cards/CardDescriptionProvider.cs` | Полная поддержка всех токенов |
| `.agents/docs/CARD_EFFECTS.md` | Полная документация всех эффектов и токенов |

### Нерешенные вопросы
Нет.
