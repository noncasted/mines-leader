using Cysharp.Threading.Tasks;
using GamePlay.Loop;
using Internal;
using Shared;
using UnityEngine;

namespace GamePlay.UI
{
    [DisallowMultipleComponent]
    public class GameResultsView : MonoBehaviour
    {
        [SerializeField] private GameUIResultsBindings _bindings;

        public async UniTask<GameEndMenuResult> Show(IReadOnlyLifetime lifetime, MatchCompletedData result)
        {
            var stats = result.Stats;

            var resultDataBindings = _bindings.ResultData;
            var buttonsBindings = _bindings.Buttons;
            var ratingText = resultDataBindings.Rating.Text.TextMeshProUGUI;
            var timeText = resultDataBindings.Time.Text.TextMeshProUGUI;
            var menuButton = buttonsBindings.Menu;
            var rematchButton = buttonsBindings.Rematch;
            var stats1 = resultDataBindings.Stats0;
            var stats2 = resultDataBindings.Stats1;

            ratingText.text = result.RatingChange > 0 ? $"+{result.RatingChange}" : result.RatingChange.ToString();
            timeText.text = result.Duration.ToString(@"mm\:ss");

            stats1.Flags.TextMeshProUGUI.text = Format("flags", stats, UserStatType.FlagsSet);
            stats1.Cards.TextMeshProUGUI.text = Format("cards", stats, UserStatType.CardsPlayed);
            stats2.Reveal.TextMeshProUGUI.text = Format("reveal", stats, UserStatType.CellsOpened);
            stats2.Taken.TextMeshProUGUI.text = Format("attack", stats, UserStatType.EnemyCellsPlanted);

            if (result.Type == MatchResultType.Leave)
                menuButton.GameObject.SetActive(false);
            else
                menuButton.GameObject.SetActive(true);

            gameObject.SetActive(true);

            var completion = new UniTaskCompletionSource<GameEndMenuResult>();

            menuButton.Button.ListenClick(lifetime, () => completion.TrySetResult(GameEndMenuResult.Menu));
            rematchButton.Button.ListenClick(lifetime, () => completion.TrySetResult(GameEndMenuResult.Rematch));

            var selection = await completion.Task;

            menuButton.GameObject.SetActive(false);
            rematchButton.GameObject.SetActive(false);

            return selection;
        }

        public void SetNotification(string message)
        {
            _bindings.Buttons.Notification.TextMeshProUGUI.text = message;
        }

        private static string Format(string label, IUserStatsState stats, UserStatType type)
        {
            var amount = stats?.Get(type) ?? 0;

            return $"{label}: {amount}";
        }
    }
}