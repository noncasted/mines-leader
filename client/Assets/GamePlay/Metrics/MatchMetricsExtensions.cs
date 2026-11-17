using System;
using Internal;

namespace GamePlay.Metrics
{
    public static class MatchMetricsExtensions
    {
        // The scope of a match: its clock, its stream of the session sessionId and the writers of the game metrics of the
        // local player. IMatchMetrics comes from the meta scope. The session is set up before the writers: they declare
        // their series in their setup.
        public static IScopeBuilder AddMatchMetrics(this IScopeBuilder builder, Guid sessionId)
        {
            builder.Register<MatchClock>()
                   .AsSelf();

            builder.Register<MatchSnapshots>()
                   .As<IMatchSnapshots>()
                   .As<IScopeSetup>();

            builder.Register<MatchMetricsSession>()
                   .WithParameter(sessionId)
                   .As<IMatchMetricsSession>()
                   .As<IScopeSetup>();

            builder.Register<CellOpenMetrics>()
                   .As<IScopeSetup>();

            builder.Register<FlagMetrics>()
                   .As<IScopeSetup>();

            builder.Register<MineHitMetrics>()
                   .As<IScopeSetup>();

            builder.Register<CardPlayMetrics>()
                   .As<IScopeSetup>();

            builder.Register<TurnMetrics>()
                   .As<IScopeSetup>();

            builder.Register<MatchEndMetrics>()
                   .As<IScopeSetup>();

            return builder;
        }
    }
}
