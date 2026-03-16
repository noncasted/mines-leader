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
    public static readonly Info StateTestTest = new("state_test_default_state", GrainKeyType.String);
    public static readonly Info TransactionTest = new("state_test_transactional_state", GrainKeyType.Guid);
    public static readonly Info User = new("state_user_entity", GrainKeyType.Guid);
    public static readonly Info UserAuth = new("state_user_auth", GrainKeyType.Guid);
    public static readonly Info UserProgression = new("state_user_progression", GrainKeyType.Guid);
    public static readonly Info UserProjection = new("state_user_projection", GrainKeyType.Guid);
    public static readonly Info UserMatchHistory = new("state_user_match_history", GrainKeyType.Guid);
    public static readonly Info UserDeck = new("state_user_projection", GrainKeyType.Guid);
    public static readonly Info Match = new("state_match_entity", GrainKeyType.Guid);
    public static readonly Info Bot = new("bot_entity", GrainKeyType.Guid);
    
    public static readonly Info BotConfig = new("configs", GrainKeyType.String);
    public static readonly Info CardConfig = new("configs", GrainKeyType.String);
    public static readonly Info GameModeConfig = new("configs", GrainKeyType.String);

    public static IReadOnlyList<Info> All =>
    [
        StateTestTest,
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
        public Info(string tableName, GrainKeyType keyType)
        {
            TableName = tableName;
            KeyType = keyType;
        }

        public string TableName { get; }
        public GrainKeyType KeyType { get; }
    }
}