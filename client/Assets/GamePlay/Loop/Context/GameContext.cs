using System;
using System.Collections.Generic;
using System.Linq;
using GamePlay.Players;

namespace GamePlay.Loop
{
    public interface IGameContext
    {
        IGamePlayer Self { get; }
        IGamePlayer Other { get; }
        IReadOnlyList<IGamePlayer> All { get; }

        GameOptions Options { get; }
        bool IsFirstOpened { get; }
        
        void CompleteSetup(IReadOnlyList<IGamePlayer> players);
        void OnFirstOpen();
    }
    
    public class GameContext : IGameContext
    {
        private readonly GameOptions _options = new GameOptions();
        
        private IGamePlayer _self;
        private IGamePlayer _other;
        private IReadOnlyList<IGamePlayer> _all;

        private bool _isFirstOpened;

        public IGamePlayer Self => _self;
        public IGamePlayer Other => _other;
        public IReadOnlyList<IGamePlayer> All => _all;
        public GameOptions Options => _options;
        public bool IsFirstOpened => _isFirstOpened;

        public void CompleteSetup(IReadOnlyList<IGamePlayer> players)
        {
            _self = players.First(t => t.Info.IsLocal == true);
            _other = players.First(t => t.Info.IsLocal == false);
            _all = players;
        }

        public void OnFirstOpen()
        {
            _isFirstOpened = true;
        }
    }
    
    public static class GameContextExtensions
    {
        public static IGamePlayer GetPlayer(this IGameContext context, Guid id)
        {
            return context.All.First(x => x.Id == id);
        }
    }
}