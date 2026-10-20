# Card Effects

## Overview

Card effect values are defined in `shared/Configs/CardConfigOptions.cs` and synchronized into runtime descriptions via `CardDescriptionProvider`.

Descriptions in `client/Assets/Resources/cards-info.json` use markup tokens that are replaced at runtime with actual config values.

## Effect Interfaces

| Interface | Properties | Cards | Tokens |
|-----------|------------|-------|--------|
| `IDurationalCardConfig` | `int TurnsDuration { get; }` | Smoke, FogOfWar, Lockdown, ChaosFog, Frost, Blackout, SoulLink | `{ROUNDS}` |
| `IDamageCardConfig` | `int Damage { get; }` | OpponentBomb | `{DAMAGE}` |
| `IHealCardConfig` | `int Heal { get; }` | Medic, Shield | `{HEAL}` |
| `IAreaSizeCardConfig` | `int Size { get; }` | Bloodhound, Trebuchet, TrebuchetAimer, ErosionDozer, ZipZap, OpponentFlagErase, OpponentFlagReshuffle, Smoke, FogOfWar, MinefieldScout, Sonar, Excavator, ThermalVision, MineCluster, Blackout, Frost, DimensionRift | `{SIZE}` |
| `IDrainAmountCardConfig` | `int DrainAmount { get; }` | Siphon | `{DRAIN_AMOUNT}` |
| `IChainCardConfig` | `int MaxChain { get; }`, `int SearchRadius { get; }`, `int SpawnSize { get; }` | ChainReaction | `{MAX_CHAIN}` |
| `IExtraMovesCardConfig` | `int ExtraMoves { get; }` | Overclock, Adrenaline, BloodPact | `{EXTRA_MOVES}` |
| `IDrawCountCardConfig` | `int DrawCount { get; }` | Scavenger, Recycler | `{DRAW_COUNT}` |
| `IMovesReductionCardConfig` | `int MovesReduction { get; }` | Lockdown | `{MOVES_REDUCTION}` |
| `IManaGainCardConfig` | `int ManaGain { get; }` | ManaSurge, BloodPact | `{MANA_GAIN}` |
| `IHpCostCardConfig` | `int HpCost { get; }` | BloodPact | `{HP_COST}` |
| `ICoinTossCardConfig` | `int WinMoves { get; }`, `int LoseMoves { get; }` | CoinToss | `{WIN_MOVES}`, `{LOSE_MOVES}` |
| `IManaRangeCardConfig` | `int MinMana { get; }`, `int MaxMana { get; }` | ManaFountain | `{MIN_MANA}`, `{MAX_MANA}` |
| `IDiscountCardConfig` | `int Discount { get; }` | Focus, PowerSurge | `{DISCOUNT}` |
| `ICostIncreaseCardConfig` | `int CostIncrease { get; }` | Embargo | `{COST_INCREASE}` |
| `IWinLoseDrawCardConfig` | `int WinDraw { get; }`, `int LoseReturn { get; }` | MysticDraw | `{WIN_DRAW}`, `{LOSE_RETURN}` |
| `IGamblersRuinCardConfig` | `int WinDraw { get; }`, `int WinMana { get; }`, `int LoseDiscard { get; }` | GamblersRuin | `{WIN_DRAW}`, `{WIN_MANA}`, `{LOSE_DISCARD}` |
| `IRandomSizeCardConfig` | `int MinSize { get; }`, `int MaxSize { get; }` | ChaosDiamond, FortuneBlast, ChaosFog | `{MIN_SIZE}`, `{MAX_SIZE}` |
| `IRandomLengthCardConfig` | `int MinLength { get; }`, `int MaxLength { get; }` | ChaosScout | `{MIN_LENGTH}`, `{MAX_LENGTH}` |
| `ILengthCardConfig` | `int Length { get; }` | CarpetBomb | `{LENGTH}` |
| `IMinesRangeCardConfig` | `int MinMines { get; }`, `int MaxMines { get; }` | FortuneCookie | `{MIN_MINES}`, `{MAX_MINES}` |
| `IPeekCountCardConfig` | `int PeekCount { get; }` | Salvage | `{PEEK_COUNT}` |

## Markup Tokens

Tokens in `cards-info.json` descriptions are replaced by `CardDescriptionProvider.GetDescription(CardType)`:

