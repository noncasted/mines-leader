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
        bool IsGameStarted { get; }
        int CardMovesCost { get; }

        void AddPlayer(IGamePlayer player);
        void SetGameStarted(int cardMovesCost);
    }

    public class GameContext : IGameContext
    {
        private IGamePlayer _self;
        private IGamePlayer _other;
        private bool _isGameStarted;
        private int _cardMovesCost = 1;

        private readonly List<IGamePlayer> _all = new();

        public IGamePlayer Self => _self;
        public IGamePlayer Other => _other;
        public IReadOnlyList<IGamePlayer> All => _all;
        public bool IsGameStarted => _isGameStarted;
        public int CardMovesCost => _cardMovesCost;

        public void AddPlayer(IGamePlayer player)
        {
            if (player.Info.IsLocal == true)
                _self = player;
            else
                _other = player;

            _all.Add(player);
        }

        public void SetGameStarted(int cardMovesCost)
        {
            _isGameStarted = true;
            _cardMovesCost = cardMovesCost;
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