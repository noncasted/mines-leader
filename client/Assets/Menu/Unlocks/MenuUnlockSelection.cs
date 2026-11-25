using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Global.UI;
using Internal;
using Meta;
using Shared;
using TMPro;
using UnityEngine;

namespace Menu.Unlocks
{
    public interface IMenuUnlockSelection : IUIState
    {
        /// <summary>
        /// Opens the tier reward selection and waits until the player claims a card or closes the window.
        /// </summary>
        UniTask Process(IReadOnlyLifetime lifetime, IAchievementTier tier);
    }

    [DisallowMultipleComponent]
    public class MenuUnlockSelection : MonoBehaviour,
                                       IMenuUnlockSelection,
                                       ISceneService,
                                       IUIStateAsyncEnterHandler
    {
        [SerializeField] private RectTransform _optionsRoot;
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _status;

        private readonly List<MenuUnlockOption> _options = new();

        private IAchievementRewards _rewards;
        private ICardsRegistry _cards;
        private ICardDescriptionProvider _descriptions;
        private ICardConfigs _configs;
        private IUIStateMachine _stateMachine;
        private ILog _log;
        private IAchievementTier _tier;
        private bool _isOpen;

        public IUIConstraints Constraints { get; } = UIConstraints.Game;

        [Inject]
        internal void Construct(
            IAchievementRewards rewards,
            ICardsRegistry cards,
            ICardDescriptionProvider descriptions,
            ICardConfigs configs,
            IUIStateMachine stateMachine,
            ILog<MenuUnlockSelection> log)
        {
            _log = log;
            _rewards = rewards;
            _cards = cards;
            _descriptions = descriptions;
            _configs = configs;
            _stateMachine = stateMachine;
        }

        public void Create(IScopeBuilder builder)
        {
            gameObject.SetActive(false);

            builder.RegisterComponent(this)
                   .As<IMenuUnlockSelection>();
        }

        public async UniTask Process(IReadOnlyLifetime lifetime, IAchievementTier tier)
        {
            if (_isOpen == true)
                return;

            _isOpen = true;
            _tier = tier;

            try
            {
                await _stateMachine.ProcessChild(lifetime, _stateMachine.Base, this);
            }
            finally
            {
                _isOpen = false;
            }
        }

        public async UniTask OnEntered(IUIStateHandle handle)
        {
            handle.AttachGameObject(gameObject);

            var lifetime = handle.InnerLifetime;
            var completion = new UniTaskCompletionSource();

            lifetime.Listen(() => completion.TrySetResult());
            lifetime.Listen(ClearOptions);

            _title.text = _tier.Description;
            _status.text = "Loading reward...";

            var options = await _rewards.GetOptions(_tier);

            if (lifetime.IsTerminated == true)
                return;

            // The request did not arrive: keeping the window in loading is pointless, so close it - the tier stays unclaimed.
            if (options == null)
            {
                _log.Error("Failed to load rewards for {Type} tier {Tier}", _tier.Type, _tier.Tier);
                return;
            }

            // Leaving without a choice is impossible, so an empty pool is the only way to close the window without a card.
            if (options.Count == 0)
            {
                _log.Info("No cards left for {Type} tier {Tier}", _tier.Type, _tier.Tier);
                return;
            }

            _status.text = "Pick your reward";
            BuildOptions(lifetime, options, completion);

            await completion.Task;
        }

        private void BuildOptions(
            IReadOnlyLifetime lifetime,
            IReadOnlyList<CardType> cards,
            UniTaskCompletionSource completion)
        {
            ClearOptions();

            // All builds a new dictionary every time, so take it once per layout.
            var configs = _configs.Value?.All;

            foreach (var card in cards)
            {
                if (_cards.Entries.TryGetValue(card, out var definition) == false)
                    continue;

                var manaCost = configs != null && configs.TryGetValue(card, out var config) ? config.ManaCost : 0;

                var option = Instantiate(MenuPrefabs.MenuUnlocksOption, _optionsRoot);
                option.Setup(definition, _descriptions.GetDescription(card), manaCost);
                option.ListenClick(lifetime, picked => Claim(lifetime, picked, completion).Forget());
                _options.Add(option);
            }
        }

        private async UniTask Claim(
            IReadOnlyLifetime lifetime,
            MenuUnlockOption picked,
            UniTaskCompletionSource completion)
        {
            SetOptionsInteractable(false);
            _status.text = "Unlocking...";

            var claimed = false;

            try
            {
                claimed = await _rewards.Claim(_tier, picked.Card);
            }
            catch (Exception e) when (e is NetworkRequestException || e is TimeoutException || e is OperationCanceledException)
            {
                // Claim is called through Forget, and without a catch the window would stay in "Unlocking..." forever.
                // Only request failures are caught, as in AchievementRewards.GetOptions: a code error propagates up.
                _log.Error(e, "Claim failed");
            }

            if (lifetime.IsTerminated == true)
                return;

            if (claimed == false)
            {
                _status.text = "Failed to unlock, try again";
                SetOptionsInteractable(true);
                return;
            }

            completion.TrySetResult();
        }

        private void SetOptionsInteractable(bool value)
        {
            foreach (var option in _options)
                option.SetInteractable(value);
        }

        private void ClearOptions()
        {
            foreach (var option in _options)
            {
                if (option != null)
                    Destroy(option.gameObject);
            }

            _options.Clear();
        }
    }
}
