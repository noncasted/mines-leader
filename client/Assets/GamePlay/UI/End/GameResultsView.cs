using Cysharp.Threading.Tasks;
using GamePlay.Loop;
using Internal;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GamePlay.UI
{
    [DisallowMultipleComponent]
    public class GameResultsView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _rating;
        [SerializeField] private TMP_Text _time;
        [SerializeField] private TMP_Text _notification;
        
        [SerializeField] private GameResultsStats _stats;
        
        [SerializeField] private Button _menuButton;
        [SerializeField] private Button _rematchButton;

        public async UniTask<GameEndMenuResult> Show(IReadOnlyLifetime lifetime, MatchCompletedData result)
        {
            _stats.Show(result.Stats);
            _rating.text = result.RatingChange > 0
                ? $"+{result.RatingChange}"
                : result.RatingChange.ToString();
            _time.text = result.Duration.ToString(@"mm\:ss");
            
            if (result.Type == MatchResultType.Leave)
                _rematchButton.gameObject.SetActive(false);
            else
                _rematchButton.gameObject.SetActive(true);

            gameObject.SetActive(true);

            var completion = new UniTaskCompletionSource<GameEndMenuResult>();

            _menuButton.ListenClick(lifetime, () => completion.TrySetResult(GameEndMenuResult.Menu));
            _rematchButton.ListenClick(lifetime, () => completion.TrySetResult(GameEndMenuResult.Rematch));

            var selection = await completion.Task;

            _menuButton.gameObject.SetActive(false);
            _rematchButton.gameObject.SetActive(false);

            return selection;
        }

        public void SetNotification(string message)
        {
            _notification.text = message;
        }
    }
}