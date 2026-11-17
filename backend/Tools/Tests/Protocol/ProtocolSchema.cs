using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using MemoryPack;
using Shared;

namespace Tests.Protocol;

/// <summary>
/// The wire schema of the shared assemblies (the framework protocol and the game shared), built by reflection:
/// every union tag, the members of every [MemoryPackable] type in serialization order and the values of the enums
/// those members use. Client and backend compile the same shared sources, so one table describes both sides.
/// A union is declared either on its base ([MemoryPackable] interface with [MemoryPackUnion]) or, when its types
/// span both assemblies, on a [MemoryPackUnionFormatter] class (INetworkContext: NetworkContextUnion).
/// </summary>
public static class ProtocolSchema
{
    public static readonly IReadOnlyList<Assembly> SharedAssemblies =
    [
        typeof(INetworkContext).Assembly,
        typeof(NetworkContextUnion).Assembly
    ];

    public static Type GetType(string fullName)
    {
        return SharedAssemblies.Select(a => a.GetType(fullName)).SingleOrDefault(t => t != null)
               ?? throw new InvalidOperationException($"Type {fullName} is not in the shared assemblies");
    }

    public static IReadOnlyList<Type> PackableTypes()
    {
        return SharedAssemblies.SelectMany(a => a.GetTypes())
                               .Where(t => t.GetCustomAttribute<MemoryPackableAttribute>() != null)
                               .OrderBy(t => t.FullName, StringComparer.Ordinal)
                               .ToList();
    }

    public static IReadOnlyList<Type> UnionBases()
    {
        var declaredOnBase = PackableTypes().Where(t => t.IsInterface || t.IsAbstract)
                                            .Where(t => t.GetCustomAttributes<MemoryPackUnionAttribute>().Any());

        return declaredOnBase.Concat(UnionFormatters().Select(f => f.Base))
                             .Distinct()
                             .OrderBy(t => t.FullName, StringComparer.Ordinal)
                             .ToList();
    }

    public static IReadOnlyList<(ushort Tag, Type Type)> UnionTags(Type unionBase)
    {
        var declaring = UnionFormatters().SingleOrDefault(f => f.Base == unionBase).Formatter ?? unionBase;

        return declaring.GetCustomAttributes<MemoryPackUnionAttribute>()
                        .Select(a => (a.Tag, a.Type))
                        .OrderBy(e => e.Tag)
                        .ToList();
    }

    private static IEnumerable<(Type Formatter, Type Base)> UnionFormatters()
    {
        return SharedAssemblies.SelectMany(a => a.GetTypes())
                               .Select(t => (Formatter: t, Attribute: t.GetCustomAttribute<MemoryPackUnionFormatterAttribute>()))
                               .Where(e => e.Attribute != null)
                               .Select(e => (e.Formatter, e.Attribute!.Type));
    }

    // Members MemoryPack writes, in its order: [MemoryPackOrder] when present, declaration order otherwise
    // (base class members first). Public fields and properties with a setter, minus [MemoryPackIgnore], plus
    // [MemoryPackInclude].
    public static IReadOnlyList<MemberInfo> SerializedMembers(Type type)
    {
        var chain = new List<Type>();

        for (var current = type; current != null && current != typeof(object); current = current.BaseType)
            chain.Insert(0, current);

        var members = new List<MemberInfo>();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                                   BindingFlags.DeclaredOnly;

        foreach (var declaring in chain)
        {
            var declared = declaring.GetMembers(flags)
                                    .Where(IsSerialized)
                                    .OrderBy(m => m.MetadataToken);

            members.AddRange(declared);
        }

        if (members.Any(m => m.GetCustomAttribute<MemoryPackOrderAttribute>() != null))
            return members.OrderBy(m => m.GetCustomAttribute<MemoryPackOrderAttribute>()!.Order).ToList();

        return members;
    }

    public static Type MemberType(MemberInfo member)
    {
        return member switch
        {
            FieldInfo field => field.FieldType,
            PropertyInfo property => property.PropertyType,
            _ => throw new InvalidOperationException($"Unexpected member {member}")
        };
    }

    public static IReadOnlyList<string> BuildLines()
    {
        var lines = new List<string>();
        var enums = new HashSet<Type>();

        foreach (var unionBase in UnionBases())
        {
            foreach (var (tag, type) in UnionTags(unionBase))
                lines.Add($"union {Name(unionBase)} {tag} {Name(type)}");
        }

        foreach (var type in PackableTypes().Where(t => t.IsInterface == false))
        {
            var members = SerializedMembers(type)
                .Select(m => {
                    var memberType = MemberType(m);
                    CollectEnums(memberType, enums);
                    return $"{m.Name}:{Name(memberType)}";
                });

            lines.Add($"type {Name(type)} {string.Join(",", members)}".TrimEnd());
        }

        foreach (var type in enums)
        {
            var values = Enum.GetValues(type)
                             .Cast<object>()
                             .Select(v => $"{Enum.GetName(type, v)}={Convert.ToInt64(v)}");

            lines.Add($"enum {Name(type)} {Name(Enum.GetUnderlyingType(type))} {string.Join(",", values)}");
        }

        lines.Sort(StringComparer.Ordinal);
        return lines;
    }

    public static string BuildText()
    {
        return string.Join("\n", BuildLines()) + "\n";
    }

    // First 16 hex chars of SHA-256 over the UTF-8 schema text.
    public static string ComputeHash(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
    }

    public static string Name(Type type)
    {
        if (type.IsArray)
            return Name(type.GetElementType()!) + "[]";

        if (type.IsGenericType)
        {
            var definition = type.GetGenericTypeDefinition().FullName!;
            var baseName = definition[..definition.IndexOf('`')];
            return $"{baseName}<{string.Join(",", type.GetGenericArguments().Select(Name))}>";
        }

        return type.FullName ?? type.Name;
    }

    private static bool IsSerialized(MemberInfo member)
    {
        if (member.GetCustomAttribute<MemoryPackIgnoreAttribute>() != null)
            return false;

        if (member.Name.Contains('<'))
            return false;

        var included = member.GetCustomAttribute<MemoryPackIncludeAttribute>() != null;

        return member switch
        {
            FieldInfo field => included || (field.IsPublic && field.IsInitOnly == false && field.IsLiteral == false),
            PropertyInfo property => property.GetIndexParameters().Length == 0 &&
                                     (included ||
                                      (property.GetMethod?.IsPublic == true && property.SetMethod != null)),
            _ => false
        };
    }

    private static void CollectEnums(Type type, HashSet<Type> enums)
    {
        if (type.IsEnum)
        {
            if (SharedAssemblies.Contains(type.Assembly))
                enums.Add(type);

            return;
        }

        if (type.IsArray)
            CollectEnums(type.GetElementType()!, enums);

        if (type.IsGenericType)
        {
            foreach (var argument in type.GetGenericArguments())
                CollectEnums(argument, enums);
        }
    }
}
