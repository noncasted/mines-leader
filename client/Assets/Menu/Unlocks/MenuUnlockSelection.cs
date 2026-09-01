using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Global.UI;
using Internal;
using Meta;
using Shared;
using TMPro;
using UnityEngine;
using VContainer;

namespace Menu.Unlocks
{
    public interface IMenuUnlockSelection : IUIState
    {
        /// <summary>
        /// Открывает выбор награды за тир и ждёт, пока игрок заберёт карту или закроет окно.
        /// </summary>
        UniTask Process(IAchievementTier tier);
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
        private IUIStateMachine _stateMachine;
        private IAchievementTier _tier;
        private bool _isOpen;

        public IUIConstraints Constraints { get; } = UIConstraints.Game;

        [Inject]
        internal void Construct(
            IAchievementRewards rewards,
            ICardsRegistry cards,
            ICardDescriptionProvider descriptions,
            IUIStateMachine stateMachine)
        {
            _rewards = rewards;
            _cards = cards;
            _descriptions = descriptions;
            _stateMachine = stateMachine;
        }

        public void Create(IScopeBuilder builder)
        {
            gameObject.SetActive(false);

            builder.RegisterComponent(this)
                   .As<IMenuUnlockSelection>();
        }

        public async UniTask Process(IAchievementTier tier)
        {
            if (_isOpen == true)
                return;

            _isOpen = true;
            _tier = tier;

            try
            {
                await _stateMachine.ProcessChild(_stateMachine.Base, this);
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

            // Выйти без выбора нельзя, поэтому пустой пул — единственный способ закрыть окно без карты.
            if (options.Count == 0)
            {
                Debug.Log($"[MenuUnlockSelection] No cards left for {_tier.Type} tier {_tier.Tier}");
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

            foreach (var card in cards)
            {
                if (_cards.Entries.TryGetValue(card, out var definition) == false)
                    continue;

                var option = Instantiate(MenuPrefabs.MenuUnlocksOption, _optionsRoot);
                option.Setup(definition, _descriptions.GetDescription(card));
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

            var claimed = await _rewards.Claim(_tier, picked.Card);

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
