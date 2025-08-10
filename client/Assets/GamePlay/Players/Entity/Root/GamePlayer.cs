using System;
using GamePlay.Boards;
using GamePlay.Cards;
using VContainer.Unity;

namespace GamePlay.Players
{
    public interface IGamePlayer
    {
        Guid Id { get; }
        IGamePlayerInfo Info { get; }
        LifetimeScope Scope { get; }
        IPlayerMana Mana { get; }
        IPlayerHealth Health { get; }
        IPlayerMoves Moves { get; }
        IHand Hand { get; } 
        IBoard Board { get; }
    }
    
    public class GamePlayer : IGamePlayer
    {
        public GamePlayer(
            LifetimeScope scope,
            IHand hand,
            IBoard board,
            IPlayerMana mana,
            IPlayerHealth health,
            IPlayerMoves moves,
            IGamePlayerInfo info)
        {
            Mana = mana;
            
            Scope = scope;
            Hand = hand;
            Board = board;
            Moves = moves;
            Info = info;
            Health = health;
        }

        public Guid Id => Info.Id;
        public LifetimeScope Scope { get; }
        public IPlayerMana Mana { get; }
        public IPlayerHealth Health { get; }
        public IPlayerMoves Moves { get; }
        public IGamePlayerInfo Info { get; }
        public IHand Hand { get; }
        public IBoard Board { get; }
    }
}