using System;
using GamePlay.Boards;
using GamePlay.Cards;
using Internal;

namespace GamePlay.Players
{
    public interface IGamePlayer
    {
        Guid Id { get; }
        IGamePlayerInfo Info { get; }
        IContainer Scope { get; }
        IPlayerMana Mana { get; }
        IPlayerHealth Health { get; }
        IPlayerTurns Turns { get; }
        IHand Hand { get; }
        ICardTable Table { get; }
        IBoard Board { get; }
        IPlayerModifiers Modifiers { get; }
    }

    public class GamePlayer : IGamePlayer
    {
        public GamePlayer(
            IContainer scope,
            IHand hand,
            ICardTable table,
            IBoard board,
            IPlayerMana mana,
            IPlayerHealth health,
            IPlayerTurns turns,
            IGamePlayerInfo info,
            IPlayerModifiers modifiers)
        {
            Mana = mana;

            Scope = scope;
            Hand = hand;
            Table = table;
            Board = board;
            Turns = turns;
            Info = info;
            Health = health;
            Modifiers = modifiers;
        }

        public Guid Id => Info.Id;
        public IContainer Scope { get; }
        public IPlayerMana Mana { get; }
        public IPlayerHealth Health { get; }
        public IPlayerTurns Turns { get; }
        public IGamePlayerInfo Info { get; }
        public IHand Hand { get; }
        public ICardTable Table { get; }
        public IBoard Board { get; }
        public IPlayerModifiers Modifiers { get; }
    }
}