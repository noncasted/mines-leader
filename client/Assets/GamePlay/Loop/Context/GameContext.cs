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

        void AddPlayer(IGamePlayer player);
    }

    public class GameContext : IGameContext
    {
        private readonly GameOptions _options = new();

        private IGamePlayer _self;
        private IGamePlayer _other;
        private readonly List<IGamePlayer> _all = new();

        public IGamePlayer Self => _self;
        public IGamePlayer Other => _other;
        public IReadOnlyList<IGamePlayer> All => _all;
        public GameOptions Options => _options;

        public void AddPlayer(IGamePlayer player)
        {
            if (player.Info.IsLocal == true)
                _self = player;
            else
                _other = player;

            _all.Add(player);
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