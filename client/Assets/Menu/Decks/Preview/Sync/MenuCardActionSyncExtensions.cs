using GamePlay.Cards;
using Internal;
using Shared;

namespace Menu.Decks
{
    public static class MenuCardActionSyncExtensions
    {
        public static void AddMenuCardActionSyncs(this IBuilder builder)
        {
            builder.AddCardActionSyncResolver<CardTrebuchetAction.Snapshot, CardActionSnapshot.Trebuchet>();
            builder.AddCardActionSyncResolver<CardBloodhoundAction.Snapshot, CardActionSnapshot.Bloodhound>();
            builder.AddCardActionSyncResolver<CardTrebuchetAimerAction.Snapshot, CardActionSnapshot.TrebuchetAimer>();
            builder.AddCardActionSyncResolver<CardErosionDozerAction.Snapshot, CardActionSnapshot.ErosionDozer>();
            builder.AddCardActionSyncResolver<CardGravediggerAction.Snapshot, CardActionSnapshot.Gravedigger>();
            builder.AddCardActionSyncResolver<CardOpponentBombAction.Snapshot, CardActionSnapshot.OpponentBomb>();
            builder.AddCardActionSyncResolver<CardOpponentFlagEraseAction.Snapshot, CardActionSnapshot.OpponentFlagErase>();
            builder.AddCardActionSyncResolver<CardOpponentFlagReshuffleAction.Snapshot, CardActionSnapshot.OpponentFlagReshuffle>();
            builder.AddCardActionSyncResolver<CardSmokeAction.Snapshot, CardActionSnapshot.Smoke>();
            builder.AddCardActionSyncResolver<CardMedicAction.Snapshot, CardActionSnapshot.Medic>();
            builder.AddCardActionSyncResolver<CardMinefieldScoutAction.Snapshot, CardActionSnapshot.MinefieldScout>();
            builder.AddCardActionSyncResolver<CardSiphonAction.Snapshot, CardActionSnapshot.Siphon>();
            builder.AddCardActionSyncResolver<CardChainReactionAction.Snapshot, CardActionSnapshot.ChainReaction>();
            builder.AddCardActionSyncResolver<CardOverclockAction.Snapshot, CardActionSnapshot.Overclock>();
            builder.AddCardActionSyncResolver<CardFogOfWarAction.Snapshot, CardActionSnapshot.FogOfWar>();
            builder.AddCardActionSyncResolver<CardScavengerAction.Snapshot, CardActionSnapshot.Scavenger>();
            builder.AddCardActionSyncResolver<CardHandScrambleAction.Snapshot, CardActionSnapshot.HandScramble>();
            builder.AddCardActionSyncResolver<CardLockdownAction.Snapshot, CardActionSnapshot.Lockdown>();
            builder.AddCardActionSyncResolver<CardSonarAction.Snapshot, CardActionSnapshot.Sonar>();
            builder.AddCardActionSyncResolver<CardPurgeAction.Snapshot, CardActionSnapshot.Purge>();
            builder.AddCardActionSyncResolver<CardAdrenalineAction.Snapshot, CardActionSnapshot.Adrenaline>();
            builder.AddCardActionSyncResolver<CardManaSurgeAction.Snapshot, CardActionSnapshot.ManaSurge>();
            builder.AddCardActionSyncResolver<CardBloodPactAction.Snapshot, CardActionSnapshot.BloodPact>();
            builder.AddCardActionSyncResolver<CardCoinTossAction.Snapshot, CardActionSnapshot.CoinToss>();
            builder.AddCardActionSyncResolver<CardManaFountainAction.Snapshot, CardActionSnapshot.ManaFountain>();
            builder.AddCardActionSyncResolver<CardFocusAction.Snapshot, CardActionSnapshot.Focus>();
            builder.AddCardActionSyncResolver<CardShieldAction.Snapshot, CardActionSnapshot.Shield>();
            builder.AddCardActionSyncResolver<CardPowerSurgeAction.Snapshot, CardActionSnapshot.PowerSurge>();
            builder.AddCardActionSyncResolver<CardEmbargoAction.Snapshot, CardActionSnapshot.Embargo>();
            builder.AddCardActionSyncResolver<CardRecyclerAction.Snapshot, CardActionSnapshot.Recycler>();
            builder.AddCardActionSyncResolver<CardMysticDrawAction.Snapshot, CardActionSnapshot.MysticDraw>();
            builder.AddCardActionSyncResolver<CardDoubleOrNothingAction.Snapshot, CardActionSnapshot.DoubleOrNothing>();
            builder.AddCardActionSyncResolver<CardGamblersRuinAction.Snapshot, CardActionSnapshot.GamblersRuin>();
            builder.AddCardActionSyncResolver<CardExcavatorAction.Snapshot, CardActionSnapshot.Excavator>();
            builder.AddCardActionSyncResolver<CardThermalVisionAction.Snapshot, CardActionSnapshot.ThermalVision>();
            builder.AddCardActionSyncResolver<CardChaosDiamondAction.Snapshot, CardActionSnapshot.ChaosDiamond>();
            builder.AddCardActionSyncResolver<CardChaosScoutAction.Snapshot, CardActionSnapshot.ChaosScout>();
            builder.AddCardActionSyncResolver<CardMineClusterAction.Snapshot, CardActionSnapshot.MineCluster>();
            builder.AddCardActionSyncResolver<CardCarpetBombAction.Snapshot, CardActionSnapshot.CarpetBomb>();
            builder.AddCardActionSyncResolver<CardFortuneBlastAction.Snapshot, CardActionSnapshot.FortuneBlast>();
            builder.AddCardActionSyncResolver<CardChaosFogAction.Snapshot, CardActionSnapshot.ChaosFog>();
            builder.AddCardActionSyncResolver<CardFrostAction.Snapshot, CardActionSnapshot.Frost>();
            builder.AddCardActionSyncResolver<CardBlackoutAction.Snapshot, CardActionSnapshot.Blackout>();
            builder.AddCardActionSyncResolver<CardFortuneCookieAction.Snapshot, CardActionSnapshot.FortuneCookie>();
            builder.AddCardActionSyncResolver<CardSalvageAction.Snapshot, CardActionSnapshot.Salvage>();
            builder.AddCardActionSyncResolver<CardCardThiefAction.Snapshot, CardActionSnapshot.CardThief>();
            builder.AddCardActionSyncResolver<CardSabotageDeckAction.Snapshot, CardActionSnapshot.SabotageDeck>();
            builder.AddCardActionSyncResolver<CardDudAction.Snapshot, CardActionSnapshot.Dud>();
            builder.AddCardActionSyncResolver<CardSoulLinkAction.Snapshot, CardActionSnapshot.SoulLink>();
            builder.AddCardActionSyncResolver<CardMirrorMatchAction.Snapshot, CardActionSnapshot.MirrorMatch>();
            builder.AddCardActionSyncResolver<CardDimensionRiftAction.Snapshot, CardActionSnapshot.DimensionRift>();
        }
    }
}
