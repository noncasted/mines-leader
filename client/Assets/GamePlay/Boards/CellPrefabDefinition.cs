#if UNITY_EDITOR
using GamePlay.Boards.Effects;
using TMPro;
using Tools;
using UnityEditor;
using UnityEngine;

namespace GamePlay.Boards
{
    [PrefabDefinition]
    public static class CellPrefab
    {
        private const string CellSpritePath = "Assets/GamePlay/Boards/Artwork/Alliance/cell.psd";
        private const string FlagSpritePath = "Assets/GamePlay/Boards/Artwork/Alliance/flag.psd";
        private const string BlowupSpritePath = "Assets/GamePlay/Boards/Artwork/cell_blowup.psd";
        private const string HighlightSpritePath = "Assets/GamePlay/Boards/Artwork/cell_highlight.psd";
        private const string EmptySpritePath = "Assets/Plugins/MPUIKit/Runtime/Resources/mpui_default_empty_sprite.png";
        private const string FontBitach = "Assets/Common/Artwork/BITACH SDF.asset";
        private const string MineExplosionPath = "Assets/GamePlay/Boards/Artwork/mine_explosion.psd";
        private const string ZipZapExplosionPath = "Assets/GamePlay/Boards/Artwork/zipZap_explosion.png";

        public static void Define(PrefabBuilder builder)
        {
            CellPointerHandler pointerHandler = null;
            CellFreeView freeView = null;
            CellTakenView takenView = null;
            CellSelectionView selectionView = null;
            CellAnimator animator = null;
            CellEffects effects = null;

            builder
                .WithName("Cell")
                .WithComponent<CellView>();

            // PointerHandler
            builder.WithChildObject("PointerHandler", pointer =>
            {
                pointer.WithComponent<CellPointerHandler>(c => pointerHandler = c);
                pointer.WithComponent<BoxCollider2D>(bc =>
                {
                    bc.size = new Vector2(1f, 1f);
                });
                pointer.SetSerialized<CellPointerHandler>("_collider",
                    pointer.GameObject.GetComponent<BoxCollider2D>());
            });

            // Free (inactive)
            builder.WithChildObject("Free", false, free =>
            {
                free.WithComponent<CellFreeView>(c => freeView = c);

                free.WithChildObject("Sprite", sprite =>
                {
                    sprite.WithComponent<SpriteRenderer>(sr =>
                    {
                        sr.sprite = LoadSprite(CellSpritePath, "Cell_Free");
                        sr.color = Color.white;
                        sr.sortingLayerName = "Field";
                        sr.sortingOrder = 0;
                    });
                });

                TMP_Text counterText = null;

                free.WithChildObject("Counter", counter =>
                {
                    counter.WithComponent<TextMeshPro>(tmp =>
                    {
                        tmp.font = PrefabBuilder.LoadAsset<TMP_FontAsset>(FontBitach);
                        tmp.text = "1";
                        tmp.color = new Color(0.730f, 0.766f, 0.513f, 1f);
                        tmp.fontSize = 8f;
                        tmp.enableAutoSizing = true;
                        tmp.fontSizeMin = 0f;
                        tmp.fontSizeMax = 8f;
                        tmp.horizontalAlignment = HorizontalAlignmentOptions.Center;
                        tmp.verticalAlignment = VerticalAlignmentOptions.Middle;
                        tmp.textWrappingMode = TextWrappingModes.Normal;
                        tmp.isOrthographic = false;
                        counterText = tmp;
                    });

                    var rt = counter.GameObject.GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        rt.anchoredPosition = Vector2.zero;
                        rt.sizeDelta = new Vector2(1f, 1f);
                    }

                    var mr = counter.GameObject.GetComponent<MeshRenderer>();
                    if (mr != null)
                    {
                        mr.sortingLayerName = "Field";
                        mr.sortingOrder = 2;
                    }
                });

                free.SetSerialized<CellFreeView>("_count", counterText);
            });

