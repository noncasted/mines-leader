#if UNITY_EDITOR
using Tools;
using Tools.DI;
using Tools.Objects;
using UnityEngine;

namespace GamePlay.Cards
{
    [PrefabDefinition]
    public static class DeckCardPrefab
    {
        private const string DeckSpritePath = "Assets/GamePlay/Boards/Artwork/Alliance/deck_cards.psd";

        public static void Define(PrefabBuilder builder)
        {
            SpriteRenderer spriteRenderer = null;

            builder
                .WithName("Game/DeckCard")
                .WithComponent<SpriteRenderer>(sr => {
                            sr.color = Color.white;
                            sr.sortingLayerName = "Field";
                            sr.sortingOrder = 3;
                            spriteRenderer = sr;
                        }
                    )
                .WithComponent<DeckCard>();

            builder.SetSerialized<DeckCard>("_even",
                AssetsBuilderExtensions.LoadSubAsset<Sprite>(DeckSpritePath, "DeckCard_0"));

            builder.SetSerialized<DeckCard>("_odd",
                AssetsBuilderExtensions.LoadSubAsset<Sprite>(DeckSpritePath, "DeckCard_1"));
            builder.SetSerialized<DeckCard>("_renderer", spriteRenderer);
        }
    }
}
#endif