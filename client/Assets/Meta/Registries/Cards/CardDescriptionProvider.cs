using System;
using System.Collections.Generic;
using Shared;
using UnityEngine;

namespace Meta
{
    public interface ICardDescriptionProvider
    {
        string GetDescription(CardType type);
    }

    public class CardDescriptionProvider : ICardDescriptionProvider
    {
        public CardDescriptionProvider(ICardConfigs configs)
        {
            _configs = configs;
            LoadRawDescriptions();
        }

        private readonly ICardConfigs _configs;
        private readonly Dictionary<CardType, string> _rawDescriptions = new();
        private readonly Dictionary<CardType, string> _resolved = new();

        public string GetDescription(CardType type)
        {
            BuildIfNeeded();

            if (_resolved.TryGetValue(type, out var description))
                return description;

            if (_rawDescriptions.TryGetValue(type, out var rawDescription))
                return rawDescription;

            return string.Empty;
        }

        private void LoadRawDescriptions()
        {
            var textAsset = Resources.Load<TextAsset>("cards-info");
            var payload = JsonUtility.FromJson<CardsInfoPayload>(textAsset.text);

            foreach (var entry in payload.cards)
            {
                var type = (CardType)Enum.Parse(typeof(CardType), entry.type);
                _rawDescriptions[type] = entry.description;
            }
        }

        private void BuildIfNeeded()
        {
            var value = _configs.Value;

            if (value == null)
                return;

            foreach (var (type, config) in value.All)
            {
                if (!_rawDescriptions.TryGetValue(type, out var description))
                    continue;

                if (config is IDurationalCardConfig durational)
                    description = description.Replace("{ROUNDS}", durational.TurnsDuration.ToString());

                if (config is IDamageCardConfig damage)
                    description = description.Replace("{DAMAGE}", damage.Damage.ToString());

                if (config is IHealCardConfig heal)
                    description = description.Replace("{HEAL}", heal.Heal.ToString());

                if (config is IAreaSizeCardConfig areaSize)
                    description = description.Replace("{SIZE}", areaSize.Size.ToString());

                if (config is IDrainAmountCardConfig drain)
                    description = description.Replace("{DRAIN_AMOUNT}", drain.DrainAmount.ToString());

                if (config is IChainCardConfig chain)
                    description = description.Replace("{MAX_CHAIN}", chain.MaxChain.ToString());

                if (config is IExtraMovesCardConfig extraMoves)
                    description = description.Replace("{EXTRA_MOVES}", extraMoves.ExtraMoves.ToString());

                if (config is IDrawCountCardConfig draw)
                    description = description.Replace("{DRAW_COUNT}", draw.DrawCount.ToString());

                if (config is IMovesReductionCardConfig reduction)
                    description = description.Replace("{MOVES_REDUCTION}", reduction.MovesReduction.ToString());

                if (config is IManaGainCardConfig manaGain)
                    description = description.Replace("{MANA_GAIN}", manaGain.ManaGain.ToString());

                if (config is IHpCostCardConfig hpCost)
                    description = description.Replace("{HP_COST}", hpCost.HpCost.ToString());

                if (config is ICoinTossCardConfig coinToss)
                {
                    description = description.Replace("{WIN_MOVES}", coinToss.WinMoves.ToString());
                    description = description.Replace("{LOSE_MOVES}", coinToss.LoseMoves.ToString());
                }

                if (config is IManaRangeCardConfig manaRange)
                {
                    description = description.Replace("{MIN_MANA}", manaRange.MinMana.ToString());
                    description = description.Replace("{MAX_MANA}", manaRange.MaxMana.ToString());
                }

                if (config is IDiscountCardConfig discount)
                    description = description.Replace("{DISCOUNT}", discount.Discount.ToString());

                if (config is ICostIncreaseCardConfig costIncrease)
                    description = description.Replace("{COST_INCREASE}", costIncrease.CostIncrease.ToString());

                if (config is IWinLoseDrawCardConfig winLoseDraw)
                {
                    description = description.Replace("{WIN_DRAW}", winLoseDraw.WinDraw.ToString());
                    description = description.Replace("{LOSE_RETURN}", winLoseDraw.LoseReturn.ToString());
                }

                if (config is IGamblersRuinCardConfig gamblersRuin)
                {
                    description = description.Replace("{WIN_DRAW}", gamblersRuin.WinDraw.ToString());
                    description = description.Replace("{WIN_MANA}", gamblersRuin.WinMana.ToString());
                    description = description.Replace("{LOSE_DISCARD}", gamblersRuin.LoseDiscard.ToString());
                }

                if (config is IRandomSizeCardConfig randomSize)
                {
                    description = description.Replace("{MIN_SIZE}", randomSize.MinSize.ToString());
                    description = description.Replace("{MAX_SIZE}", randomSize.MaxSize.ToString());
                }

                if (config is IRandomLengthCardConfig randomLength)
                {
                    description = description.Replace("{MIN_LENGTH}", randomLength.MinLength.ToString());
                    description = description.Replace("{MAX_LENGTH}", randomLength.MaxLength.ToString());
                }

                if (config is ILengthCardConfig length)
                    description = description.Replace("{LENGTH}", length.Length.ToString());

                if (config is IMinesRangeCardConfig minesRange)
                {
                    description = description.Replace("{MIN_MINES}", minesRange.MinMines.ToString());
                    description = description.Replace("{MAX_MINES}", minesRange.MaxMines.ToString());
                }

                if (config is IPeekCountCardConfig peek)
                    description = description.Replace("{PEEK_COUNT}", peek.PeekCount.ToString());

                _resolved[type] = description;
            }
        }
    }
}