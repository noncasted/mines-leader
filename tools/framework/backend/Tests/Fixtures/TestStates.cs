using System.Reflection;
using Cluster.Discovery;
using Common;
using Infrastructure.State;
using JasperFx.Events;
using JasperFx.Events.Projections;
using Marten;
using Marten.Services;
using Shared;

namespace Framework.Tests.Fixtures;

// The grain states of the test cluster: every [GrainState] / [GrainEventState] type of the framework assemblies and
// of this one, as the generator of a project (Tools/Generators) collects them into the registry of its hosts. An
// event state has no state table: it is a Marten stream with an inline snapshot document (mt_doc_*).
public static class TestStates
{
    private static readonly Assembly[] _assemblies =
    [
        typeof(IGrainStatesRegistry).Assembly,
        typeof(ServiceTag).Assembly,
        typeof(Game.ClientTelemetry.TelemetrySessionState).Assembly,
        typeof(TestStates).Assembly
    ];

    public static IReadOnlyList<GrainStateInfo> All { get; } = Collect();

    public static GrainStatesRegistry CreateRegistry()
    {
        return new GrainStatesRegistry(All.ToList());
    }

    // The event store settings of the hosts: stream identity, the serializer of the states and an inline snapshot
    // projection for every event state of the registry.
    public static void ConfigureMarten(StoreOptions options, IGrainStatesRegistry registry)
    {
        options.Events.StreamIdentity = StreamIdentity.AsString;
        options.Events.AppendMode = EventAppendMode.Quick;

        var baseSettings = JsonStateSettings.CreateBase();
        var jsonSerializer = new JsonNetSerializer();

        jsonSerializer.Configure(s => {
            s.TypeNameHandling = baseSettings.TypeNameHandling;
            s.MetadataPropertyHandling = baseSettings.MetadataPropertyHandling;
            s.PreserveReferencesHandling = baseSettings.PreserveReferencesHandling;
            s.DateFormatHandling = baseSettings.DateFormatHandling;
            s.DefaultValueHandling = baseSettings.DefaultValueHandling;
            s.MissingMemberHandling = baseSettings.MissingMemberHandling;
            s.NullValueHandling = baseSettings.NullValueHandling;
            s.ConstructorHandling = baseSettings.ConstructorHandling;
            s.TypeNameAssemblyFormatHandling = baseSettings.TypeNameAssemblyFormatHandling;
            s.Formatting = baseSettings.Formatting;

            foreach (var converter in baseSettings.Converters)
                s.Converters.Add(converter);
        });

        options.Serializer(jsonSerializer);

        // Snapshot<T>(SnapshotLifecycle, ...) is generic over the state type: called through reflection for the
        // types known only at runtime.
        var snapshotMethod = options.Projections.GetType()
                                    .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                                    .Single(m => m.Name == "Snapshot"
                                                 && m.IsGenericMethod
                                                 && m.GetParameters().Length == 2
                                                 && m.GetParameters()[0].ParameterType == typeof(SnapshotLifecycle));

        var eventStates = registry.All
                                  .Select(info => info.Type)
                                  .Where(type => typeof(IEventStateValue).IsAssignableFrom(type))
                                  .OrderBy(type => type.FullName, StringComparer.Ordinal);

        foreach (var type in eventStates)
            snapshotMethod.MakeGenericMethod(type).Invoke(options.Projections, [SnapshotLifecycle.Inline, null]);
    }

    private static List<GrainStateInfo> Collect()
    {
        var states = new List<GrainStateInfo>();

        foreach (var type in _assemblies.SelectMany(assembly => assembly.GetTypes()).OrderBy(t => t.FullName, StringComparer.Ordinal))
        {
            var state = type.GetCustomAttributes<GrainStateAttribute>().FirstOrDefault();

            if (state != null)
            {
                states.Add(new GrainStateInfo
                {
                    TableName = state.Table,
                    KeyType = state.Key,
                    Type = type,
                    Name = state.State
                });

                continue;
            }

            var eventState = type.GetCustomAttributes<GrainEventStateAttribute>().FirstOrDefault();

            if (eventState == null)
                continue;

            states.Add(new GrainStateInfo
            {
                TableName = $"mt_doc_{type.Name.ToLowerInvariant()}",
                KeyType = eventState.Key,
                Type = type,
                Name = eventState.State
            });
        }

        return states;
    }
}
