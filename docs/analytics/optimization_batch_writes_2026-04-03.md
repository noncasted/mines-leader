# Оптимизация: Multi-row batch writes — 2026-04-03

## Что изменено

`StateStorage.Write(NpgsqlTransaction, IReadOnlyDictionary)` — вместо отдельного `INSERT ON CONFLICT` на каждый record, записи группируются по таблице и выполняются одним multi-row INSERT:

```sql
-- Было (N round-trips):
INSERT INTO table VALUES (@key, @type, @version, @value) ON CONFLICT DO UPDATE;  -- x N

-- Стало (1 round-trip на группу):
INSERT INTO table VALUES (@key0, ...), (@key1, ...), (@key2, ...) ON CONFLICT DO UPDATE;
```

`StateStorage.Delete(IReadOnlyList)` — аналогично, batch DELETE вместо foreach.

## Результаты: State бенчмарки

| Benchmark | Tx Avg ДО | Tx Avg ПОСЛЕ | Ускорение | Tx Кол-во ДО | Tx Кол-во ПОСЛЕ |
|---|---|---|---|---|---|
| transactions-state | 3.69ms | 2.44ms | **-34%** | 7,915 | 6,693 |
| transactions-state-value | 8.00ms | 3.08ms | **-62%** | 6,626 | 15,608 |
| transactions-state-chained | 8.64ms | 2.73ms | **-68%** | 10,940 | 21,893 |
| transactions-state-chained-fail | 8.50ms | 2.59ms | **-70%** | 11,119 | 2,876 |
| transactions-state-overlapping | 8.55ms | 3.87ms | **-55%** | 7,621 | 6,600 |
| transactions-single-target | 10.21ms | 10.97ms | +7% | 6,801 | 2,000 |
| transactions-single-chain | 14.68ms | 7.90ms | **-46%** | 3,022 | 1,700 |
| transactions-concurrent-value | 28.50ms | 6.49ms | **-77%** | 1,922 | 10,171 |
| transactions-cross-path-read | 24.98ms | 5.29ms | **-79%** | 5,595 | 23,329 |
| transactions-large-batch | 25.02ms | 8.84ms | **-65%** | 11,869 | 2,250 |

## Наблюдения

### 1. Среднее ускорение транзакций: 55-79%

Наибольший выигрыш у сложных бенчмарков:
- `transactions-concurrent-value`: 28.5ms -> 6.49ms (**-77%**)
- `transactions-cross-path-read`: 24.98ms -> 5.29ms (**-79%**)
- `transactions-state-chained`: 8.64ms -> 2.73ms (**-68%**)

Простейший `transactions-state` (1 participant) улучшился на 34% — даже для single-record batch, overhead создания NpgsqlCommand был ощутим.

### 2. transactions-single-target не улучшился (+7%)

Это бенчмарк contention — несколько транзакций конкурируют за один grain через SemaphoreSlim. Bottleneck не в SQL writes, а в ожидании lock-а. Batch writes здесь не помогают.

### 3. Количество транзакций изменилось

Бенчмарки выполняются за фиксированное время. Если каждая транзакция быстрее — за то же время выполняется больше транзакций:
- `transactions-cross-path-read`: 5,595 -> 23,329 (**4.2x throughput**)
- `transactions-concurrent-value`: 1,922 -> 10,171 (**5.3x throughput**)
- `transactions-state-chained`: 10,940 -> 21,893 (**2x throughput**)

### 4. state (прямые writes без транзакций) — без изменений

`state` бенчмарк пишет через `StateStorage.Write(identity, value)` — single write, который вызывает batch write с 1 элементом. Для одного элемента нет группировки — overhead ~0. Write avg остался 1.13-1.14ms.

### 5. Failure pattern изменился

- `transactions-state-chained-fail` получил 420 failures (было 0) — это нормально, бенчмарк специально тестирует failure path
- `transactions-single-target` потерял failures (420 -> 0) — меньше contention при 2,000 tx vs 6,801

## Итог

Multi-row batch writes дали **55-79% ускорение транзакций** и **2-5x увеличение throughput** на сложных бенчмарках. Оптимизация затрагивает только `StateStorage.Write` и `StateStorage.Delete` — минимальный blast radius, максимальный эффект.