| Token | Replaced With | Example |
|-------|---------------|---------|
| `{ROUNDS}` | `IDurationalCardConfig.TurnsDuration` | "for {ROUNDS} rounds" |
| `{DAMAGE}` | `IDamageCardConfig.Damage` | "take {DAMAGE} damage" |
| `{HEAL}` | `IHealCardConfig.Heal` | "Restores {HEAL} HP" |
| `{SIZE}` | `IAreaSizeCardConfig.Size` | "up to {SIZE} mines" |
| `{DRAIN_AMOUNT}` | `IDrainAmountCardConfig.DrainAmount` | "Drains {DRAIN_AMOUNT} mana" |
| `{MAX_CHAIN}` | `IChainCardConfig.MaxChain` | "up to {MAX_CHAIN} mines" |
| `{EXTRA_MOVES}` | `IExtraMovesCardConfig.ExtraMoves` | "+{EXTRA_MOVES} extra actions" |
| `{DRAW_COUNT}` | `IDrawCountCardConfig.DrawCount` | "Draws {DRAW_COUNT} cards" |
| `{MOVES_REDUCTION}` | `IMovesReductionCardConfig.MovesReduction` | "by {MOVES_REDUCTION}" |
| `{MANA_GAIN}` | `IManaGainCardConfig.ManaGain` | "+{MANA_GAIN} temporary mana" |
| `{HP_COST}` | `IHpCostCardConfig.HpCost` | "Sacrifice {HP_COST} HP" |
| `{WIN_MOVES}` | `ICoinTossCardConfig.WinMoves` | "grants +{WIN_MOVES} actions" |
| `{LOSE_MOVES}` | `ICoinTossCardConfig.LoseMoves` | "costs {LOSE_MOVES} action" |
| `{MIN_MANA}` | `IManaRangeCardConfig.MinMana` | "gain {MIN_MANA}-{MAX_MANA}" |
| `{MAX_MANA}` | `IManaRangeCardConfig.MaxMana` | "gain {MIN_MANA}-{MAX_MANA}" |
| `{DISCOUNT}` | `IDiscountCardConfig.Discount` | "costs {DISCOUNT} less mana" |
| `{COST_INCREASE}` | `ICostIncreaseCardConfig.CostIncrease` | "cost {COST_INCREASE} more mana" |
| `{WIN_DRAW}` | `IWinLoseDrawCardConfig.WinDraw` / `IGamblersRuinCardConfig.WinDraw` | "draws {WIN_DRAW} cards" |
| `{LOSE_RETURN}` | `IWinLoseDrawCardConfig.LoseReturn` | "returns {LOSE_RETURN} random cards" |
| `{WIN_MANA}` | `IGamblersRuinCardConfig.WinMana` | "+{WIN_MANA} mana" |
| `{LOSE_DISCARD}` | `IGamblersRuinCardConfig.LoseDiscard` | "discards {LOSE_DISCARD} random cards" |
| `{MIN_SIZE}` | `IRandomSizeCardConfig.MinSize` | "size ({MIN_SIZE}-{MAX_SIZE})" |
| `{MAX_SIZE}` | `IRandomSizeCardConfig.MaxSize` | "size ({MIN_SIZE}-{MAX_SIZE})" |
| `{MIN_LENGTH}` | `IRandomLengthCardConfig.MinLength` | "length {MIN_LENGTH}-{MAX_LENGTH}" |
| `{MAX_LENGTH}` | `IRandomLengthCardConfig.MaxLength` | "length {MIN_LENGTH}-{MAX_LENGTH}" |
| `{LENGTH}` | `ILengthCardConfig.Length` | "line of {LENGTH}" |
| `{MIN_MINES}` | `IMinesRangeCardConfig.MinMines` | "{MIN_MINES}-{MAX_MINES} mines" |
| `{MAX_MINES}` | `IMinesRangeCardConfig.MaxMines` | "{MIN_MINES}-{MAX_MINES} mines" |
| `{PEEK_COUNT}` | `IPeekCountCardConfig.PeekCount` | "Peek at {PEEK_COUNT} random cells" |

## Pipeline

1. `CardsRegistry` loads template descriptions from `cards-info.json`
2. `CardDescriptionProvider` iterates over `ICardConfigs.Value.All`
3. For each config, it checks all effect interfaces (`is IDurationalCardConfig`, `is IDamageCardConfig`, etc.)
4. If matched, it performs `string.Replace` on the template with the config value
5. UI consumers call `GetDescription(CardType)` instead of reading `ICardDefinition.Description` directly

## `_Normal` vs `_Max` config selection

Card `Use()` must pick the config by `payload.Type`. Never hardcode `_Normal`:

```csharp
var config = payload.Type == CardType.Blackout_Max
    ? _configs.Value.Blackout_Max
    : _configs.Value.Blackout_Normal;
```

`Smoke` / `Frost` / `FogOfWar` still hardcode `_Normal` — do not copy that pattern.

## Adding New Effect Types

1. Add a new interface in `shared/Configs/CardConfigOptions.cs`
2. Apply it to relevant card config classes
3. Add a new token constant and replace logic in `CardDescriptionProvider`
4. Update descriptions in `cards-info.json` to use the new token
5. Document the effect in this file
