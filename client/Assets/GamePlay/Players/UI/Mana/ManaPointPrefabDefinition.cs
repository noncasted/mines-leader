#if UNITY_EDITOR
using Tools;
using UnityEngine;
using UnityEngine.UI;

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
            Image image = null;

            builder
                .WithName("ManaPoint")
                .WithComponent<Image>(img => {
                            img.sprite = spriteFull;
                            img.color = Color.white;
                            img.raycastTarget = true;
                            image = img;
                        }
                    )
                .WithComponent<PlayerManaPointView>();

            builder.SetSerialized<PlayerManaPointView>("_empty", spriteEmpty);
            builder.SetSerialized<PlayerManaPointView>("_full", spriteFull);
            builder.SetSerialized<PlayerManaPointView>("_image", image);

            var rt = builder.GameObject.GetComponent<RectTransform>();
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(1f, 1f);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
        }
    }
}
#endif