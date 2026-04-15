using Infrastructure;

namespace Meta.Users;

[GenerateSerializer]
public class LootProgressionSideEffect : ISideEffect
{
    [Id(0)] public Guid UserId { get; init; }

    public async Task Execute(IOrleans orleans)
    {
        var loot = orleans.GetGrain<IUserLoot>(UserId);
        await orleans.InTransaction(() => loot.Recalculate());
    }
}