            // Taken
            GameObject mineGo = null;
            GameObject flagGo = null;

            builder.WithChildObject("Taken", taken =>
            {
                taken.WithComponent<CellTakenView>(c => takenView = c);

                taken.WithChildObject("Sprite", sprite =>
                {
                    sprite.WithComponent<SpriteRenderer>(sr =>
                    {
                        sr.sprite = LoadSprite(CellSpritePath, "Cell_Taken");
                        sr.color = Color.white;
                        sr.sortingLayerName = "Field";
                        sr.sortingOrder = 0;
                    });
                });

                taken.WithChildObject("Flag", false, flag =>
                {
                    flagGo = flag.GameObject;

                    flag.WithChildObject("Sprite", flagSprite =>
                    {
                        flagSprite.WithComponent<SpriteRenderer>(sr =>
                        {
                            sr.sprite = PrefabBuilder.LoadAsset<Sprite>(FlagSpritePath);
                            sr.color = Color.white;
                            sr.sortingLayerName = "Field";
                            sr.sortingOrder = 1;
                        });
                    });
                });
            });

            // Mine (inactive)
            builder.WithChildObject("Mine", false, mine =>
            {
                mineGo = mine.GameObject;

                mine.WithChildObject("Sprite", mineSprite =>
                {
                    mineSprite.WithComponent<SpriteRenderer>(sr =>
                    {
                        sr.sprite = PrefabBuilder.LoadAsset<Sprite>(BlowupSpritePath);
                        sr.color = Color.white;
                        sr.sortingLayerName = "Field";
                        sr.sortingOrder = 1;
                    });
                });
            });

            // Selection (inactive)
            builder.WithChildObject("Selection", false, selection =>
            {
                selection.WithComponent<CellSelectionView>(c => selectionView = c);
                selection.WithComponent<SpriteRenderer>(sr =>
                {
                    sr.sprite = PrefabBuilder.LoadAsset<Sprite>(HighlightSpritePath);
                    sr.color = new Color(1f, 1f, 1f, 0.373f);
                    sr.sortingLayerName = "Field";
                    sr.sortingOrder = 1;
                });
            });

            // Effects
            SmokeCellEffect smokeEffect = null;
            FogCellEffect fogEffect = null;

            builder.WithChildObject("Effects", effectsObj =>
            {
                effectsObj.WithComponent<CellEffects>(c => effects = c);

                effectsObj.WithChildObject("Smoke", false, smoke =>
                {
                    smoke.WithScale(1.56f, 1.56f, 1f);
                    smoke.WithComponent<SpriteRenderer>(sr =>
                    {
                        sr.sprite = PrefabBuilder.LoadAsset<Sprite>(EmptySpritePath);
                        sr.color = new Color(0.877f, 0.877f, 0.877f, 1f);
                        sr.sortingLayerName = "Field";
                        sr.sortingOrder = 5;
                    });
                    smoke.WithComponent<SmokeCellEffect>(c => smokeEffect = c);
                });

                effectsObj.WithChildObject("Fog", false, fog =>
                {
                    fog.WithScale(1.56f, 1.56f, 1f);
                    fog.WithComponent<SpriteRenderer>(sr =>
                    {
                        sr.sprite = PrefabBuilder.LoadAsset<Sprite>(EmptySpritePath);
                        sr.color = new Color(0.877f, 0.877f, 0.877f, 1f);
                        sr.sortingLayerName = "Field";
                        sr.sortingOrder = 5;
                    });
                    fog.WithComponent<FogCellEffect>(c => fogEffect = c);
                });
            });

            // Animations
            SpriteRenderer animationRenderer = null;

