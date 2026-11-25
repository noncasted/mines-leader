using Cluster.Configs;
using Cluster.Discovery;
using Cluster.State;
using Common;
using Common.Extensions;
using Common.Network;
using Internal;
using Game.GamePlay;
using Game.MatchMetrics;
using Game.Session;
using Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared;

namespace Game.Global;
public static class SessionDefaultsExtensions
{
    public static void PassSessionDefaults(this ServiceCollection collection, IServiceProvider serviceProvider)
    {
        // The logs of a session go to the providers of the host; the game log of the session is its own.
        collection.Pass<ILoggerFactory>(serviceProvider);
        collection.AddSingleton<IObservationEventBuffer, ObservationEventBuffer>();
        collection.AddSingleton<ISessionLogger, SessionFileLogger>();

        collection.Pass<IOrleans>(serviceProvider);
        collection.Pass<IServiceEnvironment>(serviceProvider);
        collection.Pass<IServiceDiscovery>(serviceProvider);
        collection.Pass<ICardConfigs>(serviceProvider);
        collection.Pass<IBotConfig>(serviceProvider);
        collection.Pass<IGameModeConfig>(serviceProvider);
        collection.Pass<IClusterFlags>(serviceProvider);
        collection.Pass<IGameFlags>(serviceProvider);
        collection.Pass<ICheatsAccess>(serviceProvider);
        collection.Pass<IPlayerConfig>(serviceProvider);
        collection.Pass<IMatchMetricsWriter>(serviceProvider);
    }
}
