using System.Globalization;
using Shared;

namespace Game.Session;

/// <summary>
/// Why the modifier left the list of active ones.
/// </summary>
public enum ModifierEndReason
{
    /// <summary>The duration expired (the moves ran out).</summary>
    Expired,

    /// <summary>The effect was spent: a shield absorbed a mine, a discount went into a card payment, etc.</summary>
    Consumed,

    /// <summary>The effect was removed forcibly (dispel, end of match, overwrite).</summary>
    Removed
}

/// <summary>
/// Human-readable descriptions of buffs/debuffs for the game log: what the effect is,
/// where it came from, how long it lives and what exactly it gives the player.
/// </summary>
public static class ModifierLogDescriptions
{
    /// <summary>
    /// Buff or debuff: depends not only on the type but on the sign of the value
    /// (for example AdditionalMoves with a minus is Lockdown, i.e. a debuff).
    /// </summary>
    public static bool IsHarmful(PlayerModifier type, float value)
    {
        return type switch
        {
            PlayerModifier.ManaCostPenalty => value > 0,
            PlayerModifier.SoulLink => false,
            PlayerModifier.Shield => value < 0,
            _ => value < 0
        };
    }

    public static string Kind(PlayerModifier type, float value)
    {
        return IsHarmful(type, value) ? "Debuff" : "Buff";
    }

    /// <summary>Short name of the effect for the log line.</summary>
    public static string EffectName(PlayerModifier type)
    {
        return type switch
        {
            PlayerModifier.TrebuchetBoost => "TrebuchetBoost",
            PlayerModifier.AdditionalMana => "MaxMana",
            PlayerModifier.AdditionalMoves => "Moves",
            PlayerModifier.AdditionalHealth => "MaxHealth",
            PlayerModifier.NextCardDiscount => "NextCardDiscount",
            PlayerModifier.AllCardsDiscount => "AllCardsDiscount",
            PlayerModifier.ManaCostPenalty => "ManaCostPenalty",
            PlayerModifier.Shield => "Shield",
            PlayerModifier.SoulLink => "SoulLink",
            PlayerModifier.BaseHealth => "BaseHealth",
            PlayerModifier.BaseMoves => "BaseMoves",
            PlayerModifier.BaseMana => "BaseMana",
            PlayerModifier.BaseManaAddPerRound => "BaseManaAddPerRound",
            _ => type.ToString()
        };
    }

    /// <summary>The card/mechanic that granted the effect (the modifier source key).</summary>
    public static string Source(string key)
    {
        if (string.IsNullOrEmpty(key))
            return "Unknown";

        return key switch
        {
            "adrenaline" => "Adrenaline",
            "overclock" => "Overclock",
            "cointoss" => "CoinToss",
            "lockdown" => "Lockdown",
            "focus" => "Focus",
            "powersurge" => "PowerSurge",
            "shield" => "Shield",
            "soul_link" => "SoulLink",
            "embargo" => "Embargo",
            "blood_pact" => "BloodPact",
            "double_or_nothing" => "DoubleOrNothing",
            "gamblers_ruin" => "GamblersRuin",
            "mana_fountain" => "ManaFountain",
            "mana_surge" => "ManaSurge",
            "trebuchet_aimer" => "TrebuchetAimer",
            "base_health" => "ModeConfig(BaseHealth)",
            "base_moves" => "ModeConfig(BaseMoves)",
            "base_mana" => "ModeConfig(BaseMana)",
            "base_mana_add_per_round" => "ModeConfig(BaseManaAddPerRound)",
            _ => key
        };
    }

    /// <summary>Duration: -1 means until removed/spent, otherwise how many moves are left.</summary>
    public static string Duration(int turnsToEnd)
    {
        if (turnsToEnd < 0)
            return "until consumed";

        if (turnsToEnd == 0)
            return "ended";

        return turnsToEnd == 1 ? "1 turn left" : $"{turnsToEnd} turns left";
    }

    public static string Amount(float value)
    {
        var sign = value >= 0 ? "+" : "";
        return sign + value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Detailed explanation: what exactly the effect does to the player it is attached to.
    /// </summary>
    public static string Explain(PlayerModifier type, float value)
    {
        var abs = Math.Abs(value).ToString("0.##", CultureInfo.InvariantCulture);
        var positive = value >= 0;

        return type switch
        {
            PlayerModifier.AdditionalMoves => positive
                ? $"player gets {abs} extra move(s) per turn on top of the base limit (more cell opens / card plays)"
                : $"player loses {abs} move(s) per turn, the turn ends sooner",

            PlayerModifier.AdditionalMana => positive
                ? $"max mana raised by {abs}, so the player can hold and spend {abs} more mana on cards"
                : $"max mana lowered by {abs}, fewer/cheaper cards are affordable",

            PlayerModifier.AdditionalHealth => positive
                ? $"max HP raised by {abs}, the player survives {abs} more mine hit(s)"
                : $"max HP lowered by {abs}, the player dies from fewer mine hits",

            PlayerModifier.NextCardDiscount => positive
                ? $"the very next played card costs {abs} less mana (cost floored at 0), then the effect is consumed"
                : $"the very next played card costs {abs} more mana, then the effect is consumed",

            PlayerModifier.AllCardsDiscount => positive
                ? $"every card played while active costs {abs} less mana (cost floored at 0)"
                : $"every card played while active costs {abs} more mana",

            PlayerModifier.ManaCostPenalty => positive
                ? $"every card played while active costs {abs} more mana"
                : $"every card played while active costs {abs} less mana",

            PlayerModifier.Shield => positive
                ? $"{abs} charge(s) that absorb a mine explosion: the mine still detonates, but no HP damage and no SoulLink backlash; one charge is spent per mine"
                : $"{abs} shield charge(s) taken away, mines start dealing HP damage again",

            PlayerModifier.SoulLink => positive
                ? "while active, every 1 HP of mine damage taken by this player is mirrored to the opponent"
                : "soul link weakened, mine damage is no longer mirrored to the opponent",

            PlayerModifier.TrebuchetBoost => positive
                ? $"the next Trebuchet strike hits a {abs}x{abs} area instead of the default one; the boost is reset right after that strike"
                : $"Trebuchet strike area reduced by {abs}",

            PlayerModifier.BaseHealth =>
                $"base HP from the mode config: max HP and current HP are set to {abs}",

            PlayerModifier.BaseMoves =>
                $"base moves per turn from the mode config: {abs} move(s) each turn",

            PlayerModifier.BaseMana =>
                $"base mana from the mode config: max mana and current mana are set to {abs}",

            PlayerModifier.BaseManaAddPerRound =>
                $"max mana grows by 1 at the end of every round up to the mode cap; {abs} gained so far",

            _ => $"modifier {type} changed by {Amount(value)}"
        };
    }

    /// <summary>Explanation of why the effect ended.</summary>
    public static string ExplainEnd(PlayerModifier type, ModifierEndReason reason)
    {
        return reason switch
        {
            ModifierEndReason.Expired => "duration ran out, the bonus no longer applies",
            ModifierEndReason.Consumed => type switch
            {
                PlayerModifier.Shield => "a mine explosion was absorbed, the charge is spent",
                PlayerModifier.NextCardDiscount => "the discount was applied to a card and is spent",
                PlayerModifier.TrebuchetBoost => "the boosted Trebuchet strike was fired, the boost is spent",
                _ => "the effect was used up"
            },
            _ => "the effect was removed"
        };
    }
}
