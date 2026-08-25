using System;
using System.Collections.Generic;
using Shared;
using Tools;
using UnityEngine;

namespace Meta
{
    public interface ICardsRegistry
    {
        IReadOnlyDictionary<CardType, ICardDefinition> Entries { get; }
        Sprite GetModifierSprite(PlayerModifier modifier);
    }

    public class CardsRegistry : ICardsRegistry
    {
        public CardsRegistry()
        {
            Load();
        }
        
        private readonly Dictionary<CardType, ICardDefinition> _cards = new();

        public IReadOnlyDictionary<CardType, ICardDefinition> Entries => _cards;

        public Sprite GetModifierSprite(PlayerModifier modifier)
        {
            return ModifierToSprite(modifier);
        }

        private void Load()
        {
            var textAsset = Resources.Load<TextAsset>("cards-info");
            var payload = JsonUtility.FromJson<CardsInfoPayload>(textAsset.text);

            foreach (var entry in payload.cards)
            {
                var type = (CardType)Enum.Parse(typeof(CardType), entry.type);
                var definition = new CardDefinition(type, entry.name, entry.description, TypeToSprite(type));
                _cards[type] = definition;
            }
        }
        
        private Sprite TypeToSprite(CardType type)
        {
            return type switch
            {
                CardType.Trebuchet => Sprites.CardsIcons.Trebuchet,
                CardType.Trebuchet_Max => Sprites.CardsIcons.TrebuchetMax,
                CardType.Bloodhound => Sprites.CardsIcons.Bloodhound,
                CardType.Bloodhound_Max => Sprites.CardsIcons.BloodhoundMax,
                CardType.TrebuchetAimer => Sprites.CardsIcons.TrebuchetAimer,
                CardType.TrebuchetAimer_Max => Sprites.CardsIcons.TrebuchetAimerMax,
                CardType.ErosionDozer => Sprites.CardsIcons.ErosionDozer,
                CardType.ErosionDozer_Max => Sprites.CardsIcons.ErosionDozerMax,
                CardType.Gravedigger => Sprites.CardsIcons.Gravedigger,
                CardType.ZipZap => Sprites.CardsIcons.ZipZap,
                CardType.ZipZap_Max => Sprites.CardsIcons.ZipZapMax,
                CardType.OpponentBomb => Sprites.CardsIcons.OpponentBomb,
                CardType.OpponentFlagErase => Sprites.CardsIcons.OpponentFlagErase,
                CardType.OpponentFlagErase_Max => Sprites.CardsIcons.OpponentFlagEraseMax,
                CardType.OpponentFlagReshuffle => Sprites.CardsIcons.OpponentFlagReshuffle,
                CardType.OpponentFlagReshuffle_Max => Sprites.CardsIcons.OpponentFlagReshuffleMax,
                CardType.Smoke => Sprites.CardsIcons.Smoke,
                CardType.Smoke_Max => Sprites.CardsIcons.SmokeMax,
                CardType.Medic => Sprites.CardsIcons.Medic,
                CardType.MinefieldScout => Sprites.CardsIcons.MinefieldScout,
                CardType.MinefieldScout_Max => Sprites.CardsIcons.MinefieldScoutMax,
                CardType.Siphon => Sprites.CardsIcons.Siphon,
                CardType.ChainReaction => Sprites.CardsIcons.ChainReaction,
                CardType.Overclock => Sprites.CardsIcons.Overclock,
                CardType.FogOfWar => Sprites.CardsIcons.FogOfWar,
                CardType.FogOfWar_Max => Sprites.CardsIcons.FogOfWarMax,
                CardType.Scavenger => Sprites.CardsIcons.Scavenger,
                CardType.HandScramble => Sprites.CardsIcons.HandScramble,
                CardType.Lockdown => Sprites.CardsIcons.Lockdown,
                CardType.Sonar => Sprites.CardsIcons.Sonar,
                CardType.Purge => Sprites.CardsIcons.Purge,
                CardType.Adrenaline => Sprites.CardsIcons.Adrenaline,
                CardType.ManaSurge => Sprites.CardsIcons.ManaSurge,
                CardType.BloodPact => Sprites.CardsIcons.Devil,
                CardType.CoinToss => Sprites.CardsIcons.CoinFlip,
                CardType.ManaFountain => Sprites.CardsIcons.ManaFontain,
                CardType.Focus => Sprites.CardsIcons.Focus,
                CardType.Shield => Sprites.CardsIcons.Shield,
                CardType.PowerSurge => Sprites.CardsIcons.PowerSurge,
                CardType.Embargo => Sprites.CardsIcons.Embargo,
                CardType.Recycler => Sprites.CardsIcons.Recycler,
                CardType.MysticDraw => Sprites.CardsIcons.MysticDraw,
                CardType.DoubleOrNothing => Sprites.CardsIcons.DoubleOrNothing,
                CardType.GamblersRuin => Sprites.CardsIcons.GamblersRuin,
                CardType.Excavator => Sprites.CardsIcons.Excavator,
                CardType.Excavator_Max => Sprites.CardsIcons.ExcavatorMax,
                CardType.ThermalVision => Sprites.CardsIcons.ThermalVision,
                CardType.ThermalVision_Max => Sprites.CardsIcons.ThermalVisionMax,
                CardType.ChaosDiamond => Sprites.CardsIcons.ChaosDiamond,
                CardType.ChaosScout => Sprites.CardsIcons.ChaosScout,
                CardType.MineCluster => Sprites.CardsIcons.MineCluster,
                CardType.MineCluster_Max => Sprites.CardsIcons.MineClusterMax,
                CardType.CarpetBomb => Sprites.CardsIcons.CarpetBomb,
                CardType.CarpetBomb_Max => Sprites.CardsIcons.CarpetBombMax,
                CardType.FortuneBlast => Sprites.CardsIcons.FortuneBlast,
                CardType.ChaosFog => Sprites.CardsIcons.ChaosFog,
                CardType.Frost => Sprites.CardsIcons.Frost,
                CardType.Frost_Max => Sprites.CardsIcons.FrostMax,
                CardType.Blackout => Sprites.CardsIcons.Blackout,
                CardType.Blackout_Max => Sprites.CardsIcons.BlackoutMax,
                CardType.FortuneCookie => Sprites.CardsIcons.FortuneCookie,
                CardType.Salvage => Sprites.CardsIcons.Salvage,
                CardType.CardThief => Sprites.CardsIcons.CardThief,
                CardType.SabotageDeck => Sprites.CardsIcons.SabotageDeck,
                CardType.Dud => Sprites.CardsIcons.Dud,
                CardType.SoulLink => Sprites.CardsIcons.SoulLink,
                CardType.MirrorMatch => Sprites.CardsIcons.MirrorMatch,
                CardType.DimensionRift => Sprites.CardsIcons.DimensionRift,
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };
        }

        private Sprite ModifierToSprite(PlayerModifier modifier)
        {
            return modifier switch
            {
                PlayerModifier.TrebuchetBoost => Sprites.CardsIcons.TrebuchetAimer,
                PlayerModifier.AdditionalMana => Sprites.CardBuffs.ManaSurge,
                PlayerModifier.AdditionalMoves => Sprites.CardBuffs.Adrenaline,
                PlayerModifier.AdditionalHealth => Sprites.CardsIcons.Medic,
                PlayerModifier.NextCardDiscount => Sprites.CardBuffs.Focus,
                PlayerModifier.AllCardsDiscount => Sprites.CardBuffs.PowerSurge,
                PlayerModifier.ManaCostPenalty => Sprites.CardBuffs.Embargo,
                PlayerModifier.Shield => Sprites.CardsIcons.Shield,
                PlayerModifier.SoulLink => Sprites.CardBuffs.SoulLink,
                _ => throw new ArgumentOutOfRangeException(nameof(modifier), modifier, null)
            };
        }
    }
}