namespace Common;

public enum GrainKeyType
{
    Integer = 100,
    String = 200,
    Guid = 300,
    IntegerAndString = 400,
    GuidAndString = 500
}

public static class StatesLookup
{
    public static readonly Info StateTestTest = new()
    {
        TableName = "state_test_default_state",
        StateName = "state_test",
        KeyType = GrainKeyType.String
    };

    public static readonly Info StateMigrationTest = new()
    {
        TableName = "state_test_default_state",
        StateName = "migration_test_state",
        KeyType = GrainKeyType.String
    };

    public static readonly Info TransactionTest = new()
    {
        TableName = "state_test_transactional_state",
        StateName = "transaction_test",
        KeyType = GrainKeyType.Guid
    };

    public static readonly Info User = new()
    {
        TableName = "state_user_entity",
        StateName = "user_entity",
        KeyType = GrainKeyType.Guid
    };

    public static readonly Info UserAuth = new()
    {
        TableName = "state_user_auth",
        StateName = "user_auth",
        KeyType = GrainKeyType.Guid
    };

    public static readonly Info UserProgression = new()
    {
        TableName = "state_user_progression",
        StateName = "user_progression",
        KeyType = GrainKeyType.Guid
    };

    public static readonly Info UserProjection = new()
    {
        TableName = "state_user_projection",
        StateName = "user_projection",
        KeyType = GrainKeyType.Guid
    };

    public static readonly Info UserMatchHistory = new()
    {
        TableName = "state_user_match_history",
        StateName = "user_match_history",
        KeyType = GrainKeyType.Guid
    };

    public static readonly Info UserDeck = new()
    {
        TableName = "state_user_projection",
        StateName = "user_deck",
        KeyType = GrainKeyType.Guid
    };

    public static readonly Info Match = new()
    {
        TableName = "state_match_entity",
        StateName = "match_entity",
        KeyType = GrainKeyType.Guid
    };

    public static readonly Info Bot = new()
    {
        TableName = "bot_entity",
        StateName = "bot_entity",
        KeyType = GrainKeyType.Guid
    };

    public static readonly Info BotConfig = new()
    {
        TableName = "configs",
        StateName = "bot_config",
        KeyType = GrainKeyType.String
    };

    public static readonly Info CardConfig = new()
    {
        TableName = "configs",
        StateName = "card_config",
        KeyType = GrainKeyType.String
    };

    public static readonly Info GameModeConfig = new()
    {
        TableName = "configs",
        StateName = "game_mode_config",
        KeyType = GrainKeyType.String
    };

    public static IReadOnlyList<Info> All =>
    [
        StateTestTest,
        StateMigrationTest,
        TransactionTest,
        User,
        UserAuth,
        UserProgression,
        UserProjection,
        UserMatchHistory,
        UserDeck,
        Match,
        Bot,
        BotConfig,
        CardConfig,
        GameModeConfig,
    ];


    public class Info
    {
        public required string TableName { get; init; }
        public required string StateName { get; init; }
        public required GrainKeyType KeyType { get; init; }
    }
}
