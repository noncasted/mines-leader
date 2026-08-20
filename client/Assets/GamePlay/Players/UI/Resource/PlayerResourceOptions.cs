using UnityEngine;

namespace GamePlay.Players.Resource
{
    public class PlayerResourceOptions
    {
        public PlayerResourceOptions(
            Sprite largeBaseFull,
            Sprite largeBaseEmpty,
            Sprite largeAdditionalFull,
            Sprite largeAdditionalEmpty,
            Sprite smallBaseFull,
            Sprite smallBaseEmpty,
            Sprite smallAdditionalFull,
            Sprite smallAdditionalEmpty)
        {
            LargeBaseFull = largeBaseFull;
            LargeBaseEmpty = largeBaseEmpty;
            LargeAdditionalFull = largeAdditionalFull;
            LargeAdditionalEmpty = largeAdditionalEmpty;
            SmallBaseFull = smallBaseFull;
            SmallBaseEmpty = smallBaseEmpty;
            SmallAdditionalFull = smallAdditionalFull;
            SmallAdditionalEmpty = smallAdditionalEmpty;
        }

        public Sprite LargeBaseFull { get; }
        public Sprite LargeBaseEmpty { get; }
        public Sprite LargeAdditionalFull { get; }
        public Sprite LargeAdditionalEmpty { get; }
        public Sprite SmallBaseFull { get; }
        public Sprite SmallBaseEmpty { get; }
        public Sprite SmallAdditionalFull { get; }
        public Sprite SmallAdditionalEmpty { get; }
    }
}