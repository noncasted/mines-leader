using Microsoft.Extensions.Options;
using Shared;

namespace Game.GamePlay;

public interface IPlayerFactory
{
    IPlayer Create(IUser user);
}

public class PlayerFactory : IPlayerFactory
{
    public PlayerFactory(IEntityFactory entityFactory, IOptions<GameOptions> options)
    {
        _entityFactory = entityFactory;
        _options = options;
    }

    private readonly IEntityFactory _entityFactory;
    private readonly IOptions<GameOptions> _options;

    public IPlayer Create(IUser user)
    {
        var options = _options.Value;
        var entityBuilder = _entityFactory.Create(user);

        var healthProperty = new ValueProperty<PlayerHealthState>(0);
        var manaProperty = new ValueProperty<PlayerManaState>(1);
        var modifiersProperty = new ValueProperty<PlayerModifiersState>(2);
        var deckProperty = new ValueProperty<PlayerDeckState>(3);
        var turnsProperty = new ValueProperty<PlayerMovesState>(4);
        var handProperty = new ValueProperty<PlayerHandState>(5);
        var stashProperty = new ValueProperty<PlayerStashState>(6);

        entityBuilder.WithProperty(healthProperty);
        entityBuilder.WithProperty(manaProperty);
        entityBuilder.WithProperty(modifiersProperty);
        entityBuilder.WithProperty(deckProperty);

        entityBuilder.WithPayload(new PlayerCreatePayload());

        var entity = entityBuilder.Build();

        var health = new Health(healthProperty);
        var mana = new Mana(manaProperty);
        var modifiers = new Modifiers(modifiersProperty);
        var deck = new Deck(deckProperty);
        var turns = new Moves(turnsProperty, options.MovesCount);
        var hand = new Hand(handProperty, options.HandSize);
        var stash = new Stash(stashProperty);

        var player = new Player(entity, health, mana, modifiers, deck, turns, hand, stash);

        return player;
    }
}