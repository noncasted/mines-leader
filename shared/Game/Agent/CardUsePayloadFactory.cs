using System;

namespace Shared
{
    public static class CardUsePayloadFactory
    {
        public static ICardUsePayload Create(
            CardType type,
            Position? position,
            Guid? extraCardId,
            int? chosenIndex)
        {
            var payload = CreatePayload(type);
            payload.Type = type;

            if (payload is IBoardCardUsePayload boardPayload)
            {
                if (position.HasValue == false)
                    throw new ArgumentException("position required");

                boardPayload.Position = position.Value;
            }

            switch (payload)
            {
                case CardUsePayload.Recycler recycler:
                    if (extraCardId.HasValue == false)
                        throw new ArgumentException("extraCardId required");

                    recycler.DiscardCardId = extraCardId.Value;
                    break;
                case CardUsePayload.Salvage salvage:
                    salvage.ChosenIndex = chosenIndex ?? 0;
                    break;
                case CardUsePayload.ZipZap zipZap:
                    zipZap.CardId = extraCardId ?? Guid.Empty;
                    break;
            }

            return payload;
        }

        private static ICardUsePayload CreatePayload(CardType type)
        {
            return type switch
            {
                CardType.Trebuchet or CardType.Trebuchet_Max => new CardUsePayload.Trebuchet(),
                CardType.Bloodhound or CardType.Bloodhound_Max => new CardUsePayload.Bloodhound(),
                CardType.TrebuchetAimer or CardType.TrebuchetAimer_Max => new CardUsePayload.TrebuchetAimer(),
                CardType.ErosionDozer or CardType.ErosionDozer_Max => new CardUsePayload.ErosionDozer(),
                CardType.Gravedigger => new CardUsePayload.Gravedigger(),
                CardType.ZipZap or CardType.ZipZap_Max => new CardUsePayload.ZipZap(),
                CardType.OpponentBomb => new CardUsePayload.OpponentBomb(),
                CardType.OpponentFlagErase or CardType.OpponentFlagErase_Max => new CardUsePayload.OpponentFlagErase(),
                CardType.OpponentFlagReshuffle or CardType.OpponentFlagReshuffle_Max =>
                    new CardUsePayload.OpponentFlagReshuffle(),
                CardType.Smoke or CardType.Smoke_Max => new CardUsePayload.Smoke(),
                CardType.Medic => new CardUsePayload.Medic(),
                CardType.MinefieldScout or CardType.MinefieldScout_Max => new CardUsePayload.MinefieldScout(),
                CardType.Siphon => new CardUsePayload.Siphon(),
                CardType.ChainReaction => new CardUsePayload.ChainReaction(),
                CardType.Overclock => new CardUsePayload.Overclock(),
                CardType.FogOfWar or CardType.FogOfWar_Max => new CardUsePayload.FogOfWar(),
                CardType.Scavenger => new CardUsePayload.Scavenger(),
                CardType.HandScramble => new CardUsePayload.HandScramble(),
                CardType.Lockdown => new CardUsePayload.Lockdown(),
                CardType.Sonar => new CardUsePayload.Sonar(),
                CardType.Purge => new CardUsePayload.Purge(),
                CardType.Adrenaline => new CardUsePayload.Adrenaline(),
                CardType.ManaSurge => new CardUsePayload.ManaSurge(),
                CardType.BloodPact => new CardUsePayload.BloodPact(),
                CardType.CoinToss => new CardUsePayload.CoinToss(),
                CardType.ManaFountain => new CardUsePayload.ManaFountain(),
                CardType.Focus => new CardUsePayload.Focus(),
                CardType.Shield => new CardUsePayload.Shield(),
                CardType.PowerSurge => new CardUsePayload.PowerSurge(),
                CardType.Embargo => new CardUsePayload.Embargo(),
                CardType.Recycler => new CardUsePayload.Recycler(),
                CardType.MysticDraw => new CardUsePayload.MysticDraw(),
                CardType.DoubleOrNothing => new CardUsePayload.DoubleOrNothing(),
                CardType.GamblersRuin => new CardUsePayload.GamblersRuin(),
                CardType.Excavator or CardType.Excavator_Max => new CardUsePayload.Excavator(),
                CardType.ThermalVision or CardType.ThermalVision_Max => new CardUsePayload.ThermalVision(),
                CardType.ChaosDiamond => new CardUsePayload.ChaosDiamond(),
                CardType.ChaosScout => new CardUsePayload.ChaosScout(),
                CardType.MineCluster or CardType.MineCluster_Max => new CardUsePayload.MineCluster(),
                CardType.CarpetBomb or CardType.CarpetBomb_Max => new CardUsePayload.CarpetBomb(),
                CardType.FortuneBlast => new CardUsePayload.FortuneBlast(),
                CardType.ChaosFog => new CardUsePayload.ChaosFog(),
                CardType.Frost or CardType.Frost_Max => new CardUsePayload.Frost(),
                CardType.Blackout or CardType.Blackout_Max => new CardUsePayload.Blackout(),
                CardType.FortuneCookie => new CardUsePayload.FortuneCookie(),
                CardType.Salvage => new CardUsePayload.Salvage(),
                CardType.CardThief => new CardUsePayload.CardThief(),
                CardType.SabotageDeck => new CardUsePayload.SabotageDeck(),
                CardType.Dud => new CardUsePayload.Dud(),
                CardType.SoulLink => new CardUsePayload.SoulLink(),
                CardType.MirrorMatch => new CardUsePayload.MirrorMatch(),
                CardType.DimensionRift => new CardUsePayload.DimensionRift(),
                _ => throw new ArgumentException($"unknown card type {type}"),
            };
        }
    }
}
