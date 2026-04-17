using Game.Session;
using Infrastructure;
using Meta.Users;
using Microsoft.Extensions.Options;
using Shared;
using IUser = Game.Session.IUser;

namespace Game.GamePlay;

public interface IPlayerFactory
{
    Task<IPlayer> Create(IUser user);
}

public class PlayerFactory : IPlayerFactory
{
    public PlayerFactory(
        IOrleans orleans,
        IEntityFactory entityFactory,
        IOptions<BoardOptions> boardOptions)
    {
        _orleans = orleans;
        _entityFactory = entityFactory;
        _boardOptions = boardOptions;
    }

    private readonly IOrleans _orleans;
    private readonly IEntityFactory _entityFactory;
    private readonly IOptions<BoardOptions> _boardOptions;

    public async Task<IPlayer> Create(IUser user)
    {
        var userHandle = _orleans.CreateUserHandle(user.Id);
        var selectedDeck = await _orleans.Transactions.Run(() => userHandle.Deck.GetSelected());

        var entityBuilder = _entityFactory.Create(user);

        entityBuilder.WithPayload(new PlayerCreatePayload()
        {
            Name = $"User_{user.Index}",
            Id = user.Id,
            SelectedCharacter = CharacterType.BIBA
        });

        var entity = entityBuilder.Build();

        var board = new Board(entity.Owner.Id, _boardOptions);
        var modifiers = new Modifiers();
        var health = new Health(modifiers);
        var mana = new Mana(modifiers);
        var deck = new Deck(selectedDeck);
        var moves = new Moves(modifiers);
        var hand = new Hand();
        var stash = new Stash();
        var actions = new PlayerActions();

        var player = new Player(entity: entity,
            board: board,
            health: health,
            mana: mana,
            modifiers: modifiers,
            deck: deck,
            moves: moves,
            hand: hand,
            stash: stash,
            playerActions: actions);

        modifiers.BindOwner(player);
        mana.BindOwner(player);
        health.BindOwner(player);
        moves.BindOwner(player);

        return player;
    }
}