using Common.Extensions;
using Infrastructure.State;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Infrastructure;

public static class OrleansUtilsExtensions
{
    public static IHostApplicationBuilder AddOrleansUtils(this IHostApplicationBuilder builder)
    {
        builder.Add<StateFactory>()
               .As<IStateFactory>();

        builder.Add<StateAttributeMapper>()
               .As<IAttributeToFactoryMapper<StateAttribute>>();

        builder.Add<EventStateFactory>()
               .As<IEventStateFactory>();

        builder.Add<EventStateAttributeMapper>()
               .As<IAttributeToFactoryMapper<EventStateAttribute>>();

        builder.Add<StateMigrations>()
               .As<IStateMigrations>();

        builder.Add<StateStorage>()
               .As<IStateStorage>();

        builder.Add<DirectStorage>();

        builder.Add<EventStorage>()
               .As<IEventStorage>();

        builder.Add<StateSerializer>()
               .As<IStateSerializer>();

        // Lazy: the config is an addressable state, it needs IOrleans, and IOrleans needs ITransactions - resolved in
        // place the three made a cycle that hung the start of every service (an endless resolution, no exception).
        builder.Services.AddSingleton(provider =>
            new Lazy<ITransactionConfig>(provider.GetRequiredService<ITransactionConfig>));

        builder.Add<Transactions>()
               .As<ITransactions>();

        builder.Add<OrleansUtils>()
               .As<IOrleans>();

        // Side effects are written by the transactions, so their source comes with them.
        builder.AddBackendMeter()
               .AddTraceSource(TransactionTelemetry.Source)
               .AddTraceSource(SideEffectTelemetry.Source);

        return builder;
    }

    extension(IOrleans orleans)
    {
        public IGrainFactory Grains => orleans.Client;

        public T GetGrain<T>(string key) where T : IGrainWithStringKey
        {
            return orleans.Grains.GetGrain<T>(key);
        }

        public T GetGrain<T>(long key) where T : IGrainWithIntegerKey
        {
            return orleans.Grains.GetGrain<T>(key);
        }

        public T GetGrain<T>(Guid key) where T : IGrainWithGuidKey
        {
            return orleans.Grains.GetGrain<T>(key);
        }

        public T GetGrain<T>() where T : IGrainWithGuidKey
        {
            return orleans.Grains.GetGrain<T>(Guid.Empty);
        }

        public IReadOnlyList<T> GetGrains<T>(IReadOnlyList<Guid> ids)
            where T : IGrainWithGuidKey
        {
            var grains = new T[ids.Count];

            for (var i = 0; i < ids.Count; i++)
                grains[i] = orleans.Grains.GetGrain<T>(ids[i]);

            return grains;
        }

        public Task InTransaction(Func<Task> action)
        {
            return orleans.Transactions.RunOrThrow(action);
        }

        public Task<T> InTransaction<T>(Func<Task<T>> action)
        {
            return orleans.Transactions.Run(action);
        }
    }
}
