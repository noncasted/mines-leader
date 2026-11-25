using System;
using GamePlay.Boards;
using GamePlay.Cards;
using GamePlay.Players;
using Internal;

namespace GamePlay.Tests
{
    // A player of a match with its board only.
    public class FakePlayer : IGamePlayer
    {
        public FakePlayer(Guid id, IBoard board)
        {
            Id = id;
            Board = board;
        }

        public Guid Id { get; }
        public IBoard Board { get; }

        public IGamePlayerInfo Info => throw new NotSupportedException();
        public IContainer Scope => throw new NotSupportedException();
        public IPlayerMana Mana => throw new NotSupportedException();
        public IPlayerHealth Health => throw new NotSupportedException();
        public IPlayerTurns Turns => throw new NotSupportedException();
        public IHand Hand => throw new NotSupportedException();
        public ICardTable Table => throw new NotSupportedException();
        public IPlayerModifiers Modifiers => throw new NotSupportedException();
    }
}
