using Common.Network;
using Common.Reactive;
using Game.Session;
using Shared;

namespace Game.GamePlay.CardPreviews;

/// <summary>
/// Production-safe IPlayer stub used strictly by <see cref="CardPreviewGenerator"/>.
/// Surfaces the subset of members preview-card mechanics touch: board, user id,
/// modifiers (all zero) and health (full, damage no-op). Anything else throws so
/// any unsupported card fails loudly at generation time instead of shipping broken bundles.
/// </summary>
internal sealed class PreviewPlayer : IPlayer
{
    public PreviewPlayer(IBoard board)
    {
        Board = board;
        User = new PreviewUser(board.OwnerId);

        var modifiers = new Modifiers();
        var health = new Health(modifiers);

        Modifiers = modifiers;
        Health = health;
    }

    public IUser User { get; }
    public IBoard Board { get; }
    public IModifiers Modifiers { get; }
    public IHealth Health { get; }

    public IStash Stash => throw new NotSupportedException("PreviewPlayer does not expose Stash");
    public IDeck Deck => throw new NotSupportedException("PreviewPlayer does not expose Deck");
    public IHand Hand => throw new NotSupportedException("PreviewPlayer does not expose Hand");
    public IMana Mana => throw new NotSupportedException("PreviewPlayer does not expose Mana");
    public IMoves Moves => throw new NotSupportedException("PreviewPlayer does not expose Moves");
    public IPlayerActions Actions => throw new NotSupportedException("PreviewPlayer does not expose Actions");

    private sealed class PreviewUser : IUser
    {
        public PreviewUser(Guid id)
        {
            Id = id;
            Lifetime = new Common.Reactive.Lifetime();
        }

        public Guid Id { get; }
        public int Index => 0;
        public bool IsBot => false;
        public ILifetime Lifetime { get; }
        public IConnection Connection => throw new NotSupportedException("PreviewUser has no connection");
        public ICommandDispatcher Dispatcher => throw new NotSupportedException("PreviewUser has no dispatcher");
    }
}
