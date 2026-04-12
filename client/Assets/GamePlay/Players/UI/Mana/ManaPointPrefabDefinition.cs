#if UNITY_EDITOR
using Tools;
using UnityEngine;

namespace GamePlay.Players
{
    [PrefabDefinition]
    public static class ManaPointPrefab
    {
        private const string SpritePath = "Assets/GamePlay/Players/Art/mana_points.psd";

        public static void Define(PrefabBuilder builder)
        {
            var spriteEmpty = PrefabBuilder.LoadSubAsset<Sprite>(SpritePath, "mana_points_1");
            var spriteFull = PrefabBuilder.LoadSubAsset<Sprite>(SpritePath, "mana_points_0");
            SpriteRenderer spriteRenderer = null;

            builder
                .WithName("ManaPoint")
                .WithComponent<SpriteRenderer>(sr => {
                    sr.sprite = spriteFull;
                    sr.color = Color.white;
                    sr.sortingLayerName = "UI";
                    sr.sortingOrder = 1;
                    spriteRenderer = sr;
                })
                .WithComponent<PlayerManaPointView>();

            builder.SetSerialized<PlayerManaPointView>("_empty", spriteEmpty);
            builder.SetSerialized<PlayerManaPointView>("_full", spriteFull);
            builder.SetSerialized<PlayerManaPointView>("_renderer", spriteRenderer);
        }
    }
}
#endif