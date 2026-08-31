using Infrastructure;

namespace Meta.Users;

public interface IUserFactory
{
    Task<Guid> Create(UserCreateOptions options);
    Task<Guid> Resolve(Guid? id);
}

[GenerateSerializer]
public class UserCreateOptions
{
    [Id(0)] public string? Name { get; set; }
}

public class UserFactory : IUserFactory
{
    private readonly IOrleans _orleans;

    public UserFactory(IOrleans orleans)
    {
        _orleans = orleans;
    }

    public Task<Guid> Create(UserCreateOptions options)
    {
        return _orleans.Transactions.Run(() => CreateInternal(options));
    }

    /// <summary>
    /// Возвращает id существующего юзера или заводит нового, если id не передан или неизвестен.
    /// Проверка и создание идут одной транзакцией: на хендшейке лишние заходы в базу стоят
    /// клиенту целого round trip.
    /// </summary>
    public Task<Guid> Resolve(Guid? id)
    {
        return _orleans.Transactions.Run(async () => {
            if (id.HasValue == true)
            {
                var isExists = await _orleans.CreateUserHandle(id.Value).Auth.IsExists();

                if (isExists == true)
                    return id.Value;
            }

            return await CreateInternal(new UserCreateOptions());
        });
    }

    private async Task<Guid> CreateInternal(UserCreateOptions options)
    {
        var id = Guid.NewGuid();

        var handle = _orleans.CreateUserHandle(id);
        var name = options.Name ?? $"User_{id.ToString()[..8]}";

        // Грейны независимые, поэтому инициализация идёт веером; последовательна только
        // цепочка самой сущности, где имя ставится поверх Initialize.
        await Task.WhenAll(
            InitializeEntity(),
            handle.Deck.Initialize(),
            handle.Cards.Initialize(),
            handle.Auth.OnRegistered());

        return id;

        async Task InitializeEntity()
        {
            await handle.Entity.Initialize();
            await handle.Entity.SetName(name);
        }
    }
}
