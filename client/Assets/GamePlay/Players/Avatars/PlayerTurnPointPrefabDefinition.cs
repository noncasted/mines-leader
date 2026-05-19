#if UNITY_EDITOR
using Tools.PrefabBuilder;
using UnityEngine;

namespace GamePlay.Players
{
    [PrefabDefinition]
    public static class PlayerTurnPointPrefab
    {
        private const string TurnSpritePath = "Assets/GamePlay/Players/Art/Alliance/Player_Turn.psd";

        public static void Define(PrefabBuilder builder)
        {
            SpriteRenderer spriteRenderer = null;

            builder
                .WithName("Game/PlayerTurnPoint")
                .WithComponent<SpriteRenderer>(sr => {
                            sr.sprite = AssetsBuilderExtensions.LoadSubAsset<Sprite>(TurnSpritePath, "Player_Turn_Inactive");
                            sr.color = Color.white;
                            spriteRenderer = sr;
                        }
                    )
                .WithComponent<AvatarTurnPointView>();

            builder.SetSerialized<AvatarTurnPointView>("_baseActive",
                AssetsBuilderExtensions.LoadSubAsset<Sprite>(TurnSpritePath, "Player_Turn_Active"));

            builder.SetSerialized<AvatarTurnPointView>("_baseInactive",
                AssetsBuilderExtensions.LoadSubAsset<Sprite>(TurnSpritePath, "Player_Turn_Inactive"));

            builder.SetSerialized<AvatarTurnPointView>("_additionalActive",
                AssetsBuilderExtensions.LoadSubAsset<Sprite>(TurnSpritePath, "Player_Turn_Active"));

            builder.SetSerialized<AvatarTurnPointView>("_additionalInactive",
                AssetsBuilderExtensions.LoadSubAsset<Sprite>(TurnSpritePath, "Player_Turn_Inactive"));

            builder.SetSerialized<AvatarTurnPointView>("_renderer", spriteRenderer);
        }
    }
}
#endif
