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
        IPlayerTurns Turns { get; }
        IDeck Deck { get; }
        IStash Stash { get; }
        IHand Hand { get; }
        IBoard Board { get; }
        IPlayerModifiers Modifiers { get; }
    }

    public class GamePlayer : IGamePlayer
    {
        public GamePlayer(
            LifetimeScope scope,
            IHand hand,
            IBoard board,
            IPlayerMana mana,
            IPlayerHealth health,
            IPlayerTurns turns,
            IGamePlayerInfo info,
            IDeck deck,
            IStash stash,
            IPlayerModifiers modifiers)
        {
            Mana = mana;

            Scope = scope;
            Hand = hand;
            Board = board;
            Turns = turns;
            Info = info;
            Deck = deck;
            Stash = stash;
            Health = health;
            Modifiers = modifiers;
        }

        public Guid Id => Info.Id;
        public LifetimeScope Scope { get; }
        public IPlayerMana Mana { get; }
        public IPlayerHealth Health { get; }
        public IDeck Deck { get; }
        public IStash Stash { get; }
        public IPlayerTurns Turns { get; }
        public IGamePlayerInfo Info { get; }
        public IHand Hand { get; }
        public IBoard Board { get; }
        public IPlayerModifiers Modifiers { get; }
    }
}