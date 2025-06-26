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
        IPlayerTurns Turns { get; }
        IHand Hand { get; } 
        IDeck Deck { get; }
        IBoard Board { get; }
        IStash Stash { get; }
    }
    
    public class GamePlayer : IGamePlayer
    {
        public GamePlayer(
            LifetimeScope scope,
            IHand hand,
            IDeck deck,
            IBoard board,
            IStash stash,
            IPlayerMana mana,
            IPlayerTurns turns,
            IGamePlayerInfo info)
        {
            Mana = mana;
            
            Scope = scope;
            Hand = hand;
            Deck = deck;
            Board = board;
            Stash = stash;
            Turns = turns;
            Info = info;
        }

        public Guid Id => Info.Id;
        public LifetimeScope Scope { get; }
        public IPlayerMana Mana { get; }
        public IPlayerTurns Turns { get; }
        public IGamePlayerInfo Info { get; }
        public IHand Hand { get; }
        public IDeck Deck { get; }
        public IBoard Board { get; }
        public IStash Stash { get; }
    }
}