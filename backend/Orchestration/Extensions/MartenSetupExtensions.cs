using System.Reflection;
using Common.Extensions;
using Infrastructure.State;
using JasperFx.Events;
using JasperFx.Events.Projections;
using Marten;
using Marten.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Orchestration;

public static class MartenSetupExtensions
{
    public static IHostApplicationBuilder AddMartenStore(this IHostApplicationBuilder builder)
    {
        builder.Services.AddSingleton<IDocumentStore>(sp => {
            var dbSource = sp.GetRequiredService<IDbSource>();
            var registry = sp.GetRequiredService<IGrainStatesRegistry>();

            return DocumentStore.For(options => {
                options.Connection(dbSource.Value);
                Configure(options, registry);
            });
        });

        return builder;
    }

    // Event store settings of the hosts and of the test cluster: stream identity, serializer and an inline
    // snapshot projection for every event state of the composition's generated registry.
    public static void Configure(StoreOptions options, IGrainStatesRegistry registry)
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

        // Snapshot<T>(SnapshotLifecycle, ...) is generic over the state type: called through reflection for
        // the types known only at runtime.
        var snapshotMethod = options.Projections.GetType()
                                    .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                                    .SingleOrDefault(m => m.Name == "Snapshot"
                                                          && m.IsGenericMethod
                                                          && m.GetParameters().Length == 2
                                                          && m.GetParameters()[0].ParameterType == typeof(SnapshotLifecycle))
                            ?? throw new InvalidOperationException(
                                "Marten: Projections.Snapshot<T>(SnapshotLifecycle, Action) not found");

        foreach (var type in EventStateTypes(registry))
            snapshotMethod.MakeGenericMethod(type).Invoke(options.Projections, [SnapshotLifecycle.Inline, null]);
    }

    // The event states of the registry (generated per composition): independent of the assemblies that happen
    // to be loaded into the process.
    public static IReadOnlyList<Type> EventStateTypes(IGrainStatesRegistry registry)
    {
        return registry.All
                       .Select(info => info.Type)
                       .Where(type => typeof(IEventStateValue).IsAssignableFrom(type))
                       .OrderBy(type => type.FullName, StringComparer.Ordinal)
                       .ToList();
    }
}