            builder.WithChildObject("Animations", animations =>
            {
                animations.WithComponent<SpriteRenderer>(sr =>
                {
                    sr.sprite = null;
                    sr.color = Color.white;
                    sr.sortingLayerName = "Field";
                    sr.sortingOrder = 4;
                    animationRenderer = sr;
                });
                animations.WithComponent<CellAnimator>(c => animator = c);

                animations.SetSerialized<CellAnimator>("_renderer", animationRenderer);
            });

            // CellView cross-references (CellView is on root)
            builder.SetSerialized<CellView>("_pointerHandler", pointerHandler);
            builder.SetSerialized<CellView>("_freeView", freeView);
            builder.SetSerialized<CellView>("_takenView", takenView);
            builder.SetSerialized<CellView>("_selection", selectionView);
            builder.SetSerialized<CellView>("_animator", animator);
            builder.SetSerialized<CellView>("_effects", effects);

            // CellTakenView cross-references (CellTakenView is on Taken child)
            ConfigureTakenView(takenView, mineGo, flagGo);

            // Complex serialized fields that require direct SerializedObject access
            ConfigureAnimatorData(animator);
            ConfigureEffectsDictionary(effects, smokeEffect, fogEffect);
        }

        private static void ConfigureTakenView(CellTakenView takenView, GameObject mineGo, GameObject flagGo)
        {
            var so = new SerializedObject(takenView);
            so.FindProperty("_mine").objectReferenceValue = mineGo;
            so.FindProperty("_flag").objectReferenceValue = flagGo;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureAnimatorData(CellAnimator animator)
        {
            var so = new SerializedObject(animator);

            var mineSprites = so.FindProperty("_mineExplosionData._sprites");
            var mineTime = so.FindProperty("_mineExplosionData._time");
            if (mineSprites != null && mineTime != null)
            {
                var sprites = LoadAllSprites(MineExplosionPath, 3);
                mineSprites.arraySize = sprites.Length;
                for (int i = 0; i < sprites.Length; i++)
                {
                    mineSprites.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
                }
                mineTime.floatValue = 0.2f;
            }

            var zipSprites = so.FindProperty("_zipZapExplosionData._sprites");
            var zipTime = so.FindProperty("_zipZapExplosionData._time");
            if (zipSprites != null && zipTime != null)
            {
                var sprites = LoadAllSprites(ZipZapExplosionPath);
                zipSprites.arraySize = sprites.Length;
                for (int i = 0; i < sprites.Length; i++)
                {
                    zipSprites.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
                }
                zipTime.floatValue = 0.8f;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureEffectsDictionary(
            CellEffects effects,
            SmokeCellEffect smokeEffect,
            FogCellEffect fogEffect)
        {
            var so = new SerializedObject(effects);
            var keys = so.FindProperty("_effects._keys");
            var values = so.FindProperty("_effects._values");

            if (keys != null && values != null)
            {
                keys.arraySize = 2;
                values.arraySize = 2;

                keys.GetArrayElementAtIndex(0).enumValueIndex = 0;
                values.GetArrayElementAtIndex(0).objectReferenceValue = smokeEffect;

                keys.GetArrayElementAtIndex(1).enumValueIndex = 1;
                values.GetArrayElementAtIndex(1).objectReferenceValue = fogEffect;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Sprite LoadSprite(string assetPath, string spriteName)
        {
            var allAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            foreach (var asset in allAssets)
            {
                if (asset is Sprite sprite && sprite.name == spriteName)
                    return sprite;
            }

            Debug.LogWarning($"[CellPrefab] Sprite '{spriteName}' not found in '{assetPath}'");
            return null;
        }

        private static Sprite[] LoadAllSprites(string assetPath, int count = -1)
        {
            var allAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            var sprites = new System.Collections.Generic.List<Sprite>();
            foreach (var asset in allAssets)
            {
                if (asset is Sprite sprite)
                    sprites.Add(sprite);
            }
            sprites.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));
            if (count > 0 && count < sprites.Count)
                sprites.RemoveRange(count, sprites.Count - count);
            return sprites.ToArray();
        }
    }
}
#endif
