# Неиспользуемые WithParameter

## Контекст

В генераторе контейнера framework появилось предупреждение CINGR015: значение из `WithParameter` не может принять ни
один параметр конструктора или `[Inject]`-метода класса, и оно теряется. Сборку это не ломает.

Правка в framework: коммит `e1bb569`, он уже в `origin/main`. `tools/framework` в игре на 2026-11-02 ещё на `c5b7c94`, без неё. Вместе с ней меняется и привязка:
`WithParameter` теперь важнее регистрации того же типа и того, что scope даёт сам (`IContainer`, `ILifetime`,
`IReadOnlyLifetime`).

Список ниже получен прогоном нового генератора по исходникам игры в памяти, по устаревшим csproj. Игра с новым framework
не собиралась, поэтому при настоящей сборке список может измениться.

## Что сделать

1. Обновить submodule `tools/framework` на версию с CINGR015 и собрать клиент. Сверить предупреждения со списком ниже.
2. Удалить лишние вызовы из пунктов A и B.
3. Разобрать пункт C: для каждого действия решить, остаток это или значения конфига действительно должны доходить.
4. После обновления проверить уничтожение карт и конец матча: корни `LocalCard` и `RemoteCard` теперь получают
   `ScopeLifetime`, а не lifetime контейнера, и порядок разрушения сущности изменился.

Критерий готовности: сборка клиента без CINGR015, в проверке из шага 4 поведение не сломано.

## A. `*SnapshotHandler` (14 предупреждений, одна строка)

Лишний `.WithParameter(builder.Lifetime)` в общем `AddSnapshotHandler`:
`client/Assets/GamePlay/Services/Sync/ISnapshotHandler.cs:22`. Перед удалением убедиться, что ни одному обработчику
lifetime не нужен: по прогону его не принимает ни один из них.

Обработчики: `BoardSnapshotHandler`, `BoardStateUpdateSnapshotHandler`, `CardRemoveSnapshotHandler`,
`CardsStashedSnapshotHandler`, `DeckSnapshotHandler`, `GameCompletedSnapshotHandler`, `GameStartedSnapshotHandler`,
`LastManStandingRoundSnapshotHandler`, `PlayerHealthSnapshotHandler`, `PlayerManaSnapshotHandler`,
`PlayerModifierSnapshotHandler`, `PlayerMovesSnapshotHandler`, `StashSnapshotHandler`, `TimeLimitedRoundSnapshotHandler`.

## B. `CardLocalSpawn` (1 предупреждение)

Лишний `.WithParameter(builder.Lifetime)`: `client/Assets/GamePlay/Cards/Entities/States/CardStatesExtensions.cs:29`.
В конструкторе нет параметра lifetime, класс берёт его через `ICardStateLifetime`.

## C. Действия карт (32 предупреждения)

`CardStatesExtensions.cs`, `switch` с `registration.WithParameter(configs.X_Normal)` (строки примерно 138-170): конфиг
передаётся, но конструктор действия его не принимает.

Проверено только `CardAdrenalineAction`: похоже на остаток, а не на баг. Конструктор принимает только
`ICardDropDetector`, `TryUse` не читает значения конфига, а вложенный `Snapshot` берёт настройки через `ICardConfigs`.
Остальные действия не проверялись. Поля вроде `ManaCost` и `Target` есть у всех этих конфигов, у многих есть и свои
значения (`ExtraMoves`, `Heal`, `DrainAmount`, `SearchRadius`), так что для каждого действия нужно убедиться, что эти
значения читаются в другом месте.

Отдельно посмотреть варианты `_Max`: если у действия нет параметра конфига, `_Normal` и `_Max` на клиенте через этот
вызов не различаются.

Действия: `Adrenaline`, `BloodPact`, `CardThief`, `ChainReaction`, `CoinToss`, `DoubleOrNothing`, `Dud`, `Embargo`,
`Focus`, `FortuneCookie`, `GamblersRuin`, `Gravedigger`, `HandScramble`, `Lockdown`, `ManaFountain`, `ManaSurge`, `Medic`,
`MirrorMatch`, `MysticDraw`, `OpponentBomb`, `Overclock`, `PowerSurge`, `Purge`, `Recycler`, `SabotageDeck`, `Salvage`,
`Scavenger`, `Shield`, `Siphon`, `SoulLink`, `TrebuchetAimer`, `ZipZap` (класс `Card<Имя>Action`, конфиг
`CardConfigOptions.<Имя>`).
