using System;
using Cysharp.Threading.Tasks;
using GamePlay.Loop;
using Global.UI;
using Internal;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
        [SerializeField] private TMP_Text _title;
        [SerializeField] private Image _resultImage;
        [SerializeField] private Sprite _winSprite;
        [SerializeField] private Sprite _loseSprite;
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
            switch (result.Type)
            {
                case MatchResultType.Leave:
                case MatchResultType.Win:
                    _title.text = "You Won!";
                    _resultImage.sprite = _winSprite;
                    break;
                case MatchResultType.Lose:
                    _title.text = "You Lose...";
                    _resultImage.sprite = _loseSprite;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (result.Type == MatchResultType.Leave)
                _rematchButton.gameObject.SetActive(false);
            else
                _rematchButton.gameObject.SetActive(true);
            
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