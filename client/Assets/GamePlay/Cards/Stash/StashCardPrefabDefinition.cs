#if UNITY_EDITOR
using Tools;
using Tools.DI;
using Tools.Objects;
using UnityEngine;

namespace GamePlay.Cards
{
    [PrefabDefinition]
    public static class StashCardPrefab
    {
        private const string StashSpritePath = "Assets/GamePlay/Boards/Artwork/Alliance/discard_cards.psd";

        public static void Define(PrefabBuilder builder)
        {
            SpriteRenderer spriteRenderer = null;

            builder
                .WithName("Game/StashCard")
                .WithComponent<StashCard>()
                .WithComponent<SpriteRenderer>(sr => {
                    sr.color = Color.white;
                    sr.sortingLayerName = "Field";
                    sr.sortingOrder = 0;
                    spriteRenderer = sr;
                });

            builder.SetSerialized<StashCard>("_even",
                AssetsBuilderExtensions.LoadSubAsset<Sprite>(StashSpritePath, "discard_cards_0"));

            builder.SetSerialized<StashCard>("_odd",
                AssetsBuilderExtensions.LoadSubAsset<Sprite>(StashSpritePath, "discard_cards_1"));
            builder.SetSerialized<StashCard>("_renderer", spriteRenderer);
        }
    }
}
#endif