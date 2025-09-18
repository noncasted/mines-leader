using Microsoft.Extensions.Hosting;
using Orleans.Transactions.Abstractions;

namespace Common;

public static class States
{
    public const string User_Entity = "User_Entity";
    public const string User_Progression = "User_Progression";
    public const string User_MatchHistory = "User_Match_History";
    public const string User_Projection = "User_Projection";
    public const string User_Deck = "User_Deck";

    public const string Match_Entity = "Match_Entity";

    public const string Config = "Config";
    
    public const string Messaging_Queue = "Messaging_Queue";

    public static readonly IReadOnlyList<string> StateTables =
    [
        User_Entity,
        User_Progression,
        User_MatchHistory,
        User_Projection,
        User_Deck,
        Match_Entity,
        Config, 
        Messaging_Queue
    ];

    public class UserEntityAttribute() : TransactionalStateAttribute(User_Entity, User_Entity);

    public class UserProgressionAttribute() : TransactionalStateAttribute(User_Progression, User_Progression);

    public class UserMatchHistoryAttribute() : TransactionalStateAttribute(User_MatchHistory, User_MatchHistory);

    public class UserProjectionAttribute() : TransactionalStateAttribute(User_Projection, User_Projection);

    public class UserDeckAttribute() : TransactionalStateAttribute(User_Deck, User_Deck);

    public class MatchAttribute() : TransactionalStateAttribute(Match_Entity, Match_Entity);

    public class ConfigStorageAttribute() : PersistentStateAttribute(Config, Config);
    
    public class MessageQueueAttribute() : PersistentStateAttribute(Messaging_Queue, Messaging_Queue);
}

public static class StateAttributesExtensions
{
    public static IHostApplicationBuilder AddStateAttributes(this IHostApplicationBuilder builder)
    {
        AddTransactionalAttribute<States.UserEntityAttribute>();
        AddTransactionalAttribute<States.UserProgressionAttribute>();
        AddTransactionalAttribute<States.UserMatchHistoryAttribute>();
        AddTransactionalAttribute<States.UserProjectionAttribute>();
        AddTransactionalAttribute<States.UserDeckAttribute>();

        AddTransactionalAttribute<States.MatchAttribute>();
        
        AddPersistentAttribute<States.MessageQueueAttribute>();
        AddPersistentAttribute<States.ConfigStorageAttribute>();

        return builder;

        void AddTransactionalAttribute<TAttribute>()
            where TAttribute : TransactionalStateAttribute, new()
        {
            builder.Services.Add<IAttributeToFactoryMapper<TAttribute>,
                GenericTransactionalStateAttributeMapper<TAttribute>>();
        }

        void AddPersistentAttribute<TAttribute>()
            where TAttribute : PersistentStateAttribute
        {
            builder.Services.Add<IAttributeToFactoryMapper<TAttribute>,
                GenericPersistentStateAttributeMapper<TAttribute>>();
        }
    }
}