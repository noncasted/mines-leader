using Microsoft.Extensions.Options;
using Shared;

namespace Game.GamePlay;

public interface IPlayerFactory
{
    IPlayer Create(IUser user);
}

public class PlayerFactory : IPlayerFactory
{
    public PlayerFactory(IEntityFactory entityFactory, IOptions<BoardOptions> boardOptions)
    {
        _entityFactory = entityFactory;
        _boardOptions = boardOptions;
    }

    private readonly IEntityFactory _entityFactory;
    private readonly IOptions<BoardOptions> _boardOptions;

    public IPlayer Create(IUser user)
    {
        var entityBuilder = _entityFactory.Create(user);

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
            }
        );

        var entity = entityBuilder.Build();

        var board = new Board(entity.Owner.Id, _boardOptions);
        var health = new Health(healthProperty);
        var mana = new Mana(manaProperty);
        var modifiers = new Modifiers(modifiersProperty);
        var deck = new Deck(deckProperty);
        var moves = new Moves(movesProperty);
        var hand = new Hand(handProperty);
        var stash = new Stash(stashProperty);

        var player = new Player(entity, board, health, mana, modifiers, deck, moves, hand, stash);

        return player;
    }
}