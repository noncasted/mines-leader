using System;
using GamePlay.Boards;
using GamePlay.Cards;
using GamePlay.Players;
using Shared;
using VContainer.Unity;

namespace Menu.Decks
{
    /// <summary>
    /// Stub <see cref="IGamePlayer"/> used only by the menu card preview pipeline.
    /// Exposes <see cref="Board"/> (the Menu_Board) and a stable <see cref="Id"/> — everything
    /// else throws so we catch any sync accidentally touching hand/deck/mana of a player that
    /// doesn't really exist in the menu scope.
    /// </summary>
    public sealed class MenuPreviewGamePlayer : IGamePlayer
    {
        public MenuPreviewGamePlayer(IBoard board)
        {
            Board = board;
            Info = new MenuPreviewGamePlayerInfo(Guid.Empty);
        }

        public Guid Id => Info.Id;
        public IGamePlayerInfo Info { get; }
        public IBoard Board { get; }

        public LifetimeScope Scope => null;

        public IPlayerMana Mana => throw new NotSupportedException("MenuPreviewGamePlayer has no mana");
        public IPlayerHealth Health => throw new NotSupportedException("MenuPreviewGamePlayer has no health");
        public IPlayerTurns Turns => throw new NotSupportedException("MenuPreviewGamePlayer has no turns");
        public IHand Hand => throw new NotSupportedException("MenuPreviewGamePlayer has no hand");
        public IPlayerModifiers Modifiers => throw new NotSupportedException("MenuPreviewGamePlayer has no modifiers");
    }

    internal sealed class MenuPreviewGamePlayerInfo : IGamePlayerInfo
    {
        public MenuPreviewGamePlayerInfo(Guid id)
        {
            Id = id;
        }

        public Guid Id { get; }
        public bool IsLocal => true;
        public CharacterType SelectedCharacter => default;
    }
}
