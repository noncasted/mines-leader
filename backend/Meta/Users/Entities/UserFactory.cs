using Infrastructure;

namespace Meta.Users;

public interface IUserFactory
{
    Task<ResolvedUser> Create(UserCreateOptions options);
    Task<ResolvedUser> Resolve(Guid? id);
}

[GenerateSerializer]
public class UserCreateOptions
{
    [Id(0)] public string? Name { get; set; }
}

public class ResolvedUser
{
    public required Guid Id { get; init; }
    public required IReadOnlyList<IProjectionPayload> Projections { get; init; }
}

public class UserFactory : IUserFactory
{
    private readonly IOrleans _orleans;
    private readonly IUserProjectionsLoader _projections;

    public UserFactory(IOrleans orleans, IUserProjectionsLoader projections)
    {
        _orleans = orleans;
        _projections = projections;
    }

    public Task<ResolvedUser> Create(UserCreateOptions options)
    {
        return _orleans.Transactions.Run(async () => {
            var id = await CreateInternal(options);
            return await ToResolved(id);
        });
    }

    /// <summary>
    /// Возвращает существующего юзера или заводит нового, если id не передан или неизвестен.
    /// Проверка, создание и чтение проекций идут одной транзакцией: на хендшейке лишние заходы
    /// в базу стоят клиенту целого round trip.
    /// </summary>
    public Task<ResolvedUser> Resolve(Guid? id)
    {
        return _orleans.Transactions.Run(async () => {
            if (id.HasValue == true)
            {
                var isExists = await _orleans.CreateUserHandle(id.Value).Auth.IsExists();

                if (isExists == true)
                    return await ToResolved(id.Value);
            }

            var createdId = await CreateInternal(new UserCreateOptions());
            return await ToResolved(createdId);
        });
    }

    private async Task<ResolvedUser> ToResolved(Guid id)
    {
        var projections = await _projections.Load(id);

        return new ResolvedUser
        {
            Id = id,
            Projections = projections
        };
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
