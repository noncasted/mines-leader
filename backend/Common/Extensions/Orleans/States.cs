using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orleans.Transactions.Abstractions;

namespace Common;

public static class States
{
    private const string User_Entity = "User_Entity";
    private const string User_Progression = "User_Progression";
    private const string User_MatchHistory = "User_MatchHistory";
    private const string User_Projection = "User_Projection_Records";
    private const string User_ProjectionConnection = "User_Projection_Connection";
    private const string User_Deck = "User_Deck";

    private const string Match_Entity = "Match_Entity";

    public class UserEntityAttribute() : TransactionalStateAttribute(User_Entity);

    public class UserProgressionAttribute() : TransactionalStateAttribute(User_Progression);

    public class UserMatchHistoryAttribute() : TransactionalStateAttribute(User_MatchHistory);

    public class UserProjectionAttribute() : TransactionalStateAttribute(User_Projection);

    public class UserProjectionConnectionAttribute() : PersistentStateAttribute(User_ProjectionConnection);

    public class UserDeckAttribute() : TransactionalStateAttribute(User_Deck);

    public class MatchAttribute() : TransactionalStateAttribute(Match_Entity);
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
        AddPersistentAttribute<States.UserProjectionConnectionAttribute>();

        AddTransactionalAttribute<States.MatchAttribute>();

        return builder;

        void AddTransactionalAttribute<TAttribute>()
            where TAttribute : TransactionalStateAttribute, new()
        {
            builder.Services
                .AddSingleton<IAttributeToFactoryMapper<TAttribute>,
                    GenericTransactionalStateAttributeMapper<TAttribute>>();
        }

        void AddPersistentAttribute<TAttribute>()
            where TAttribute : PersistentStateAttribute, new()
        {
            builder.Services
                .AddSingleton<IAttributeToFactoryMapper<TAttribute>,
                    GenericPersistentStateAttributeMapper<TAttribute>>();
        }
    }
}