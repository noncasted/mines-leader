using System;
using Cysharp.Threading.Tasks;
using GamePlay.Loop;
using Internal;
using Shared;

namespace GamePlay.UI
{
    public interface IGameEnd
    {
        UniTask<IGameEndTransition> Process(IReadOnlyLifetime lifetime, MatchCompletedData matchData);
    }

    public class GameEnd : IGameEnd
    {
        public GameEnd(IGameEndUI ui, IRematchAwaiter rematchAwaiter, INetworkConnection connection)
        {
            _ui = ui;
            _rematchAwaiter = rematchAwaiter;
            _connection = connection;
        }

        private readonly IGameEndUI _ui;
        private readonly IRematchAwaiter _rematchAwaiter;
        private readonly INetworkConnection _connection;

        public async UniTask<IGameEndTransition> Process(IReadOnlyLifetime lifetime, MatchCompletedData matchData)
        {
            if (matchData.Type == MatchResultType.Leave)
                return new GameEndTransition.Exit();

            var selectedType = await _ui.Show(lifetime, matchData);

            if (selectedType == GameEndMenuResult.Menu)
                return new GameEndTransition.Exit();

            _connection.OneWay(new RematchContexts.Request());
            _ui.SetNotification("waiting for opponent");
            var transition = await _rematchAwaiter.Await(lifetime);

            switch (transition)
            {
                case GameEndTransition.Exit:
                {
                    _ui.SetNotification("opponent declined");
                    break;
                }
                case GameEndTransition.Rematch:
                {
                    _ui.SetNotification("opponent accepted");
                    break;
                }
            }

            await UniTask.Delay(TimeSpan.FromSeconds(1));

            return transition;
        }
    }
}