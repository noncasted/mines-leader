using Microsoft.Extensions.Options;
using Shared;

namespace Game.GamePlay;

public interface IPlayerFactory
{
    IPlayer Create(IUser user);
}

public class PlayerFactory : IPlayerFactory
{
    public PlayerFactory(
        IEntityFactory entityFactory,
        IOptions<GameOptions> gameOptions,
        IOptions<BoardOptions> boardOptions)
    {
        _entityFactory = entityFactory;
        _gameOptions = gameOptions;
        _boardOptions = boardOptions;
    }

    private readonly IEntityFactory _entityFactory;
    private readonly IOptions<GameOptions> _gameOptions;
    private readonly IOptions<BoardOptions> _boardOptions;

    public IPlayer Create(IUser user)
    {
        var gameOptions = _gameOptions.Value;
        var entityBuilder = _entityFactory.Create(user);

        var healthProperty = new ValueProperty<PlayerHealthState>(0);
        var manaProperty = new ValueProperty<PlayerManaState>(1);
        var modifiersProperty = new ValueProperty<PlayerModifiersState>(2);
        var deckProperty = new ValueProperty<PlayerDeckState>(3);
        var movesProperty = new ValueProperty<PlayerMovesState>(4);
        var handProperty = new ValueProperty<PlayerHandState>(5);
        var stashProperty = new ValueProperty<PlayerStashState>(6);

        entityBuilder.WithProperty(healthProperty);
        entityBuilder.WithProperty(manaProperty);
        entityBuilder.WithProperty(modifiersProperty);
        entityBuilder.WithProperty(deckProperty);

        entityBuilder.WithPayload(new PlayerCreatePayload()
        {
            Name = $"User_{user.Index}",
            Id = user.Id,
            SelectedCharacter = CharacterType.BIBA
        });

        var entity = entityBuilder.Build();

        var board = new Board(entity.Owner.Id, _boardOptions);
        var health = new Health(healthProperty);
        var mana = new Mana(manaProperty);
        var modifiers = new Modifiers(modifiersProperty);
        var deck = new Deck(deckProperty);
        var moves = new Moves(movesProperty, gameOptions.MovesCount);
        var hand = new Hand(handProperty, gameOptions.HandSize);
        var stash = new Stash(stashProperty);

        var player = new Player(entity, board, health, mana, modifiers, deck, moves, hand, stash);

        return player;
    }
}