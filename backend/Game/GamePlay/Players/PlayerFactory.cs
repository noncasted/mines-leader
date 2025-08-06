using Shared;

namespace Game.GamePlay;

public interface IPlayerFactory
{
    IPlayer Create(IUser user);
}

public class PlayerFactory : IPlayerFactory
{
    public PlayerFactory(IEntityFactory entityFactory)
    {
        _entityFactory = entityFactory;
    }

    private readonly IEntityFactory _entityFactory;

    public IPlayer Create(IUser user)
    {
        var entityBuilder = _entityFactory.Create(user);

        var healthProperty = new ValueProperty<PlayerHealthState>(0);
        var manaProperty = new ValueProperty<PlayerManaState>(1);
        var modifiersProperty = new ValueProperty<PlayerModifiersState>(2);

        entityBuilder.WithProperty(healthProperty);
        entityBuilder.WithProperty(manaProperty);
        entityBuilder.WithProperty(modifiersProperty);

        entityBuilder.WithPayload(new PlayerCreatePayload());
        var entity = entityBuilder.Build();
        
        var health = new Health(healthProperty);
        var mana = new Mana(manaProperty);
        var modifiers = new Modifiers(modifiersProperty);

        var player = new Player(entity, health, mana, modifiers);

        return player;
    }
}