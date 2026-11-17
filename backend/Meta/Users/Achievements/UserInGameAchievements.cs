using Cluster.Configs;
using Common;
using Infrastructure;
using Infrastructure.State;
using Microsoft.Extensions.Logging;
using Shared;

namespace Meta.Users;

public interface IUserInGameAchievements : IUserGrain, IUserProjectionSource
{
    /// <summary>
    /// Сверяет статы игрока с конфигом и открывает все ачивки, условия которых выполнены.
    /// Награда при этом не выдаётся: игрок забирает её сам через
    /// <see cref="GetRewardOptions"/> и <see cref="ClaimReward"/>.
    /// Вызывается сайд-эффектом от <see cref="IUserStats"/> после обновления статов.
    /// </summary>
    [Transaction]
    Task<IReadOnlyList<InGameAchievementEntry>> Evaluate();

    /// <summary>
    /// Варианты награды за открытую ачивку. Пул фиксируется при первом запросе,
    /// поэтому повторный вызов возвращает те же карты и рероллить их нельзя.
    /// </summary>
    [Transaction]
    Task<IReadOnlyList<CardType>> GetRewardOptions(InGameAchievementType type, int tier);

    /// <summary>
    /// Выдаёт выбранную карту. Карта должна быть из списка <see cref="GetRewardOptions"/>.
    /// </summary>
    [Transaction]
    Task<bool> ClaimReward(InGameAchievementType type, int tier, CardType card);

    [Transaction]
    Task<IReadOnlyList<InGameAchievementEntry>> GetUnlocked();

    [Transaction]
    Task Reset();
}

[GenerateSerializer]
public class InGameAchievementEntry
{
    [Id(0)] public InGameAchievementType Type { get; set; }
    [Id(1)] public int Tier { get; set; }
    [Id(2)] public DateTime Date { get; set; }
    [Id(3)] public CardType? UnlockedCard { get; set; }
    [Id(4)] public List<CardType> RewardOptions { get; set; } = new();
    [Id(5)] public bool Claimed { get; set; }
}

[GenerateSerializer]
[GrainEventState(State = "user_in_game_achievements", Lookup = "UserInGameAchievements", Key = GrainKeyType.Guid)]
public class UserInGameAchievementsState : IEventStateValue, IProjectionPayload
{
    [Id(0)] public string Id { get; set; } = string.Empty;
    [Id(1)] public List<InGameAchievementEntry> Unlocked { get; set; } = new();

    public int Version => 0;

    public void Apply(InGameAchievementUnlocked e) => Unlocked.Add(e.Entry);

    public void Apply(InGameAchievementsReset e) => Unlocked.Clear();

    public void Apply(InGameAchievementRewardOffered e)
    {
        var entry = Find(e.Type, e.Tier);
        if (entry == null)
            return;

        entry.RewardOptions = e.Options.ToList();
    }

    public void Apply(InGameAchievementRewardClaimed e)
    {
        var entry = Find(e.Type, e.Tier);
        if (entry == null)
            return;

        entry.UnlockedCard = e.Card;
        entry.Claimed = true;
    }

    public InGameAchievementEntry? Find(InGameAchievementType type, int tier)
    {
        foreach (var entry in Unlocked)
        {
            if (entry.Type == type && entry.Tier == tier)
                return entry;
        }

        return null;
    }

    public bool IsUnlocked(InGameAchievementType type, int tier) => Find(type, tier) != null;

    public INetworkContext ToContext() => new SharedBackendUser.InGameAchievementsProjection
    {
        Unlocked = Unlocked
                   .Select(entry => new SharedBackendUser.InGameAchievementsProjection.UnlockedAchievement
                   {
                       Type = entry.Type,
                       Tier = entry.Tier,
                       Date = entry.Date,
                       Claimed = entry.Claimed,
                       RewardCard = entry.UnlockedCard
                   })
                   .ToList()
    };
}

[GenerateSerializer]
public record InGameAchievementUnlocked(InGameAchievementEntry Entry);

[GenerateSerializer]
public record InGameAchievementRewardOffered(
    InGameAchievementType Type,
    int Tier,
    IReadOnlyList<CardType> Options);

[GenerateSerializer]
public record InGameAchievementRewardClaimed(InGameAchievementType Type, int Tier, CardType? Card);

[GenerateSerializer]
public record InGameAchievementsReset;

public class UserInGameAchievements : UserGrain, IUserInGameAchievements
{
    public UserInGameAchievements(
        [EventState] EventState<UserInGameAchievementsState> state,
        IInGameAchievementConfig achievementConfig,
        ICardConfigs cardConfigs,
        ILogger<UserInGameAchievements> logger)
    {
        _state = state;
        _achievementConfig = achievementConfig;
        _cardConfigs = cardConfigs;
        _logger = logger;
    }

