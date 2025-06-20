using System.Collections.Generic;
using System.Linq;
using GamePlay.Players;

namespace GamePlay.Loop
{
    public class GameContext : IGameContext
    {
        private readonly GameOptions _options = new GameOptions();
        
        private IGamePlayer _self;
        private IReadOnlyList<IGamePlayer> _all;

        private bool _isFirstOpened;

        public IGamePlayer Self => _self;
        public IReadOnlyList<IGamePlayer> All => _all;
        public GameOptions Options => _options;
        public bool IsFirstOpened => _isFirstOpened;

        public void CompleteSetup(IReadOnlyList<IGamePlayer> players)
        {
            _self = players.First(t => t.Info.IsLocal == true);
            _all = players;
        }

        public void OnFirstOpen()
        {
            _isFirstOpened = true;
        }
    }
}