using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Global.UI;
using Global.UI.Toolkit;
using Internal;
using Menu.Screens.Cards.Preview;
using Meta;
using Shared;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace Menu.Decks
{
    public interface IMenuDecks : IUIState
    {
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public class MenuDecks : MonoBehaviour, IMenuDecks, IScopeSetup, ISceneService, IUIStateAsyncEnterHandler
    {
        [SerializeField] private VisualTreeAsset _cardTemplate;

        private VisualElement _root;
        private VisualElement _cardsRoot;

        private readonly List<CardElement> _deckSlots = new();
        private readonly List<CardElement> _poolCards = new();
        private readonly Dictionary<CardType, CardElement> _typeToPoolCard = new();

        private IDeckService _deckService;
        private ICardsRegistry _cardsRegistry;
        private ICardConfigs _configs;
        private IBackendProjection<SharedBackendUser.CardsProjection> _cardsProjection;
        private IMenuCardPreviewPlayer _previewPlayer;

        private Label _avgManaLabel;
        private VisualElement _deckSlotsContainer;
        private ScrollView _poolScroll;
        private VisualElement _deckIndexRow;
        private VisualElement _bottomBar;

        private VisualElement _previewPopup;
        private VisualElement _previewImage;
        private Label _previewName;
        private Label _previewDesc;
        private Texture2D _glowTexture;

        public IUIConstraints Constraints { get; } = UIConstraints.Game;

        [Inject]
        private void Construct(
            IDeckService deckService,
            ICardsRegistry cardsRegistry,
            ICardConfigs configs,
            IBackendProjection<SharedBackendUser.CardsProjection> cardsProjection,
            IMenuCardPreviewPlayer previewPlayer)
        {
            _configs = configs;
            _cardsRegistry = cardsRegistry;
            _deckService = deckService;
            _cardsProjection = cardsProjection;
            _previewPlayer = previewPlayer;
        }

        public void Create(IScopeBuilder builder)
        {
            builder.RegisterComponent(this)
                   .As<IMenuDecks>()
                   .As<IScopeSetup>();
        }

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _root = GetComponent<UIDocument>().rootVisualElement;

            _cardsRoot = _root.Q<VisualElement>("cards-root");
            _avgManaLabel = _root.Q<Label>("avg-mana");
            _deckSlotsContainer = _root.Q<VisualElement>("deck-slots");
            _poolScroll = _root.Q<ScrollView>("pool-scroll");
            _deckIndexRow = _root.Q<VisualElement>("deck-index-row");

            _previewPopup = _root.Q<VisualElement>("card-preview-popup");
            _previewImage = _root.Q<VisualElement>("card-preview-image");
            _previewName = _root.Q<Label>("card-preview-name");
            _previewDesc = _root.Q<Label>("card-preview-desc");
            _previewPopup.Hide();

            // Find bottom bar from another UIDocument
            var allDocs = FindObjectsByType<UIDocument>(FindObjectsSortMode.None);

            foreach (var doc in allDocs)
            {
                if (doc == GetComponent<UIDocument>())
                    continue;
                var bar = doc.rootVisualElement?.Q<VisualElement>("bottom-bar");

                if (bar != null)
                {
                    _bottomBar = doc.rootVisualElement;
                    break;
                }
            }

            _cardsRoot.Hide();

            if (_deckService.Configurations.Count == 0)
                _deckService.Updated.Advise(lifetime, () => OnInitialized(lifetime));
            else
                OnInitialized(lifetime);
        }

        private CardElement CloneCard(bool isPoolCard)
        {
            var container = _cardTemplate.CloneTree();
            var cardRoot = container.Q<VisualElement>("card-root");

            // Remove from template container, use card-root directly
            cardRoot.RemoveFromHierarchy();

            if (isPoolCard)
                cardRoot.AddToClassList("pool-card");

            return new CardElement(cardRoot);
        }

        private void OnInitialized(IReadOnlyLifetime lifetime)
        {
            if (_deckSlots.Count != 0)
                return;

            var deckButtons = _deckIndexRow.Query<Button>(className: "deck-index-btn").ToList();

            for (var i = 0; i < deckButtons.Count; i++)
            {
                var index = i;

                deckButtons[i].ListenClick(lifetime, () => {
                    SetActiveIndex(index);
                    UpdateDeck(index);
                });
            }

            SetActiveIndex(_deckService.SelectedIndex.Value);

            // Deck slots
            var selected = _deckService.Configurations[_deckService.SelectedIndex.Value];

            for (var i = 0; i < selected.Cards.Count; i++)
            {
                var slot = CloneCard(false);
                slot.Root.AddToClassList("deck-slot");
                _deckSlotsContainer.Add(slot.Root);
                _deckSlots.Add(slot);
                RegisterPreviewHover(slot, lifetime);
            }

            // Pool cards
            foreach (var (type, definition) in _cardsRegistry.Entries)
            {
                var config = _configs.Value.All[type];
                var card = CloneCard(true);
                card.SetCard(definition, config);
                _poolScroll.Add(card.Root);
                _poolCards.Add(card);
                _typeToPoolCard[type] = card;

                var manipulator = new CardDragManipulator(
                    _root,
                    () => CreateGhostCard(definition, config),
                    dropTarget => OnCardDropped(card, dropTarget),
                    () => card.IsOwned);
                card.Root.AddManipulator(manipulator);
                RegisterPreviewHover(card, lifetime);
            }

            ForceUpdateDeck(_deckService.SelectedIndex.Value);
            RecalculateMana();

            _cardsProjection.Listen(lifetime, OnCardsUpdated);

            // Generate white glow texture after layout is resolved
            _poolScroll.schedule.Execute(GenerateGlowTexture).ExecuteLater(200);
        }


        private void GenerateGlowTexture()
        {
            if (_poolCards.Count == 0)
                return;

            var frame = _poolCards[0].Root.Q<VisualElement>("card-frame");
            var bg = frame.resolvedStyle.backgroundImage;
            var srcTex = bg.texture;

            if (srcTex == null && bg.sprite != null)
                srcTex = bg.sprite.texture;

            if (srcTex == null)
                return;

            var rt = RenderTexture.GetTemporary(srcTex.width, srcTex.height, 0);
            Graphics.Blit(srcTex, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            _glowTexture = new Texture2D(srcTex.width, srcTex.height, TextureFormat.RGBA32, false);
            _glowTexture.ReadPixels(new Rect(0, 0, srcTex.width, srcTex.height), 0, 0);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);

            var pixels = _glowTexture.GetPixels();

            for (var i = 0; i < pixels.Length; i++)
                pixels[i] = new Color(1f, 1f, 1f, pixels[i].a);
            _glowTexture.SetPixels(pixels);
            _glowTexture.Apply();
            _glowTexture.filterMode = FilterMode.Point;

            var style = new StyleBackground(_glowTexture);

            foreach (var card in _poolCards)
            {
                var glow = card.Root.Q<VisualElement>("card-glow");

                if (glow != null)
                    glow.style.backgroundImage = style;
            }

            foreach (var slot in _deckSlots)
            {
                var glow = slot.Root.Q<VisualElement>("card-glow");

                if (glow != null)
                    glow.style.backgroundImage = style;
            }
        }

        private void OnCardsUpdated(SharedBackendUser.CardsProjection projection)
        {
            var ownedSet = new HashSet<CardType>(projection.OwnedCards);

            foreach (var (type, card) in _typeToPoolCard)
                card.SetOwned(ownedSet.Contains(type));

            var sorted = _poolCards
                         .OrderByDescending(c => c.IsOwned)
                         .ToList();

            for (var i = 0; i < sorted.Count; i++)
                _poolScroll.Insert(i, sorted[i].Root);
        }

        public async UniTask OnEntered(IUIStateHandle handle)
        {
            _cardsRoot.Show();
            _bottomBar?.Hide();

            handle.InnerLifetime.Listen(() => {
                _cardsRoot.Hide();
                _bottomBar?.Show();
            });

            var completion = new UniTaskCompletionSource();

            var exitBtn = _root.Q<Button>("btn-exit");

            if (exitBtn != null)
                exitBtn.ListenClick(handle.InnerLifetime, () => completion.TrySetResult());

            void OnKeyDown(KeyDownEvent evt)
            {
                if (evt.keyCode == KeyCode.Escape)
                    completion.TrySetResult();
            }

            _cardsRoot.RegisterCallback<KeyDownEvent>(OnKeyDown);
            handle.InnerLifetime.Listen(() => _cardsRoot.UnregisterCallback<KeyDownEvent>(OnKeyDown));

            await completion.Task;

            _deckService.SendUpdate().Forget();
        }

        private void SetActiveIndex(int activeIndex)
        {
            var buttons = _deckIndexRow.Children().ToList();

            for (var i = 0; i < buttons.Count; i++)
            {
                if (i == activeIndex)
                    buttons[i].AddToClassList("active");
                else
                    buttons[i].RemoveFromClassList("active");
            }
        }

        private void UpdateDeck(int index)
        {
            _deckService.SetIndex(index);
            ForceUpdateDeck(index);
        }

        private void ForceUpdateDeck(int index)
        {
            foreach (var card in _poolCards)
                card.SetInDeck(false);

            var selected = _deckService.Configurations[index];

            for (var i = 0; i < selected.Cards.Count && i < _deckSlots.Count; i++)
            {
                var cardDef = selected.Cards[i];
                var config = _configs.Value.All[cardDef.Type];
                _deckSlots[i].SetCard(cardDef, config);
                _typeToPoolCard[cardDef.Type].SetInDeck(true);
            }

            RecalculateMana();
        }

        private void OnCardDropped(CardElement poolCard, VisualElement dropTarget)
        {
            if (!poolCard.IsOwned)
                return;

            var slotIndex = -1;

            for (var i = 0; i < _deckSlots.Count; i++)
            {
                if (_deckSlots[i].Root == dropTarget)
                {
                    slotIndex = i;
                    break;
                }
            }

            if (slotIndex < 0)
                return;

            var slot = _deckSlots[slotIndex];

            if (slot.CurrentType.HasValue)
                _typeToPoolCard[slot.CurrentType.Value].SetInDeck(false);

            var config = _configs.Value.All[poolCard.Definition.Type];
            slot.SetCard(poolCard.Definition, config);
            poolCard.SetInDeck(true);

            var cards = new List<ICardDefinition>();

            foreach (var s in _deckSlots)
                cards.Add(s.CurrentDefinition);

            var selected = _deckService.Configurations[_deckService.SelectedIndex.Value];
            selected.Update(cards);
            RecalculateMana();
        }

        private void RecalculateMana()
        {
            var total = 0f;
            var count = 0;

            foreach (var slot in _deckSlots)
            {
                if (slot.CurrentType.HasValue)
                {
                    total += _configs.Value.All[slot.CurrentType.Value].ManaCost;
                    count++;
                }
            }

            var avg = count > 0 ? total / count : 0f;
            _avgManaLabel.text = avg.ToString("F1");
        }

        private VisualElement CreateGhostCard(ICardDefinition definition, ICardConfig config)
        {
            var card = CloneCard(true);
            card.SetCard(definition, config);
            return card.Root;
        }

        private void RegisterPreviewHover(CardElement card, IReadOnlyLifetime lifetime)
        {
            void OnEnter(PointerEnterEvent evt)
            {
                Debug.Log($"[Preview] Hover.Enter: cardType={card.CurrentType?.ToString() ?? "null"}.");

                if (!card.CurrentType.HasValue)
                    return;
                ShowPreview(card);
            }

            void OnLeave(PointerLeaveEvent evt)
            {
                Debug.Log("[Preview] Hover.Leave.");
                HidePreview();
            }

            card.Root.RegisterCallback<PointerEnterEvent>(OnEnter);
            card.Root.RegisterCallback<PointerLeaveEvent>(OnLeave);

            lifetime.Listen(() => {
                card.Root.UnregisterCallback<PointerEnterEvent>(OnEnter);
                card.Root.UnregisterCallback<PointerLeaveEvent>(OnLeave);
            });
        }

        private void ShowPreview(CardElement card)
        {
            if (!card.CurrentType.HasValue || card.CurrentDefinition == null)
            {
                Debug.Log("[Preview] Decks.ShowPreview: aborting (null type or definition).");
                return;
            }

            var type = card.CurrentType.Value;

            if (_previewPlayer.HasPreview(type) == false)
            {
                Debug.Log($"[Preview] Decks.ShowPreview({type}): no bundle (resource/buff/hand card), skipping popup.");
                return;
            }

            Debug.Log($"[Preview] Decks.ShowPreview({type}): calling Player.Play.");
            _previewPlayer.Play(type);

            var rt = _previewPlayer.PreviewTexture;
            Debug.Log($"[Preview] Decks.ShowPreview({type}): RT={(rt != null ? rt.name : "null")}.");

            if (rt != null)
                _previewImage.style.backgroundImage = Background.FromRenderTexture(rt);
            else
                _previewImage.style.backgroundImage = StyleKeyword.None;

            _previewName.text = card.CurrentDefinition.Name;
            _previewDesc.text = card.CurrentDefinition.Description ?? string.Empty;

            _previewPopup.Show();
            PositionPreview(card.Root);
        }

        private void HidePreview()
        {
            _previewPlayer.Stop();
            _previewPopup.Hide();
        }

        private void PositionPreview(VisualElement anchor)
        {
            var panelWidth = _cardsRoot.resolvedStyle.width;
            var panelHeight = _cardsRoot.resolvedStyle.height;
            var popupWidth = _previewPopup.resolvedStyle.width;
            var popupHeight = _previewPopup.resolvedStyle.height;

            // Защита на случай, если layout ещё не просчитан.
            if (popupWidth <= 0f)
                popupWidth = 96f;

            if (popupHeight <= 0f)
                popupHeight = 120f;

            // Якорный rect относительно cards-root.
            var anchorRect = anchor.ChangeCoordinatesTo(_cardsRoot,
                new Rect(0, 0, anchor.resolvedStyle.width, anchor.resolvedStyle.height));

            // Базовое позиционирование — справа от карточки, сверху совмещено.
            var left = anchorRect.xMax + 2f;
            var top = anchorRect.yMin - 4f;

            // Overflow-safe: если выходит за правый край экрана — отзеркалим на левую сторону карточки.
            if (left + popupWidth > panelWidth)
                left = anchorRect.xMin - popupWidth - 2f;

            // Если и слева не помещается — прижмём к краю.
            if (left < 0f)
                left = 0f;

            // Вертикальная коррекция — чтобы не уходил вниз за экран.
            if (top + popupHeight > panelHeight)
                top = panelHeight - popupHeight;

            if (top < 0f)
                top = 0f;

            _previewPopup.style.left = left;
            _previewPopup.style.top = top;
        }
    }

    /// <summary>
    /// Wrapper around a cloned card UXML template. Provides Q-based access to elements.
    /// </summary>
    public class CardElement
    {
        public VisualElement Root { get; }
        public ICardDefinition CurrentDefinition { get; private set; }
        public ICardDefinition Definition => CurrentDefinition;
        public CardType? CurrentType { get; private set; }
        public bool IsOwned { get; private set; } = true;

        private readonly VisualElement _image;
        private readonly Label _name;
        private readonly Label _mana;
        private readonly Label _desc;

        public CardElement(VisualElement root)
        {
            Root = root;
            _image = Root.Q<VisualElement>("card-image");
            _name = Root.Q<Label>("card-name");
            _mana = Root.Q<Label>("card-mana");
            _desc = Root.Q<Label>("card-info");
        }

        public void SetCard(ICardDefinition definition, ICardConfig config)
        {
            CurrentDefinition = definition;
            CurrentType = definition.Type;

            if (definition.Image != null)
                _image.style.backgroundImage = new StyleBackground(definition.Image);
            else
                _image.style.backgroundImage = StyleKeyword.None;

            _name.text = definition.Name;
            _mana.text = config.ManaCost.ToString();
            _desc.text = definition.Description;
        }

        public void SetOwned(bool owned)
        {
            IsOwned = owned;

            if (owned)
                Root.RemoveFromClassList("unowned");
            else
                Root.AddToClassList("unowned");
        }

        public void SetInDeck(bool inDeck)
        {
            if (inDeck)
                Root.AddToClassList("in-deck");
            else
                Root.RemoveFromClassList("in-deck");
        }
    }
}