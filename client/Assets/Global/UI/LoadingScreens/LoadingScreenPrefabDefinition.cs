#if UNITY_EDITOR
using MPUIKIT;
using Tools;
using UnityEngine;
using UnityEngine.UI;

namespace Global.UI.Prefabs
{
    [PrefabDefinition]
    public static class LoadingScreenPrefab
    {
        private const string LoadingPsd = "Assets/Global/UI/LoadingScreens/loading.psd";

        public static void Define(PrefabBuilder builder)
        {
            CanvasGroup canvasGroup = null;
            LoadingScreenAnimation animation = null;

            builder
                .WithName("LoadingScreen")
                .WithComponent<Canvas>(c =>
                {
                    c.renderMode = RenderMode.ScreenSpaceOverlay;
                    c.sortingOrder = 32767;
                })
                .WithComponent<CanvasScaler>(cs =>
                {
                    cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    cs.referenceResolution = new Vector2(1920f, 1080f);
                    cs.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                    cs.matchWidthOrHeight = 0f;
                })
                .WithComponent<GraphicRaycaster>()
                .WithComponent<CanvasGroup>(cg =>
                {
                    cg.alpha = 1f;
                    cg.interactable = false;
                    cg.blocksRaycasts = true;
                    cg.ignoreParentGroups = false;
                    canvasGroup = cg;
                })
                .WithComponent<LoadingScreen>();

            var rootRt = builder.GameObject.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.anchoredPosition = Vector2.zero;
            rootRt.sizeDelta = Vector2.zero;
            rootRt.pivot = new Vector2(0.5f, 0.5f);

            builder.WithChildObject("Background", bg =>
            {
                bg.WithComponent<MPImage>(img =>
                {
                    img.color = Color.white;
                    img.raycastTarget = true;
                    img.GradientEffect = new GradientEffect
                    {
                        Enabled = true,
                        GradientType = GradientType.Linear,
                        Rotation = -118.18f,
                        Gradient = CreateBackgroundGradient()
                    };
                });

                var bgRt = bg.GameObject.GetComponent<RectTransform>();
                bgRt.anchorMin = Vector2.zero;
                bgRt.anchorMax = Vector2.one;
                bgRt.anchoredPosition = Vector2.zero;
                bgRt.sizeDelta = Vector2.zero;
                bgRt.pivot = new Vector2(0.5f, 0.5f);
            });

            builder.WithChildObject("Animation", anim =>
            {
                Image animImage = null;

                anim.WithComponent<Image>(img =>
                {
                    img.sprite = PrefabBuilder.LoadSubAsset<Sprite>(LoadingPsd, "loading_0");
                    img.color = Color.white;
                    img.raycastTarget = true;
                    animImage = img;
                });

                anim.WithComponent<LoadingScreenAnimation>(lsa => animation = lsa);

                var animRt = anim.GameObject.GetComponent<RectTransform>();
                animRt.anchorMin = new Vector2(0.5f, 0.5f);
                animRt.anchorMax = new Vector2(0.5f, 0.5f);
                animRt.anchoredPosition = Vector2.zero;
                animRt.sizeDelta = new Vector2(480f, 320f);
                animRt.pivot = new Vector2(0.5f, 0.5f);

                anim.SetSerialized<LoadingScreenAnimation>("_image", animImage);
                anim.SetSerialized<LoadingScreenAnimation>("_loopTime", 1f);
                anim.SetSerialized<LoadingScreenAnimation>("_sequence", new[]
                {
                    PrefabBuilder.LoadSubAsset<Sprite>(LoadingPsd, "loading_2"),
                    PrefabBuilder.LoadSubAsset<Sprite>(LoadingPsd, "loading_3"),
                    PrefabBuilder.LoadSubAsset<Sprite>(LoadingPsd, "loading_4"),
                    PrefabBuilder.LoadSubAsset<Sprite>(LoadingPsd, "loading_5")
                });
            });

            builder.SetSerialized<LoadingScreen>("_group", canvasGroup);
            builder.SetSerialized<LoadingScreen>("_curve._time", 1f);
            builder.SetSerialized<LoadingScreen>("_curve._curve", new AnimationCurve(
                new Keyframe(0f, 0f, 0f, 0f),
                new Keyframe(1f, 1f, 2f, 2f)));
            builder.SetSerialized<LoadingScreen>("_animation", animation);
        }

        private static Gradient CreateBackgroundGradient()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.004f, 0.004f, 0.004f), 0f),
                    new GradientColorKey(new Color(0f, 0f, 0.008f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f)
                });
            return gradient;
        }
    }
}
#endif
