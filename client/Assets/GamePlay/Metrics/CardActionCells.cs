using System;
using System.Collections.Generic;
using Shared;

namespace GamePlay.Metrics
{
    // The cells of a played card, read from its action data: the cells it opened on a board and the cells it aimed at.
    // The actions without such cells give 0 opened and no target (-1).
    public static class CardActionCells
    {
        public const int Unknown = -1;

        // The cells the card opened on the board of boardOwner.
        public static int Opened(ICardActionData data, Guid boardOwner)
        {
            switch (data)
            {
                case CardActionSnapshot.MirrorMatch mirror:
                    return mirror.CopiedAction == null ? 0 : Opened(mirror.CopiedAction, boardOwner);
                case CardActionSnapshot.DimensionRift rift:
                    return OpenedByRift(rift, boardOwner);
            }

            if (data.TargetPlayer != boardOwner)
                return 0;

            return data switch
            {
                CardActionSnapshot.ZipZap card => Count(card.OpenedCells),
                CardActionSnapshot.Bloodhound card => Count(card.OpenedCells),
                CardActionSnapshot.ErosionDozer card => Count(card.OpenedCells),
                CardActionSnapshot.OpponentBomb card => Count(card.OpenedCells),
                CardActionSnapshot.Smoke card => Count(card.OpenedCells),
                CardActionSnapshot.MinefieldScout card => Count(card.OpenedCells),
                CardActionSnapshot.Excavator card => Count(card.OpenedCells),
                CardActionSnapshot.ChaosDiamond card => Count(card.OpenedCells),
                CardActionSnapshot.ChaosScout card => Count(card.OpenedCells),
                _ => 0
            };
        }

        // The cells the card aimed at, Unknown for a card without a target on a board.
        public static int Targeted(ICardActionData data)
        {
            return data switch
            {
                CardActionSnapshot.MirrorMatch mirror => mirror.CopiedAction == null
                    ? Unknown
                    : Targeted(mirror.CopiedAction),
                CardActionSnapshot.ZipZap card => Count(card.TargetCells),
                CardActionSnapshot.Bloodhound card => Count(card.TargetCells),
                CardActionSnapshot.ErosionDozer card => Count(card.TargetCells),
                CardActionSnapshot.Trebuchet card => Count(card.TargetCells),
                CardActionSnapshot.OpponentBomb card => Count(card.TargetCells),
                CardActionSnapshot.OpponentFlagErase card => Count(card.TargetCells),
                CardActionSnapshot.OpponentFlagReshuffle card => Count(card.TargetCells),
                CardActionSnapshot.Smoke card => Count(card.TargetCells),
                CardActionSnapshot.MinefieldScout card => Count(card.TargetCells),
                CardActionSnapshot.ChainReaction card => Count(card.TargetCells),
                CardActionSnapshot.FogOfWar card => Count(card.TargetCells),
                CardActionSnapshot.Sonar card => Count(card.TargetCells),
                CardActionSnapshot.Excavator card => Count(card.TargetCells),
                CardActionSnapshot.ThermalVision card => Count(card.TargetCells),
                CardActionSnapshot.ChaosDiamond card => Count(card.TargetCells),
                CardActionSnapshot.ChaosScout card => Count(card.TargetCells),
                CardActionSnapshot.MineCluster card => Count(card.TargetCells),
                CardActionSnapshot.CarpetBomb card => Count(card.TargetCells),
                CardActionSnapshot.FortuneBlast card => Count(card.TargetCells),
                CardActionSnapshot.ChaosFog card => Count(card.TargetCells),
                CardActionSnapshot.Frost card => Count(card.TargetCells),
                CardActionSnapshot.Blackout card => Count(card.TargetCells),
                CardActionSnapshot.FortuneCookie card => Count(card.TargetCells),
                CardActionSnapshot.DimensionRift card => Count(card.TargetCells),
                _ => Unknown
            };
        }

        private static int OpenedByRift(CardActionSnapshot.DimensionRift rift, Guid boardOwner)
        {
            var opened = 0;

            if (rift.OwnerPlayer == boardOwner)
                opened += Count(rift.OwnerOpenedCells);

            if (rift.TargetPlayer == boardOwner)
                opened += Count(rift.TargetOpenedCells);

            return opened;
        }

        // A list the server did not fill comes as null.
        private static int Count<T>(IReadOnlyList<T> cells)
        {
            return cells?.Count ?? 0;
        }
    }
}