    private readonly EventState<UserInGameAchievementsState> _state;
    private readonly IInGameAchievementConfig _achievementConfig;
    private readonly ICardConfigs _cardConfigs;
    private readonly ILogger<UserInGameAchievements> _logger;

    private const int RewardOptionsCount = 3;

    public async Task<IReadOnlyList<InGameAchievementEntry>> Evaluate()
    {
        var userId = this.GetPrimaryKey();
        var stats = await Grains.GetGrain<IUserStats>(userId).GetState();
        var state = await _state.Read();

        var unlocked = new List<InGameAchievementEntry>();

        foreach (var group in _achievementConfig.Value.Groups)
        {
            foreach (var tier in group.Tiers.OrderBy(t => t.Tier))
            {
                if (tier.Condition == null)
                    continue;

                if (state.IsUnlocked(group.Type, tier.Tier))
                    continue;

                if (tier.Condition.IsConditionMet(stats) == false)
                    break;

                var entry = new InGameAchievementEntry
                {
                    Type = group.Type,
                    Tier = tier.Tier,
                    Date = DateTime.UtcNow,
                    // Забрать награду нечего, поэтому такая ачивка закрывается сразу.
                    Claimed = tier.Reward is not CardUnlockReward
                };

                state = await _state.Apply(new InGameAchievementUnlocked(entry));
                unlocked.Add(entry);

                _logger.LogInformation(
                    "[User] [Achievements] User {Id} unlocked {Type} tier {Tier}",
                    userId, entry.Type, entry.Tier);
            }
        }

        if (unlocked.Count > 0)
            await this.SendProjection(state);

        return unlocked;
    }

    public async Task<IReadOnlyList<CardType>> GetRewardOptions(InGameAchievementType type, int tier)
    {
        var userId = this.GetPrimaryKey();
        var state = await _state.Read();
        var entry = state.Find(type, tier);

        if (entry == null || entry.Claimed == true)
            return Array.Empty<CardType>();

        if (entry.RewardOptions.Count > 0)
            return entry.RewardOptions.ToList();

        if (GetTierConfig(type, tier)?.Reward is not CardUnlockReward reward)
            return Array.Empty<CardType>();

        var owned = await Grains.GetGrain<IUserCards>(userId).GetAll();
        var options = AchievementRewardPicker.PickCards(
            _cardConfigs.Value, owned, reward.PossibleGroups, new Random(), RewardOptionsCount);

        if (options.Count == 0)
        {
            // Открывать больше нечего — закрываем ачивку, иначе она навсегда зависнет доступной.
            _logger.LogInformation("[User] [Achievements] User {Id} has no cards left to unlock for reward", userId);
            await ApplyClaimed(type, tier, null);
            return Array.Empty<CardType>();
        }

        state = await _state.Apply(new InGameAchievementRewardOffered(type, tier, options));

        _logger.LogInformation(
            "[User] [Achievements] User {Id} offered {Options} for {Type} tier {Tier}",
            userId, string.Join(", ", options), type, tier);

        return state.Find(type, tier)!.RewardOptions.ToList();
    }

    public async Task<bool> ClaimReward(InGameAchievementType type, int tier, CardType card)
    {
        var userId = this.GetPrimaryKey();
        var state = await _state.Read();
        var entry = state.Find(type, tier);

        if (entry == null || entry.Claimed == true)
            return false;

        if (entry.RewardOptions.Contains(card) == false)
        {
            _logger.LogWarning(
                "[User] [Achievements] User {Id} tried to claim {Card} outside of {Type} tier {Tier} options",
                userId, card, type, tier);

            return false;
        }

        await Grains.GetGrain<IUserCards>(userId).AddCard(card);
        await ApplyClaimed(type, tier, card);

        _logger.LogInformation(
            "[User] [Achievements] User {Id} claimed {Card} for {Type} tier {Tier}",
            userId, card, type, tier);

        return true;
    }

    public async Task<IReadOnlyList<InGameAchievementEntry>> GetUnlocked()
    {
        var state = await _state.Read();
        return state.Unlocked.ToList();
    }

    public async Task Reset()
    {
        var state = await _state.Apply(new InGameAchievementsReset());
        await this.SendProjection(state);
    }

    public async Task<IProjectionPayload> GetProjection()
    {
        var state = await _state.Read();
        return state;
    }

    private async Task ApplyClaimed(InGameAchievementType type, int tier, CardType? card)
    {
        var state = await _state.Apply(new InGameAchievementRewardClaimed(type, tier, card));
        await this.SendProjection(state);
    }

    private InGameAchievementTierConfig? GetTierConfig(InGameAchievementType type, int tier)
    {
        foreach (var group in _achievementConfig.Value.Groups)
        {
            if (group.Type != type)
                continue;

            foreach (var tierConfig in group.Tiers)
            {
                if (tierConfig.Tier == tier)
                    return tierConfig;
            }
        }

        return null;
    }
}
