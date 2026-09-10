using Cysharp.Threading.Tasks;
using GamePlay.Loop;
using Internal;
using UnityEngine;

namespace GamePlay.UI
{
    public enum GameEndMenuResult
    {
        Menu,
        Rematch
    }
    
    public interface IGameResults
    {
        UniTask<GameEndMenuResult> Show(IReadOnlyLifetime lifetime, MatchCompletedData result);
        void SetNotification(string message);
    }

    [DisallowMultipleComponent]
    public class GameResults : MonoBehaviour, IGameResults, ISceneService
    {
        [SerializeField] private GameResultsView _win;
        [SerializeField] private GameResultsView _lose;

        private GameResultsView _current;

        private IGameContext _gameContext;

        internal void Construct(IGameContext gameContext)
        {
            _gameContext = gameContext;
        }

        public void Create(IScopeBuilder builder)
        {
            builder.RegisterComponent(this)
                   .As<IGameResults>();

            gameObject.SetActive(false);
        }

        public UniTask<GameEndMenuResult> Show(IReadOnlyLifetime lifetime, MatchCompletedData result)
        {
            _gameContext.SetPaused(true);

            gameObject.SetActive(true);
            _current = result.Type == MatchResultType.Win ? _win : _lose;

            return _current.Show(lifetime, result);
        }

        public void SetNotification(string message)
        {
            if (_current == null)
                return;

            _current.SetNotification(message);
        }
    }
}
