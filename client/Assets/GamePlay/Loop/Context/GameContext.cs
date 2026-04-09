using System;
using System.Collections.Generic;
using System.Linq;
using GamePlay.Players;
using Internal;

namespace GamePlay.Loop
{
    public interface IGameContext
    {
        IGamePlayer Self { get; }
        IGamePlayer Other { get; }
        IReadOnlyList<IGamePlayer> All { get; }
        IViewableDelegate Updated { get; }

        void AddPlayer(IGamePlayer player);
    }

    public class GameContext : IGameContext
    {
        private IGamePlayer _self;
        private IGamePlayer _other;

        private readonly List<IGamePlayer> _all = new();
        private readonly ViewableDelegate _updated = new();

        public IGamePlayer Self => _self;
        public IGamePlayer Other => _other;
        public IReadOnlyList<IGamePlayer> All => _all;
        public IViewableDelegate Updated => _updated;

        public void AddPlayer(IGamePlayer player)
        {
            if (player.Info.IsLocal == true)
                _self = player;
            else
                _other = player;

            _all.Add(player);
            _updated.Invoke();
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