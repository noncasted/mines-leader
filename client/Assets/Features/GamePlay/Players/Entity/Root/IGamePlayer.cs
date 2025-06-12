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
}