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

        var boardProperty = entityBuilder.AddProperty<BoardState>(PlayerStateIds.Board);
        var healthProperty = entityBuilder.AddProperty<PlayerHealthState>(PlayerStateIds.Health);
        var manaProperty = entityBuilder.AddProperty<PlayerManaState>(PlayerStateIds.Mana);
        var modifiersProperty = entityBuilder.AddProperty<PlayerModifiersState>(PlayerStateIds.Modifiers);
        var movesProperty = entityBuilder.AddProperty<PlayerMovesState>(PlayerStateIds.Moves);
        var deckProperty = entityBuilder.AddProperty<PlayerDeckState>(PlayerStateIds.Deck);
        var handProperty = entityBuilder.AddProperty<PlayerHandState>(PlayerStateIds.Hand);
        var stashProperty = entityBuilder.AddProperty<PlayerStashState>(PlayerStateIds.Stash);

        entityBuilder.WithPayload(new PlayerCreatePayload()
        {
            Name = $"User_{user.Index}",
            Id = user.Id,
            SelectedCharacter = CharacterType.BIBA
        });

        var entity = entityBuilder.Build();

        var board = new Board(boardProperty, entity.Owner.Id, _boardOptions);
        var health = new Health(healthProperty);
        var modifiers = new Modifiers(modifiersProperty);
        var mana = new Mana(manaProperty, modifiers);
        var deck = new Deck(deckProperty, selectedDeck);
        var moves = new Moves(movesProperty);
        var hand = new Hand(handProperty);
        var stash = new Stash(stashProperty);
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

        deckProperty.Update(state => state.Queue = new List<CardType>());

        return player;
    }
}