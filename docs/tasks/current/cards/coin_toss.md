## Задача: CoinToss — монетка: +2/-1 ход

### Статус: Не начато

### Карта

| Поле | Значение |
|------|----------|
| Имя | CoinToss |
| Мана | 1 |
| Цель | Self |
| Паттерн | -- |
| Категория | Усиление |
| Пул | Ludic |
| Max-вариант | Нет |

### Описание эффекта

Бросок монеты: орёл — +2 хода в текущем ходу, решка — -1 ход. Рискованная альтернатива Adrenaline.

### Сложность: 2

Как Adrenaline, но с RNG (50/50). Нужен генератор случайных чисел на бэке + передача результата броска клиенту через snapshot.

---

### Шаги реализации

#### 1. Shared

- [ ] 1.1. `shared/Domain/CardType.cs` — `CoinToss = XXXX`
- [ ] 1.2. `shared/Configs/CardConfigOptions.cs` — ManaCost=1, WinMoves=2, LoseMoves=-1, Target=Self
- [ ] 1.3-1.4. Snapshot должен содержать bool IsHeads для визуализации

#### 2. Backend

- [ ] 2.1. `backend/Game/GamePlay/Cards/CoinToss.cs` — Random flip, AddMoves(+2 или -1)
- [ ] 2.2. CardFactory
- [ ] 2.3. `CoinTossStrategy.cs` — utility: средний если ходов >= 2 (страховка от решки)
- [ ] 2.4. BotServiceExtensions

#### 3-7. Стандартные шаги

---

### Особые требования

- Snapshot должен передавать результат броска (IsHeads) для анимации на клиенте
- Клиент: анимация броска монеты

### Зависимости

Adrenaline (аналогичная механика ходов).
