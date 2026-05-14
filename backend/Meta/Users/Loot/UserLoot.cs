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

public class LootBoxAdded
{
    public Guid Id { get; set; }
}

public class LootBoxRemoved
{
    public Guid Id { get; set; }
}

public class LootBoxesCalculated
{
    public Dictionary<Guid, LootBoxEntry> Boxes { get; set; } = new();
    public int AwardedCount { get; set; }
}

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
        
        await _state.Read();
        var currentAwarded = _state.Value.AwardedCount;

        if (crossedCount <= currentAwarded)
            return;

        var toAward = crossedCount - currentAwarded;

        _logger.LogInformation(
            "[User] [Loot] User {Id} has {TotalXp} XP, crossed {Crossed} thresholds, awarding {ToAward} new boxes",
            this.GetPrimaryKey(), totalXp, crossedCount, toAward);

        var newBoxes = new Dictionary<Guid, LootBoxEntry>();
        for (var i = 0; i < toAward; i++)
        {
            var id = Guid.NewGuid();
            newBoxes[id] = new LootBoxEntry
            {
                Id = id,
                AwardedDate = DateTime.UtcNow
            };
        }

        await _state.Append(new LootBoxesCalculated
        {
            Boxes = _state.Value.Boxes.ToDictionary(k => k.Key, v => v.Value), // preserve existing
            AwardedCount = crossedCount
        });
        // Note: We could just append the new boxes as separate events, but for Recalculate,
        // a bulk update of the set is cleaner to avoid event log bloat.
        // Wait, Apply for LootBoxesCalculated replaces the whole dictionary.
        // Let's fix Apply to merge.
        
        await _state.Write();
        await this.SendProjection(_state.Value);
    }

    public async Task AddBox()
    {
        var id = Guid.NewGuid();

        _logger.LogInformation("[User] [Loot] User {Id} received loot box",
            this.GetPrimaryKey());

        await _state.Read();
        await _state.Append(new LootBoxAdded { Id = id });
        await _state.Write();
        await this.SendProjection(_state.Value);
    }

    public Task<LootBoxEntry?> GetBox(Guid id)
    {
        return _state.ReadAndGetBox(id);
    }

    public async Task<bool> TryRemoveBox(Guid id)
    {
        await _state.Read();
        var exists = _state.Value.Boxes.ContainsKey(id);

        if (!exists)
            return false;

        await _state.Append(new LootBoxRemoved { Id = id });
        await _state.Write();
        await this.SendProjection(_state.Value);
        return true;
    }

    public async Task RemoveBox(Guid id)
    {
        await _state.Read();
        await _state.Append(new LootBoxRemoved { Id = id });
        await _state.Write();
        await this.SendProjection(_state.Value);
    }

    public Task<IProjectionPayload> GetProjection()
    {
        return Task.FromResult((IProjectionPayload)_state.Value);
    }
}

public static class UserLootEventStateExtensions
{
    public static async Task<LootBoxEntry?> ReadAndGetBox(this EventState<UserLootState> state, Guid id)
    {
        await state.Read();
        return state.Value.Boxes.TryGetValue(id, out var entry) ? entry : null;
    }
}
