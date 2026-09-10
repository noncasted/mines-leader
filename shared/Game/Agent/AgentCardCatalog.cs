using System.Collections.Generic;

namespace Shared
{
    /// <summary>
    /// Какие клетки внутри паттерна делают позицию легальной для карты.
    /// Повторяет фильтр, который карта применяет в Use: SelectTaken / SelectFree / SelectAll.
    /// </summary>
    public enum AgentCardCells
    {
        Any,
        Taken,
        Free,
    }

    public sealed class AgentCardInfo
    {
        public AgentCardInfo(string shape, AgentCardCells cells, string summary)
        {
            Shape = shape;
            Cells = cells;
            Summary = summary;
        }

        public string Shape { get; }
        public AgentCardCells Cells { get; }
        public string Summary { get; }
    }

    /// <summary>
    /// Описание карт для агента: форма паттерна и одна строка про эффект.
    /// Единственный источник правды для observation и legal plays. Размер паттерна
    /// берётся из конфига через <see cref="ResolveSize"/>.
    /// </summary>
#if UNITY_5_3_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public static class AgentCardCatalog
    {
        public const string ShapeRhombus = "Rhombus";
        public const string ShapeLine = "Line";
        public const string ShapeCross = "Cross";
        public const string ShapeChain = "Chain";
        public const string ShapeSingle = "Single";
        public const string ShapeNone = "None";

        public static readonly IReadOnlyList<string> Shapes = new[]
        {
            ShapeRhombus,
            ShapeLine,
            ShapeCross,
            ShapeChain,
            ShapeSingle,
            ShapeNone,
        };

        private static readonly AgentCardInfo _unknown = new AgentCardInfo(ShapeNone, AgentCardCells.Any, "Unknown card");
        private static readonly Dictionary<CardType, AgentCardInfo> _all = Build();

        public static IReadOnlyDictionary<CardType, AgentCardInfo> All => _all;

        public static AgentCardInfo Get(CardType type)
        {
            if (_all.TryGetValue(type, out var info))
                return info;

            return _unknown;
        }

        /// <summary>
        /// Размер паттерна из конфига. 0 когда у карты нет формы или конфиг не передан.
        /// Для случайных размеров отдаём максимум.
        /// </summary>
        public static int ResolveSize(CardType type, ICardConfig config)
        {
            if (config == null || Get(type).Shape == ShapeNone)
                return 0;

            return config switch
            {
                IAreaSizeCardConfig area => area.Size,
                ILengthCardConfig length => length.Length,
                IRandomSizeCardConfig randomSize => randomSize.MaxSize,
                IRandomLengthCardConfig randomLength => randomLength.MaxLength,
                IChainCardConfig chain => chain.SpawnSize,
                _ => 0,
            };
        }

        private static Dictionary<CardType, AgentCardInfo> Build()
        {
            var all = new Dictionary<CardType, AgentCardInfo>();

            // Own board: scouting.
            Add(all, ShapeRhombus, AgentCardCells.Taken,
                "Own board, diamond of closed cells: detonates mines inside without damage and opens every cell in the diamond.",
                CardType.Bloodhound, CardType.Bloodhound_Max);
            Add(all, ShapeRhombus, AgentCardCells.Taken,
                "Own board, diamond of closed cells: flags every unflagged mine inside, opens nothing. Fails if no unflagged mine is inside.",
                CardType.Sonar);
            Add(all, ShapeRhombus, AgentCardCells.Taken,
                "Own board, diamond of closed cells: highlights mines inside for this turn only, no flags.",
                CardType.ThermalVision, CardType.ThermalVision_Max);
            Add(all, ShapeRhombus, AgentCardCells.Taken,
                "Own board, random diamond 2-5 of closed cells: flags mines, opens safe cells.",
                CardType.ChaosDiamond);
            Add(all, ShapeCross, AgentCardCells.Taken,
                "Own board, cross of closed cells: flags mines, opens safe cells.",
                CardType.Excavator, CardType.Excavator_Max);
            Add(all, ShapeLine, AgentCardCells.Taken,
                "Own board, line of closed cells (the longer of horizontal / vertical through the click): flags mines, opens safe cells.",
                CardType.MinefieldScout, CardType.MinefieldScout_Max);
            Add(all, ShapeLine, AgentCardCells.Taken,
                "Own board, random line 3-7 of closed cells (longer orientation): flags mines, opens safe cells.",
                CardType.ChaosScout);
            Add(all, ShapeRhombus, AgentCardCells.Free,
                "Own board: needs an OPEN cell in the diamond around the click, then chain-defuses up to Size unflagged mines found in the search diamond (4) around the click and opens them. No extra card.",
                CardType.ZipZap, CardType.ZipZap_Max);
            Add(all, ShapeChain, AgentCardCells.Taken,
                "Own board: from a closed cell opens up to Size connected closed cells nearest to the click; mines inside detonate without damage.",
                CardType.ErosionDozer, CardType.ErosionDozer_Max);

            // Enemy board: attack.
            Add(all, ShapeRhombus, AgentCardCells.Free,
                "Enemy board, diamond of OPEN cells: closes them again and plants mines on the row edges. Needs open enemy cells inside.",
                CardType.Trebuchet, CardType.Trebuchet_Max);
            Add(all, ShapeCross, AgentCardCells.Free,
                "Enemy board, cross of OPEN cells: closes them and plants a mine in each. Needs open enemy cells inside.",
                CardType.MineCluster, CardType.MineCluster_Max);
            Add(all, ShapeLine, AgentCardCells.Free,
                "Enemy board, line of OPEN cells (longer orientation): closes them and plants mines. Needs open enemy cells inside.",
                CardType.CarpetBomb, CardType.CarpetBomb_Max);
            Add(all, ShapeRhombus, AgentCardCells.Free,
                "Enemy board, random diamond 1-4 of OPEN cells: closes them and plants mines. Needs open enemy cells inside.",
                CardType.FortuneBlast);
            Add(all, ShapeChain, AgentCardCells.Taken,
                "Enemy board: click a closed cell that holds a mine (enemy flags are the best guess); spawns mines in a diamond of Size around up to 3 chained mines. Fails if the cell has no mine.",
                CardType.ChainReaction);
            Add(all, ShapeSingle, AgentCardCells.Taken,
                "Enemy board: opens one closed unflagged enemy cell; if it hides a mine the enemy takes 1 damage.",
                CardType.OpponentBomb);
            Add(all, ShapeRhombus, AgentCardCells.Taken,
                "Enemy board, diamond of closed cells: removes every enemy flag inside.",
                CardType.OpponentFlagErase, CardType.OpponentFlagErase_Max);
            Add(all, ShapeRhombus, AgentCardCells.Taken,
                "Enemy board, diamond of closed cells: moves each enemy flag inside to a random unflagged closed cell of the diamond.",
                CardType.OpponentFlagReshuffle, CardType.OpponentFlagReshuffle_Max);
            Add(all, ShapeRhombus, AgentCardCells.Free,
                "Enemy board, diamond of OPEN cells: hides their numbers for 2 turns.",
                CardType.FogOfWar, CardType.FogOfWar_Max);
            Add(all, ShapeRhombus, AgentCardCells.Any,
                "Enemy board, diamond (any cells): covers it with fog for 3 turns, enemy sees no numbers there.",
                CardType.Smoke, CardType.Smoke_Max);
            Add(all, ShapeRhombus, AgentCardCells.Any,
                "Enemy board, random diamond 1-4 (any cells): fog for 3 turns.",
                CardType.ChaosFog);
            Add(all, ShapeRhombus, AgentCardCells.Any,
                "Enemy board, diamond (any cells): freezes it so the enemy cannot open those cells for the duration.",
                CardType.Frost, CardType.Frost_Max);
            Add(all, ShapeRhombus, AgentCardCells.Any,
                "Enemy board, diamond (any cells): blackout hides numbers there for the duration.",
                CardType.Blackout, CardType.Blackout_Max);
            Add(all, ShapeRhombus, AgentCardCells.Any,
                "Swaps the diamond area at the same coordinates between your board and the enemy board (cells, mines, flags).",
                CardType.DimensionRift);

            // No position: self.
            Add(all, ShapeNone, AgentCardCells.Any, "Next Trebuchet gets +2 size. Stacks.",
                CardType.TrebuchetAimer, CardType.TrebuchetAimer_Max);
            Add(all, ShapeNone, AgentCardCells.Any, "Returns the last discarded card to your hand.", CardType.Gravedigger);
            Add(all, ShapeNone, AgentCardCells.Any, "+1 HP, up to max.", CardType.Medic);
            Add(all, ShapeNone, AgentCardCells.Any, "+2 moves this turn.", CardType.Overclock);
            Add(all, ShapeNone, AgentCardCells.Any, "Draw 2 cards.", CardType.Scavenger);
            Add(all, ShapeNone, AgentCardCells.Any, "Removes enemy effects (fog, frost, blackout) from your board.", CardType.Purge);
            Add(all, ShapeNone, AgentCardCells.Any, "+1 move this turn.", CardType.Adrenaline);
            Add(all, ShapeNone, AgentCardCells.Any, "+3 temporary mana this turn.", CardType.ManaSurge);
            Add(all, ShapeNone, AgentCardCells.Any, "-1 HP, +3 mana, +2 moves.", CardType.BloodPact);
            Add(all, ShapeNone, AgentCardCells.Any, "Coin flip: +2 moves or -1 move.", CardType.CoinToss);
            Add(all, ShapeNone, AgentCardCells.Any, "Random 1-5 mana.", CardType.ManaFountain);
            Add(all, ShapeNone, AgentCardCells.Any, "Next card costs 1 less.", CardType.Focus);
            Add(all, ShapeNone, AgentCardCells.Any, "Absorbs the next mine hit on your board.", CardType.Shield);
            Add(all, ShapeNone, AgentCardCells.Any, "All cards cost 1 less this turn.", CardType.PowerSurge);
            Add(all, ShapeNone, AgentCardCells.Any, "Discard 1 chosen hand card (extra_card_id), draw 2.", CardType.Recycler);
            Add(all, ShapeNone, AgentCardCells.Any, "Coin flip: draw 2 or return 2 random hand cards to the deck.", CardType.MysticDraw);
            Add(all, ShapeNone, AgentCardCells.Any, "Coin flip: double current mana or set it to 0.", CardType.DoubleOrNothing);
            Add(all, ShapeNone, AgentCardCells.Any, "Coin flip: +3 cards and +2 mana, or discard 2 random cards.", CardType.GamblersRuin);
            Add(all, ShapeNone, AgentCardCells.Any, "Highlights 1-3 random hidden mines on your board. Fails if none are hidden.", CardType.FortuneCookie);
            Add(all, ShapeNone, AgentCardCells.Any, "Look at the top 3 deck cards, take one (chosen_index 0-2).", CardType.Salvage);
            Add(all, ShapeNone, AgentCardCells.Any, "Does nothing and cannot be paid for (cost 99).", CardType.Dud);
            Add(all, ShapeNone, AgentCardCells.Any, "Copies the last card the enemy played and casts it.", CardType.MirrorMatch);

            // No position: opponent.
            Add(all, ShapeNone, AgentCardCells.Any, "Enemy max mana -1, your max mana +1.", CardType.Siphon);
            Add(all, ShapeNone, AgentCardCells.Any, "Enemy hand goes back to the deck, shuffles and redraws.", CardType.HandScramble);
            Add(all, ShapeNone, AgentCardCells.Any, "Enemy gets -1 move for 2 turns.", CardType.Lockdown);
            Add(all, ShapeNone, AgentCardCells.Any, "Enemy cards cost +1 mana on their next turn.", CardType.Embargo);
            Add(all, ShapeNone, AgentCardCells.Any, "Steals a random card from the enemy hand. Fails if it is empty.", CardType.CardThief);
            Add(all, ShapeNone, AgentCardCells.Any, "Shuffles a Dud into the enemy deck.", CardType.SabotageDeck);
            Add(all, ShapeNone, AgentCardCells.Any, "For 2 turns mine damage you take is reflected to the enemy.", CardType.SoulLink);

            return all;
        }

        private static void Add(
            Dictionary<CardType, AgentCardInfo> all,
            string shape,
            AgentCardCells cells,
            string summary,
            params CardType[] types)
        {
            var info = new AgentCardInfo(shape, cells, summary);

            foreach (var type in types)
                all.Add(type, info);
        }
    }
}
