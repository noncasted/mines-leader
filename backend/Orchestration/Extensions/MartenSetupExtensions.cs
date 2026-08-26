using System.Reflection;
using Common.Extensions;
using Infrastructure.State;
using JasperFx.Events;
using JasperFx.Events.Projections;
using Marten;
using Marten.Events.Projections;
using Marten.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Orchestration;

public static class MartenSetupExtensions
{
    public static IHostApplicationBuilder AddMartenStore(this IHostApplicationBuilder builder)
    {
        builder.Services.AddSingleton<IDocumentStore>(sp =>
        {
            var dbSource = sp.GetRequiredService<IDbSource>();

            return DocumentStore.For(options =>
            {
                options.Connection(dbSource.Value);
                options.Events.StreamIdentity = StreamIdentity.AsString;
                options.Events.AppendMode = EventAppendMode.Quick;

                var baseSettings = JsonStateSettings.CreateBase();
                var jsonSerializer = new JsonNetSerializer();
                jsonSerializer.Configure(s =>
                {
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

                var stateTypes = FindEventStateTypes();
                var projectionsType = options.Projections.GetType();
                var snapshotMethod = projectionsType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(m => m.Name == "Snapshot"
                        && m.IsGenericMethod
                        && m.GetParameters().Length >= 1
                        && m.GetParameters()[0].ParameterType == typeof(SnapshotLifecycle));
                if (snapshotMethod != null)
                {
                    foreach (var type in stateTypes)
                    {
                        var generic = snapshotMethod.MakeGenericMethod(type);
                        generic.Invoke(options.Projections, [SnapshotLifecycle.Inline, null]);
                    }
                }
            });

            IReadOnlyList<Type> FindEventStateTypes()
            {
                var eventStateInterface = typeof(IEventStateValue);
                var result = new List<Type>();

                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location)))
                {
                    Type[] types;
                    try { types = asm.GetTypes(); }
                    catch { continue; }

                    foreach (var type in types)
                    {
                        if (type.IsInterface || type.IsAbstract || type.IsGenericTypeDefinition)
                            continue;

                        if (!eventStateInterface.IsAssignableFrom(type))
                            continue;

                        if (type.GetConstructor(Type.EmptyTypes) is not { IsPublic: true })
                            continue;

                        result.Add(type);
                        File.AppendAllText("/tmp/marten_setup.log", $"[MartenSetup] Registered inline projection for: {type.FullName}\n");
                    }
                }

                File.AppendAllText("/tmp/marten_setup.log", $"[MartenSetup] Total inline projections registered: {result.Count}\n");

                return result;
            }
        });

        return builder;
    }
}
