using Cysharp.Threading.Tasks;
using GamePlay.Loop;
using Global.UI;
using Internal;
using TMPro;
using UnityEngine;

namespace GamePlay.UI
{
    public enum GameEndMenuResult
    {
        Menu,
        Rematch
    }

    public interface IGameEndUI
    {
        UniTask<GameEndMenuResult> Show(IReadOnlyLifetime lifetime, MatchCompletedData result);
        void SetNotification(string message);
    }

    [DisallowMultipleComponent]
    public class GameEndUI : MonoBehaviour, IGameEndUI, ISceneService
    {
        [SerializeField] private GameEndRating _rating;

        [SerializeField] private DesignButton _menuButton;
        [SerializeField] private DesignButton _rematchButton;

        [SerializeField] private TMP_Text _notification;

        public void Create(IScopeBuilder builder)
        {
            builder.RegisterComponent(this)
                .As<IGameEndUI>();
        }
        
        public async UniTask<GameEndMenuResult> Show(IReadOnlyLifetime lifetime, MatchCompletedData result)
        {
            _rating.Show(result.CurrentRating, result.RatingChange);
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