#if UNITY_EDITOR
using System;
using GamePlay.Boards;
using GamePlay.Cards;
using TMPro;
using Tools;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace GamePlay.Players
{
    [PrefabDefinition]
    public static class AvatarPrefab
    {
        private const string FontBitach = "Assets/Common/Artwork/BITACH SDF.asset";
        private const string PortretSprite = "Assets/GamePlay/Players/Art/Alliance/portret.psd";
        private const string PortretFrameSprite = "Assets/GamePlay/Players/Art/Alliance/portret_frame.psd";
        private const string ManaSprite = "Assets/GamePlay/Players/Art/mana.psd";
        private const string HealthSprite = "Assets/GamePlay/Players/Art/health.psd";


        public static void Define(PrefabBuilder builder)
        {
            AvatarView avatarView = null;
            SpriteRenderer avatarSprite = null;
            TMP_Text healthText = null;
            TMP_Text manaText = null;
            AvatarMovesView movesView = null;

            builder
                .WithName("Avatar")
                .WithPosition(0f, -2.915f, 0f)
                .WithComponent<AvatarFactory>()
                .WithComponent<SortingGroup>(sg =>
                    {
                        sg.sortingLayerName = "UI";
                        sg.sortingOrder = 0;
                    }
                );

            builder.WithChildObject("View", view =>
                {
                    view.WithComponent<AvatarView>(av => avatarView = av);

                    view.WithChildObject("Image", image =>
                        {
                            image.WithComponent<SpriteRenderer>(sr =>
                                {
                                    sr.sprite = PrefabBuilder.LoadAsset<Sprite>(PortretSprite);
                                    sr.color = Color.white;
                                    sr.sortingOrder = 0;
                                    avatarSprite = sr;
                                }
                            );
                        }
                    );

                    view.WithChildObject("Outline", outline =>
                        {
                            outline.WithComponent<SpriteRenderer>(sr =>
                                {
                                    sr.sprite = PrefabBuilder.LoadAsset<Sprite>(PortretFrameSprite);
                                    sr.color = Color.white;
                                    sr.sortingOrder = 0;
                                }
                            );
                        }
                    );

                    view.WithChildObject("Mana", mana =>
                        {
                            mana.WithComponent<SpriteRenderer>(sr =>
                                {
                                    sr.sprite = PrefabBuilder.LoadAsset<Sprite>(ManaSprite);
                                    sr.color = Color.white;
                                    sr.sortingOrder = 1;
                                }
                            );

                            ConfigureStatText(mana, 0.969f, -1.383f, tmp => manaText = tmp);
                        }
                    );

                    view.WithChildObject("Health", health =>
                        {
                            health.WithComponent<SpriteRenderer>(sr =>
                                {
                                    sr.sprite = PrefabBuilder.LoadAsset<Sprite>(HealthSprite);
                                    sr.color = Color.white;
                                    sr.sortingOrder = 1;
                                }
                            );

                            ConfigureStatText(health, -0.936f, -1.352f, tmp => healthText = tmp);
                        }
                    );

                    view.WithChildObject("Turns", turns =>
                        {
                            turns.WithPosition(0f, 2.4f, 0f);
                            turns.WithComponent<AvatarMovesView>(mv => movesView = mv);

                            turns.SetSerialized<AvatarMovesView>("_spaceBetweenPoints", 0.584f);
                        }
                    );

                    view.SetSerialized<AvatarView>("_avatarSprite", avatarSprite);
                    view.SetSerialized<AvatarView>("_healthText", healthText);
                    view.SetSerialized<AvatarView>("_manaText", manaText);
                    view.SetSerialized<AvatarView>("_movesView", movesView);
                }
            );

            builder.SetSerialized<AvatarFactory>("_view", avatarView);
        }

        private static void ConfigureStatText(
            PrefabBuilder parent,
            float posX,
            float posY,
            Action<TMP_Text> capture)
        {
            parent.WithChildObject("Text", text =>
                {
                    text.WithComponent<TextMeshPro>(tmp =>
                        {
                            tmp.font = PrefabBuilder.LoadAsset<TMP_FontAsset>(FontBitach);
                            tmp.color = new Color(0.1509434f, 0.10300224f, 0.10667598f, 1f);
                            tmp.fontSize = 8f;
                            tmp.enableAutoSizing = true;
                            tmp.fontSizeMin = 0f;
                            tmp.fontSizeMax = 8f;
                            tmp.horizontalAlignment = HorizontalAlignmentOptions.Center;
                            tmp.verticalAlignment = VerticalAlignmentOptions.Middle;
                            tmp.textWrappingMode = TextWrappingModes.Normal;
                            tmp.text = "1";
                            capture(tmp);
                        }
                    );

                    var rt = text.GameObject.GetComponent<RectTransform>();
                    rt.anchoredPosition = new Vector2(posX, posY);
                    rt.sizeDelta = new Vector2(1f, 1f);

                    var meshRenderer = text.GameObject.GetComponent<MeshRenderer>();
                    if (meshRenderer != null)
                    {
                        meshRenderer.sortingOrder = 2;
                    }
                }
            );
        }
    }

    [PrefabDefinition]
    public static class PlayerLocalBasePrefab
    {
        private const string BoardPrefab = "Assets/GamePlay/Boards/Options/Board_Local.prefab";
        private const string HandOptionsLocal = "Assets/GamePlay/Cards/Hand/Position/HandPositionsOptions_Local.asset";

        public static void Define(PrefabBuilder builder)
        {
            GamePlayerScope scope = null;
            DeckFactory deckFactory = null;
            BoardFactory boardFactory = null;
            HandFactory handFactory = null;
            StashFactory stashFactory = null;

            builder
                .WithName("Player_Local_Base")
                .WithComponent<GamePlayerScope>(s => scope = s)
                .WithComponent<GamePlayerEntityView>();

            builder.WithChildObject("Board", board =>
                {
                    board.WithPosition(10f, 1f, 0f);
                    board.WithComponent<BoardFactory>(bf => boardFactory = bf);

                    var boardTransform = board.GameObject.GetComponent<Transform>();
                    board.SetSerialized<BoardFactory>("_prefab",
                        PrefabBuilder.LoadAsset<Board>(BoardPrefab)
                    );
                    board.SetSerialized<BoardFactory>("_parent", boardTransform);
                }
            );

            builder.WithChildObject("Deck", deck =>
                {
                    deck.WithPosition(19.767f, -3.59f, 0f);

                    DeckView deckView = null;

                    deck.WithComponent<DeckFactory>(df => deckFactory = df);
                    deck.WithComponent<DeckView>(dv => deckView = dv);

                    deck.SetSerialized<DeckView>("_cardHeight", 0.083333336f);
                    deck.SetSerialized<DeckFactory>("_view", deckView);
                }
            );

            builder.WithChildObject("Hand", hand =>
                {
                    HandView handView = null;
                    HandPositions handPositions = null;
                    Transform centerTransform = null;

                    hand.WithComponent<HandFactory>(hf => handFactory = hf);
                    hand.WithComponent<HandView>(hv => handView = hv);
                    hand.WithComponent<HandPositions>(hp => handPositions = hp);

                    hand.WithChildObject("Center", center =>
                        {
                            center.WithPosition(0f, -10f, 0f);
                            centerTransform = center.GameObject.GetComponent<Transform>();
                        }
                    );

                    hand.SetSerialized<HandPositions>("_options",
                        PrefabBuilder.LoadAsset<HandPositionsOptions>(HandOptionsLocal)
                    );
                    hand.SetSerialized<HandPositions>("_center", centerTransform);
                    hand.SetSerialized<HandView>("_positions", handPositions);
                    hand.SetSerialized<HandFactory>("_view", handView);
                }
            );

            builder.WithChildObject("Canvas", canvas =>
                {
                    canvas.WithComponent<Canvas>(c =>
                        {
                            c.renderMode = RenderMode.WorldSpace;
                            c.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1 |
                                                         AdditionalCanvasShaderChannels.Normal |
                                                         AdditionalCanvasShaderChannels.Tangent;
                        }
                    );
                    canvas.WithComponent<CanvasScaler>();
                    canvas.WithComponent<GraphicRaycaster>();

                    var rt = canvas.GameObject.GetComponent<RectTransform>();
                    rt.sizeDelta = new Vector2(42.6667f, 24f);

                    canvas.WithPrefabChild("Assets/Resources/Generated/Mines.prefab", "Mines");
                }
            );

            builder.WithChildObject("Stash", stash =>
                {
                    stash.WithPosition(19.767f, 1.21f, 0f);

                    StashView stashView = null;

                    stash.WithComponent<StashFactory>(sf => stashFactory = sf);
                    stash.WithComponent<StashView>(sv => stashView = sv);

                    stash.SetSerialized<StashView>("_cardHeight", 0.083333336f);
                    stash.SetSerialized<StashFactory>("_view", stashView);
                }
            );

            var avatarGo = builder.WithPrefabChild("Assets/Resources/Generated/Avatar.prefab", "Avatar");
            avatarGo.transform.localPosition = new Vector3(0f, -2.92f, 0f);
            var avatarFactory = avatarGo.GetComponent<AvatarFactory>();

            builder.SetSerialized<GamePlayerEntityView>("_scope", scope);
            builder.SetSerialized<GamePlayerEntityView>("_deckFactory", deckFactory);
            builder.SetSerialized<GamePlayerEntityView>("_boardFactory", boardFactory);
            builder.SetSerialized<GamePlayerEntityView>("_handFactory", handFactory);
            builder.SetSerialized<GamePlayerEntityView>("_stashFactory", stashFactory);
            builder.SetSerialized<GamePlayerEntityView>("_avatarFactory", avatarFactory);
        }
    }

    [PrefabDefinition]
    public static class PlayerRemoteBasePrefab
    {
        private const string BoardPrefab = "Assets/GamePlay/Boards/Options/Board_Remote.prefab";

        private const string
            HandOptionsRemote = "Assets/GamePlay/Cards/Hand/Position/HandPositionsOptions_Remote.asset";

        public static void Define(PrefabBuilder builder)
        {
            GamePlayerScope scope = null;
            DeckFactory deckFactory = null;
            BoardFactory boardFactory = null;
            HandFactory handFactory = null;
            StashFactory stashFactory = null;

            builder
                .WithName("Player_Remote_Base")
                .WithComponent<GamePlayerScope>(s => scope = s)
                .WithComponent<GamePlayerEntityView>();

            builder.WithChildObject("Board", board =>
                {
                    board.WithPosition(-10f, 1f, 0f);
                    board.WithComponent<BoardFactory>(bf => boardFactory = bf);

                    var boardTransform = board.GameObject.GetComponent<Transform>();
                    board.SetSerialized<BoardFactory>("_prefab",
                        PrefabBuilder.LoadAsset<Board>(BoardPrefab)
                    );
                    board.SetSerialized<BoardFactory>("_parent", boardTransform);
                }
            );

            builder.WithChildObject("Deck", deck =>
                {
                    deck.WithPosition(-19.767f, -3.59f, 0f);

                    DeckView deckView = null;

                    deck.WithComponent<DeckFactory>(df => deckFactory = df);
                    deck.WithComponent<DeckView>(dv => deckView = dv);

                    deck.SetSerialized<DeckView>("_cardHeight", 0.083333336f);
                    deck.SetSerialized<DeckFactory>("_view", deckView);

                    deck.WithChild("SpawnPoint");
                }
            );

            builder.WithChildObject("Hand", hand =>
                {
                    HandView handView = null;
                    HandPositions handPositions = null;
                    Transform centerTransform = null;

                    hand.WithComponent<HandFactory>(hf => handFactory = hf);
                    hand.WithComponent<HandView>(hv => handView = hv);
                    hand.WithComponent<HandPositions>(hp => handPositions = hp);

                    hand.WithChildObject("Center", center =>
                        {
                            center.WithPosition(0f, 12.69f, 0f);
                            centerTransform = center.GameObject.GetComponent<Transform>();
                        }
                    );

                    hand.SetSerialized<HandPositions>("_options",
                        PrefabBuilder.LoadAsset<HandPositionsOptions>(HandOptionsRemote)
                    );
                    hand.SetSerialized<HandPositions>("_center", centerTransform);
                    hand.SetSerialized<HandView>("_positions", handPositions);
                    hand.SetSerialized<HandFactory>("_view", handView);
                }
            );

            builder.WithChildObject("Canvas", canvas =>
                {
                    canvas.WithComponent<Canvas>(c =>
                        {
                            c.renderMode = RenderMode.WorldSpace;
                            c.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1 |
                                                         AdditionalCanvasShaderChannels.Normal |
                                                         AdditionalCanvasShaderChannels.Tangent;
                        }
                    );
                    canvas.WithComponent<CanvasScaler>();
                    canvas.WithComponent<GraphicRaycaster>();

                    var rt = canvas.GameObject.GetComponent<RectTransform>();
                    rt.sizeDelta = new Vector2(42.6667f, 24f);

                    var minesGo = canvas.WithPrefabChild("Assets/Resources/Generated/Mines.prefab", "Mines");
                    if (minesGo != null)
                    {
                        var minesRt = minesGo.GetComponent<RectTransform>();
                        if (minesRt != null)
                        {
                            minesRt.anchoredPosition = new Vector2(-16.626f, 9.8333f);
                            minesRt.sizeDelta = new Vector2(100f, 100f);
                        }
                    }
                }
            );

            builder.WithChildObject("Stash", stash =>
                {
                    stash.WithPosition(-19.767f, 1.42f, 0f);

                    StashView stashView = null;

                    stash.WithComponent<StashFactory>(sf => stashFactory = sf);
                    stash.WithComponent<StashView>(sv => stashView = sv);

                    stash.SetSerialized<StashView>("_cardHeight", 0.083333336f);
                    stash.SetSerialized<StashFactory>("_view", stashView);
                }
            );

            var avatarGo = builder.WithPrefabChild("Assets/Resources/Generated/Avatar.prefab", "Avatar");
            avatarGo.transform.localPosition = new Vector3(0f, 6.25f, 0f);
            var avatarFactory = avatarGo.GetComponent<AvatarFactory>();

            builder.SetSerialized<GamePlayerEntityView>("_scope", scope);
            builder.SetSerialized<GamePlayerEntityView>("_deckFactory", deckFactory);
            builder.SetSerialized<GamePlayerEntityView>("_boardFactory", boardFactory);
            builder.SetSerialized<GamePlayerEntityView>("_handFactory", handFactory);
            builder.SetSerialized<GamePlayerEntityView>("_stashFactory", stashFactory);
            builder.SetSerialized<GamePlayerEntityView>("_avatarFactory", avatarFactory);
        }
    }
}
#endif