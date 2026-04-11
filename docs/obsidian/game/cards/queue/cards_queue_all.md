# Очередь на реализацию

30 карт со статусом PASS из трёх пулов: Ordinary (13), Research (8), Ludic (9). Карты сгруппированы по функциональным категориям и ждут реализации в клиенте и бэкенде.

Контекст игры: поле 16x16, 40 мин, 3 HP, 5 ходов/ход, мана стартует с 1, растёт +1/ход. Колода 10 карт, рука до 5, сброс перерабатывается.

---

## Сводная таблица

| # | Карта | Мана | Цель | Паттерн | Категория | Пул |
|---|-------|------|------|---------|-----------|-----|
| 1 | Thermal Vision | 3 | OwnBoard | Ромб (3) | Разведка | Ordinary |
| 2 | Fortune Cookie | 1 | Self | -- | Разведка | Ludic |
| 3 | Excavator | 3 | OwnBoard | Крест (3) | Разведка | Ordinary |
| 4 | Chaos Diamond | 2 | OwnBoard | Ромб (2-5) | Разведка | Ludic |
| 5 | Chaos Scout | 2 | OwnBoard | Линия (3-7) | Разведка | Ludic |
| 6 | Recycler | 1 | Self | -- | Колода | Ordinary |
| 7 | Salvage | 2 | Self | -- | Колода | Ordinary |
| 8 | Mystic Draw | 1 | Self | -- | Колода | Ludic |
| 9 | Card Thief | 3 | Opponent | -- | Колода | Research |
| 10 | Sabotage Deck | 2 | Opponent | -- | Колода | Research |
| 11 | Adrenaline | 1 | Self | -- | Усиление | Ordinary |
| 12 | Focus | 1 | Self | -- | Усиление | Ordinary |
| 13 | Coin Toss | 1 | Self | -- | Усиление | Ludic |
| 14 | Shield | 2 | Self | -- | Усиление | Ordinary |
| 15 | Power Surge | 3 | Self | -- | Усиление | Research |
| 16 | Carpet Bomb | 5 | OpponentBoard | Линия (5) | Кросс-борд | Ordinary |
| 17 | Mine Cluster | 3 | OpponentBoard | Крест (2) | Кросс-борд | Ordinary |
| 18 | Fortune Blast | 2 | OpponentBoard | Ромб (1-4) | Кросс-борд | Ludic |
| 19 | Blackout | 2 | OpponentBoard | Ромб (2) | Кросс-борд | Ordinary |
| 20 | Frost | 2 | OpponentBoard | Ромб (2) | Кросс-борд | Ordinary |
| 21 | Chaos Fog | 2 | OpponentBoard | Ромб (1-4) | Кросс-борд | Ludic |
| 22 | Dimension Rift | 4 | OpponentBoard | Ромб (2) | Кросс-борд | Research |
| 23 | Mirror Match | 3 | Self | -- | Кросс-борд | Research |
| 24 | Mana Surge | 2 | Self | -- | Ресурсы | Ordinary |
| 25 | Mana Fountain | 1 | Self | -- | Ресурсы | Ludic |
| 26 | Double or Nothing | 2 | Self | -- | Ресурсы | Ludic |
| 27 | Soul Link | 3 | Opponent | -- | Ресурсы | Research |
| 28 | Gambler's Ruin | 2 | Self | -- | Ресурсы | Research |
| 29 | Blood Pact | 0 | Self | -- | Ресурсы | Research |
| 30 | Embargo | 3 | Opponent | -- | Ресурсы | Ordinary |

---

## Разведка и расчистка (5 карт)

[[cards_queue_scout]]

- Thermal Vision -- подсветка мин в ромбе на текущий ход, без флагов
- Fortune Cookie -- рулетка: раскрывает 1-3 случайных мины
- Excavator -- крестообразная расчистка, мины флажатся
- Chaos Diamond -- рандомный Bloodhound: ромб случайного размера
- Chaos Scout -- рандомный Minefield Scout: линия случайной длины

## Колода (5 карт)

[[cards_queue_hand]]

- Recycler -- сброс 1 карты, добор 2
- Salvage -- посмотреть 3 верхних карты, взять 1
- Mystic Draw -- монетка: орёл +2 карты, решка -2 карты
- Card Thief -- украсть 1 случайную карту из руки противника
- Sabotage Deck -- подмешать карту-пустышку Dud в колоду противника

## Усиление (5 карт)

[[cards_queue_improve]]

- Adrenaline -- +1 ход в текущем ходу
- Focus -- следующая карта стоит -1 ману (мин 0)
- Coin Toss -- монетка: орёл +2 хода, решка -1 ход
- Shield -- следующее попадание на мину = 0 урона
- Power Surge -- все карты -1 мана в этом ходу (мин 1)

## Кросс-борд (8 карт)

[[cards_queue_cross]]

- Carpet Bomb -- мины линией (5) на поле противника
- Mine Cluster -- мины крестом (центр + 4 стороны) на поле противника
- Fortune Blast -- рандомный Trebuchet: мины ромбом случайного размера
- Blackout -- замена чисел на "?" на 2 раунда
- Frost -- заморозка клеток на 1 раунд
- Chaos Fog -- рандомный Smoke: дым ромбом случайного размера
- Dimension Rift -- обмен областями между полями
- Mirror Match -- копирует последнюю карту противника в свою пользу

## Ресурсы (7 карт)

[[cards_queue_resources]]

- Mana Surge -- +3 временной маны на текущий ход
- Mana Fountain -- кость d5: +1..+5 временной маны
- Double or Nothing -- монетка: орёл = удвоить ману, решка = обнулить
- Soul Link -- связь на 2 раунда: ваш урон от мины = урон противнику тоже
- Gambler's Ruin -- монетка: орёл +3 карты +2 маны, решка -2 карты
- Blood Pact -- -1 HP, +3 временной маны, +2 хода
- Embargo -- все карты противника +1 мана на 1 ход
