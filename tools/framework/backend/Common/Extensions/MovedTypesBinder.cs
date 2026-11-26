using System.Collections.Concurrent;
using System.Reflection;
using Newtonsoft.Json.Serialization;

namespace Common.Extensions;

// Persisted states carry assembly-qualified type names ("$type": "Cluster.Configs.CardConfigsState, Cluster").
// A type moved to another assembly with its namespace kept (a game type out of a framework assembly, a framework
// type out of a game one) is still found: when the named assembly does not have it, the type is looked up by its
// full name in the loaded assemblies. Generic arguments are resolved the same way. Names are written as before.
public class MovedTypesBinder : DefaultSerializationBinder
{
    private static readonly ConcurrentDictionary<(string? Assembly, string Type), Type> Cache = new();

    public override Type BindToType(string? assemblyName, string typeName)
    {
        return Cache.GetOrAdd((assemblyName, typeName),
            key => Resolve(key.Assembly == null ? key.Type : $"{key.Type}, {key.Assembly}"));
    }

    // An assembly-qualified name stored outside Json.NET (the event payloads of EventStorage), resolved the same way.
    public static Type Resolve(string qualifiedName)
    {
        return Type.GetType(qualifiedName, LoadedAssembly, FindType, throwOnError: false)
               ?? throw new Newtonsoft.Json.JsonSerializationException(
                   $"Could not resolve type '{qualifiedName}': it is in no loaded assembly");
    }

    private static Assembly LoadedAssembly(AssemblyName name)
    {
        var loaded = AppDomain.CurrentDomain.GetAssemblies()
                              .FirstOrDefault(a => AssemblyName.ReferenceMatchesDefinition(name, a.GetName()));

        if (loaded != null)
            return loaded;

        try
        {
            return Assembly.Load(name);
        }
        catch (FileNotFoundException)
        {
            // The assembly is gone (the type moved). Type.GetType gives up on a null assembly without asking
            // FindType, so another assembly stands in: FindType does not find the type there and searches by name.
            return typeof(MovedTypesBinder).Assembly;
        }
    }

    private static Type? FindType(Assembly? assembly, string fullName, bool ignoreCase)
    {
        var type = assembly?.GetType(fullName, false, ignoreCase);

        if (type != null)
            return type;

        var matches = AppDomain.CurrentDomain.GetAssemblies()
                               .Select(a => a.GetType(fullName, false, ignoreCase))
                               .Where(t => t != null)
                               .Distinct()
                               .ToList();

        return matches.Count switch
        {
            0 => null,
            1 => matches[0],
            _ => throw new Newtonsoft.Json.JsonSerializationException(
                $"Type '{fullName}' is declared in several assemblies: " +
                string.Join(", ", matches.Select(t => t!.Assembly.GetName().Name)))
        };
    }
}
