using Common.Extensions;
using Microsoft.Extensions.Hosting;
using Orleans.Transactions.Abstractions;

namespace Infrastructure;

public static class States
{
    public const string User_Entity = "user_entity";
    public const string User_Auth = "user_auth";
    public const string User_Progression = "user_progression";
    public const string User_MatchHistory = "user_match_history";
    public const string User_Projection = "user_projection";
    public const string User_Deck = "user_deck";
    public const string User_Collection = "user_collection";

    public const string Bot_Collection = "bot_collection";

    public const string Match_Entity = "match_entity";

    public const string Config_Card = "config_card";
    public const string Config = "config";
    public const string Messaging_Queue = "messaging_queue";
    public const string ClusterState = "clusterState";

    public static readonly IReadOnlyList<string> StateTables =
    [
        User_Entity,
        User_Auth,
        User_Progression,
        User_MatchHistory,
        User_Projection,
        User_Deck,
        User_Collection,
        Bot_Collection,
        Match_Entity,
        Config,
        Config_Card,
        Messaging_Queue,
        ClusterState
    ];

    public class UserEntityAttribute() : TransactionalStateAttribute(User_Entity, User_Entity);

    public class UserAuthAttribute() : TransactionalStateAttribute(User_Auth, User_Auth);

    public class UserProgressionAttribute() : TransactionalStateAttribute(User_Progression, User_Progression);

    public class UserMatchHistoryAttribute() : TransactionalStateAttribute(User_MatchHistory, User_MatchHistory);

    public class UserProjectionAttribute() : TransactionalStateAttribute(User_Projection, User_Projection);

    public class UserDeckAttribute() : TransactionalStateAttribute(User_Deck, User_Deck);

    public class UserCollectionAttribute() : PersistentStateAttribute(User_Collection, User_Collection);

    public class BotCollectionAttribute() : PersistentStateAttribute(Bot_Collection, Bot_Collection);

    public class MatchAttribute() : TransactionalStateAttribute(Match_Entity, Match_Entity);

    public class ConfigStorageAttribute() : PersistentStateAttribute(Config, Config);
    public class CardConfigAttribute() : PersistentStateAttribute(Config_Card, Config_Card);

    public class MessageQueueAttribute() : PersistentStateAttribute(Messaging_Queue, Messaging_Queue);

    public class ClusterStateAttribute() : PersistentStateAttribute(ClusterState, ClusterState);
}

public static class StateAttributesExtensions
{
    public static IHostApplicationBuilder AddStateAttributes(this IHostApplicationBuilder builder)
    {
        AddTransactionalAttribute<States.UserEntityAttribute>();
        AddTransactionalAttribute<States.UserAuthAttribute>();
        AddTransactionalAttribute<States.UserProgressionAttribute>();
        AddTransactionalAttribute<States.UserMatchHistoryAttribute>();
        AddTransactionalAttribute<States.UserProjectionAttribute>();
        AddTransactionalAttribute<States.UserDeckAttribute>();

        AddTransactionalAttribute<States.MatchAttribute>();

        AddPersistentAttribute<States.MessageQueueAttribute>();
        AddPersistentAttribute<States.ConfigStorageAttribute>();
        AddPersistentAttribute<States.ClusterStateAttribute>();
        AddPersistentAttribute<States.UserCollectionAttribute>();
        AddPersistentAttribute<States.BotCollectionAttribute>();
        AddPersistentAttribute<States.CardConfigAttribute>();

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