using Cluster.Configs;
using Common;
using Infrastructure;
using Infrastructure.State;
using Microsoft.Extensions.Logging;
using Shared;

namespace Meta.Users;

public interface IUserLoot : IUserGrain
{
    [Transaction]
    Task Initialize();

    [Transaction]
    Task Recalculate();

    [Transaction]
    Task AddBox();

    [Transaction]
    Task<LootBoxEntry?> GetBox(Guid id);

    [Transaction]
    Task<bool> TryRemoveBox(Guid id);

    [Transaction]
    Task RemoveBox(Guid id);
}

[GenerateSerializer]
[GrainState(Table = "state_user_loot", State = "user_loot", Lookup = "UserLoot", Key = GrainKeyType.Guid)]
public class UserLootState : IProjectionPayload, IStateValue
{
    [Id(0)] public Dictionary<Guid, LootBoxEntry> Boxes { get; set; } = new();
    [Id(1)] public int AwardedCount { get; set; }

    public int Version => 0;

    public INetworkContext ToContext() => new SharedBackendUser.LootProjection
    {
        AwardedCount = AwardedCount,
        Boxes = Boxes.Values.Select(b => new SharedBackendUser.LootProjection.LootEntry
        {
            Id = b.Id
        }).ToList()
    };
}

[GenerateSerializer]
public class LootBoxEntry
{
    [Id(0)] public Guid Id { get; set; }
    [Id(1)] public DateTime AwardedDate { get; set; }
}

public class UserLoot : UserGrain, IUserLoot
{
    public UserLoot(
        [State] State<UserLootState> state,
        ILootProgressionConfig lootProgressionConfig,
        ILogger<UserLoot> logger)
    {
        _state = state;
        _lootProgressionConfig = lootProgressionConfig;
        _logger = logger;
    }

    private readonly State<UserLootState> _state;
    private readonly ILootProgressionConfig _lootProgressionConfig;
    private readonly ILogger<UserLoot> _logger;

    public async Task Initialize()
    {
        await Recalculate();
    }

    public async Task Recalculate()
    {
        var progression = this.Grains.GetGrain<IUserProgression>(this.GetPrimaryKey());
        var totalXp = await progression.GetTotal();

        var thresholds = _lootProgressionConfig.Value.Thresholds
            .OrderBy(t => t)
            .ToList();

        var crossedCount = thresholds.Count(t => totalXp >= t);
        var currentAwarded = await _state.Read(s => s.AwardedCount);

        if (crossedCount <= currentAwarded)
            return;

        var toAward = crossedCount - currentAwarded;

        _logger.LogInformation(
            "[User] [Loot] User {Id} has {TotalXp} XP, crossed {Crossed} thresholds, awarding {ToAward} new boxes",
            this.GetPrimaryKey(), totalXp, crossedCount, toAward);

        var state = await _state.Update(s => {
            for (var i = 0; i < toAward; i++)
            {
                var id = Guid.NewGuid();
                s.Boxes[id] = new LootBoxEntry
                {
                    Id = id,
                    AwardedDate = DateTime.UtcNow
                };
            }

            s.AwardedCount = crossedCount;
        });

        await this.SendCachedProjection(state);
    }

    public async Task AddBox()
    {
        var id = Guid.NewGuid();

        _logger.LogInformation("[User] [Loot] User {Id} received loot box",
            this.GetPrimaryKey());

        var state = await _state.Update(state => {
            state.Boxes[id] = new LootBoxEntry
            {
                Id = id,
                AwardedDate = DateTime.UtcNow
            };
        });

        await this.SendCachedProjection(state);
    }

    public Task<LootBoxEntry?> GetBox(Guid id)
    {
        return _state.Read(state => state.Boxes.TryGetValue(id, out var entry) ? entry : null);
    }

    public async Task<bool> TryRemoveBox(Guid id)
    {
        var exists = await _state.Read(state => state.Boxes.ContainsKey(id));

        if (!exists)
            return false;

        var state = await _state.Update(state => state.Boxes.Remove(id));
        await this.SendCachedProjection(state);
        return true;
    }

    public async Task RemoveBox(Guid id)
    {
        var state = await _state.Update(state => state.Boxes.Remove(id));
        await this.SendCachedProjection(state);
    }
}
