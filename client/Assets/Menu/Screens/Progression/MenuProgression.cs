using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Global.UI;
using Internal;
using Meta;
using TMPro;
using UnityEngine;
using VContainer;

namespace Menu.Screens
{
    public interface IMenuProgression : IUIState
    {
    }

    [DisallowMultipleComponent]
    public class MenuProgression : MonoBehaviour,
                                   IMenuProgression,
                                   ISceneService,
                                   IUIStateAsyncEnterHandler
    {
        [SerializeField] private DesignButton _backButton;
        [SerializeField] private RectTransform _barFill;
        [SerializeField] private RectTransform _barRoot;
        [SerializeField] private TMP_Text _xpText;
        [SerializeField] private RectTransform _milestonesRoot;
        [SerializeField] private ProgressionMilestone _milestonePrefab;
        [SerializeField] private MenuProgressionSelection _selection;

        private IProgression _progression;

        public IUIConstraints Constraints { get; } = UIConstraints.Game;

        [Inject]
        private void Construct(IProgression progression)
        {
            _progression = progression;
        }

        public void Create(IScopeBuilder builder)
        {
            gameObject.SetActive(false);

            builder.RegisterComponent(this)
                   .As<IMenuProgression>();
        }

        public async UniTask OnEntered(IUIStateHandle handle)
        {
            var lifetime = handle.InnerLifetime;

            handle.AttachGameObject(gameObject);

            var mileStones = new List<ProgressionMilestone>();

            _progression.CurrentProgress.View(lifetime, UpdateBar);

            _progression.Milestones.View(lifetime, milestone => {
                var maxXp = _progression.Milestones.Count > 0 ? _progression.Milestones.Max(m => m.Required) : 1;
                var normalizedPosition = (float)milestone.Required / maxXp;

                var view = Instantiate(_milestonePrefab, _milestonesRoot);
                var position = new Vector2(normalizedPosition * _milestonesRoot.rect.width, 0);
                view.Setup(milestone, lifetime, position);
                mileStones.Add(view);

                view.Button.ListenClick(lifetime, () => OpenLootBox(lifetime, milestone).Forget());
            });

            await _backButton.WaitClick(handle);

            foreach (var milestone in mileStones)
                Destroy(milestone.gameObject);
        }

        private void UpdateBar(int currentXp)
        {
            var maxXp = _progression.Milestones.Count > 0 ? _progression.Milestones.Max(m => m.Required) : 0;
            var fillRatio = maxXp > 0 ? Mathf.Clamp01((float)currentXp / maxXp) : 0f;

            var barWidth = _barRoot.rect.width;
            _barFill.sizeDelta = new Vector2(barWidth * fillRatio, 100);
            _xpText.text = $"{currentXp}";
        }

        private async UniTask OpenLootBox(IReadOnlyLifetime lifetime, IProgressionMilestone milestone)
        {
            var offer = await _progression.OpenLootBox(milestone);

            if (offer.Definitions.Count == 0 || offer.CardTypes.Count == 0)
                return;

            var result = await _selection.Show(lifetime, offer.Definitions);
            await _progression.ChooseLootReward(offer.BoxId, result.Type);
        }
    }
}