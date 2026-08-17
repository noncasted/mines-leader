#if UNITY_EDITOR
using Animations;
using TMPro;
using Tools;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using VContainer.Unity;

namespace Menu.Social
{
    [PrefabDefinition]
    public static class MenuPlayerPrefab
    {
        private const string CharacterPsd = "Assets/Menu/Artwork/Player/main_menu_character.psd";
        private const string FontBitach = "Assets/Common/Artwork/BITACH SDF.asset";

        public static void Define(PrefabBuilder builder)
        {
            Rigidbody2D rb = null;
            SpriteRenderer viewRenderer = null;

            builder
                .WithName("Menu/MenuPlayer")
                .WithComponent<Rigidbody2D>(r => {
                            r.gravityScale = 1f;
                            r.constraints = RigidbodyConstraints2D.FreezeRotation;
                            rb = r;
                        }
                    )
                .WithComponent<CircleCollider2D>(c => {
                            c.radius = 15.41f;
                        }
                    )
                .WithComponent<MenuPlayerMovement>()
                .WithComponent<MenuPlayerView>()
                .WithComponent<LifetimeScope>(ls => {
                            ls.autoRun = false;
                        }
                    )
                .WithComponent<SortingGroup>(sg => {
                    sg.sortingLayerName = "Default";
                    sg.sortingOrder = 5;
                });

            builder.WithChildObject("View", view => {
                view.WithScale(50f, 50f, 1f);

                view.WithComponent<SpriteRenderer>(sr => {
                    sr.sprite = AssetsBuilderExtensions.LoadAsset<Sprite>(CharacterPsd);
                    sr.color = Color.white;
                    sr.sortingOrder = 1;
                    viewRenderer = sr;
                });
                view.WithComponent<MenuPlayerAnimator>();

                view.SetSerialized<SpriteAnimationRenderer>("_renderer", viewRenderer);

                var idle0 = AssetsBuilderExtensions.LoadSubAsset<Sprite>(CharacterPsd, "main_menu_character_1");
                var idle1 = AssetsBuilderExtensions.LoadSubAsset<Sprite>(CharacterPsd, "main_menu_character_2");
                var run0 = AssetsBuilderExtensions.LoadSubAsset<Sprite>(CharacterPsd, "main_menu_character_0");
                var run1 = AssetsBuilderExtensions.LoadSubAsset<Sprite>(CharacterPsd, "main_menu_character_1");
                var run2 = AssetsBuilderExtensions.LoadSubAsset<Sprite>(CharacterPsd, "main_menu_character_2");
                var run3 = AssetsBuilderExtensions.LoadSubAsset<Sprite>(CharacterPsd, "main_menu_character_3");

                var so = new SerializedObject(view.GameObject.GetComponent<MenuPlayerAnimator>());

                var idleProp = so.FindProperty("_idle");
                var idleSprites = idleProp.FindPropertyRelative("_sprites");
                idleSprites.arraySize = 2;
                idleSprites.GetArrayElementAtIndex(0).objectReferenceValue = idle0;
                idleSprites.GetArrayElementAtIndex(1).objectReferenceValue = idle1;
                idleProp.FindPropertyRelative("_time").floatValue = 0.8f;

                var runProp = so.FindProperty("_run");
                var runSprites = runProp.FindPropertyRelative("_sprites");
                runSprites.arraySize = 4;
                runSprites.GetArrayElementAtIndex(0).objectReferenceValue = run0;
                runSprites.GetArrayElementAtIndex(1).objectReferenceValue = run1;
                runSprites.GetArrayElementAtIndex(2).objectReferenceValue = run2;
                runSprites.GetArrayElementAtIndex(3).objectReferenceValue = run3;
                runProp.FindPropertyRelative("_time").floatValue = 0.8f;

                so.ApplyModifiedPropertiesWithoutUndo();
            });

            TMP_Text chatText = null;

            builder.WithChildObject("ChatView", chat => {
                chat.WithComponent<TextMeshPro>(tmp => {
                    tmp.font = AssetsBuilderExtensions.LoadAsset<TMP_FontAsset>(FontBitach);
                    tmp.text = "";
                    tmp.color = Color.white;
                    tmp.fontSize = 200f;
                    tmp.enableAutoSizing = true;
                    tmp.fontSizeMin = 200f;
                    tmp.fontSizeMax = 300f;
                    tmp.horizontalAlignment = HorizontalAlignmentOptions.Center;
                    tmp.verticalAlignment = VerticalAlignmentOptions.Middle;
                    tmp.textWrappingMode = TextWrappingModes.Normal;
                    chatText = tmp;
                });

                chat.WithComponent<MenuPlayerChatView>();

                var rt = chat.GameObject.GetComponent<RectTransform>();
                rt.anchoredPosition = new Vector2(0f, 163f);
                rt.sizeDelta = new Vector2(200f, 100f);
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);

                chat.SetSerialized<MenuPlayerChatView>("_text", chatText);
                chat.SetSerialized<MenuPlayerChatView>("_time", 15f);
            });

            builder.SetSerialized<MenuPlayerMovement>("_rb", rb);
            builder.SetSerialized<MenuPlayerMovement>("_moveSpeed", 300f);
            builder.SetSerialized<MenuPlayerMovement>("_lerpSpeed", 10f);
            builder.SetSerialized<MenuPlayerMovement>("_renderer", viewRenderer);
        }
    }
}
#endif