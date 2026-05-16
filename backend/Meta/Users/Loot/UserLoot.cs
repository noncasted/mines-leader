using Cluster.Configs;
using Common;
using Infrastructure;
using Infrastructure.State;
using Microsoft.Extensions.Logging;
using Shared;

namespace Meta.Users;

public interface IUserLoot : IUserGrain, IUserProjectionSource
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
[GrainEventState(State = "user_loot", Lookup = "UserLoot", Key = GrainKeyType.Guid)]
public class UserLootState : IEventStateValue, IProjectionPayload
{
    [Id(0)] public string Id { get; set; } = string.Empty;
    [Id(1)] public Dictionary<Guid, LootBoxEntry> Boxes { get; set; } = new();
    [Id(2)] public int AwardedCount { get; set; }

    public int Version => 0;

    public void Apply(LootBoxesCalculated e)
    {
        foreach (var entry in e.Boxes)
            Boxes[entry.Key] = entry.Value;

        AwardedCount = e.AwardedCount;
    }

    public void Apply(LootBoxAdded e)
    {
        Boxes[e.Id] = new LootBoxEntry
        {
            Id = e.Id,
            AwardedDate = DateTime.UtcNow
        };
    }

    public void Apply(LootBoxRemoved e)
    {
        Boxes.Remove(e.Id);
    }

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

public record LootBoxAdded(Guid Id);

public record LootBoxRemoved(Guid Id);

public record LootBoxesCalculated(Dictionary<Guid, LootBoxEntry> Boxes, int AwardedCount);

public class UserLoot : UserGrain, IUserLoot
{
    public UserLoot(
        [EventState] EventState<UserLootState> state,
        ILootProgressionConfig lootProgressionConfig,
        ILogger<UserLoot> logger)
    {
        _state = state;
        _lootProgressionConfig = lootProgressionConfig;
        _logger = logger;
    }

    private readonly EventState<UserLootState> _state;
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

        var state = await _state.Read();
        var currentAwarded = state.AwardedCount;

        if (crossedCount <= currentAwarded)
            return;

        var toAward = crossedCount - currentAwarded;

        _logger.LogInformation(
            "[User] [Loot] User {Id} has {TotalXp} XP, crossed {Crossed} thresholds, awarding {ToAward} new boxes",
            this.GetPrimaryKey(), totalXp, crossedCount, toAward);

        var allBoxes = state.Boxes.ToDictionary(k => k.Key, v => v.Value);
        for (var i = 0; i < toAward; i++)
        {
            var id = Guid.NewGuid();
            allBoxes[id] = new LootBoxEntry
            {
                Id = id,
                AwardedDate = DateTime.UtcNow
            };
        }

        var newState = await _state.Apply(new LootBoxesCalculated(allBoxes, crossedCount));
        await this.SendProjection(newState);
    }

    public async Task AddBox()
    {
        var id = Guid.NewGuid();

        _logger.LogInformation("[User] [Loot] User {Id} received loot box",
            this.GetPrimaryKey());

        var state = await _state.Apply(new LootBoxAdded(id));
        await this.SendProjection(state);
    }

    public async Task<LootBoxEntry?> GetBox(Guid id)
    {
        var state = await _state.Read();
        return state.Boxes.TryGetValue(id, out var e) ? e : null;
    }

    public async Task<bool> TryRemoveBox(Guid id)
    {
        var state = await _state.Read();
        var exists = state.Boxes.ContainsKey(id);

        if (!exists)
            return false;

        var newState = await _state.Apply(new LootBoxRemoved(id));
        await this.SendProjection(newState);
        return true;
    }

    public async Task RemoveBox(Guid id)
    {
        var state = await _state.Apply(new LootBoxRemoved(id));
        await this.SendProjection(state);
    }

    public async Task<IProjectionPayload> GetProjection()
    {
        var state = await _state.Read();
        return state;
    }
}
