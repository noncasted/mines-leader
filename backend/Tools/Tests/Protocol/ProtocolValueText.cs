using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text;
using MemoryPack;

namespace Tests.Protocol;

/// <summary>
/// A decoded protocol value as text: runtime type and every member MemoryPack writes (public fields, public
/// properties with a setter, [MemoryPackInclude]), recursively. Padding bytes of unmanaged structs are not members,
/// so two encodings that differ only in padding give the same text. The client test Internal.Tests.ProtocolBytesTests
/// has the same function.
/// </summary>
public static class ProtocolValueText
{
    private const int MaxDepth = 32;

    public static string Of(object value)
    {
        var builder = new StringBuilder();
        Append(builder, value, 0);
        return builder.ToString();
    }

    private static void Append(StringBuilder builder, object value, int depth)
    {
        if (depth > MaxDepth)
            throw new InvalidOperationException($"Value is deeper than {MaxDepth}: {value?.GetType()}");

        if (value == null)
        {
            builder.Append("null");
            return;
        }

        var type = value.GetType();

        if (value is string text)
        {
            builder.Append('"').Append(text).Append('"');
            return;
        }

        if (type.IsEnum)
        {
            builder.Append(type.FullName).Append('.').Append(Convert.ToInt64(value, CultureInfo.InvariantCulture));
            return;
        }

        if (value is float single)
        {
            builder.Append(single.ToString("R", CultureInfo.InvariantCulture));
            return;
        }

        if (value is double number)
        {
            builder.Append(number.ToString("R", CultureInfo.InvariantCulture));
            return;
        }

        // Full precision: the default text of DateTime / TimeSpan drops ticks.
        switch (value)
        {
            case DateTime date:
                builder.Append(date.ToBinary());
                return;
            case DateTimeOffset offset:
                builder.Append(offset.UtcTicks).Append('+').Append(offset.Offset.Ticks);
                return;
            case TimeSpan span:
                builder.Append(span.Ticks);
                return;
        }

        if (type.IsPrimitive || value is decimal || value is Guid)
        {
            builder.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
            return;
        }

        if (value is IDictionary dictionary)
        {
            var entries = new List<string>();

            foreach (DictionaryEntry entry in dictionary)
                entries.Add(Of(entry.Key, depth + 1) + ":" + Of(entry.Value, depth + 1));

            entries.Sort(StringComparer.Ordinal);
            builder.Append('{').Append(string.Join(",", entries)).Append('}');
            return;
        }

        if (value is IEnumerable sequence)
        {
            builder.Append('[');

            foreach (var item in sequence)
            {
                Append(builder, item, depth + 1);
                builder.Append(',');
            }

            builder.Append(']');
            return;
        }

        builder.Append(type.FullName).Append('(');

        foreach (var member in Members(type))
        {
            builder.Append(member.Name).Append('=');
            Append(builder, member is FieldInfo field ? field.GetValue(value) : ((PropertyInfo)member).GetValue(value),
                depth + 1);
            builder.Append(';');
        }

        builder.Append(')');
    }

    private static string Of(object value, int depth)
    {
        var builder = new StringBuilder();
        Append(builder, value, depth);
        return builder.ToString();
    }

    private static IEnumerable<MemberInfo> Members(Type type)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // A plain struct (Position, Vector2) is copied as raw memory: every field is on the wire, padding is not.
        if (type.IsValueType && type.GetCustomAttribute<MemoryPackableAttribute>() == null)
            return type.GetFields(flags).OrderBy(f => f.Name, StringComparer.Ordinal);

        return type.GetMembers(flags)
                   .Where(IsWritten)
                   .OrderBy(m => m.Name, StringComparer.Ordinal);
    }

    private static bool IsWritten(MemberInfo member)
    {
        if (member.GetCustomAttribute<MemoryPackIgnoreAttribute>() != null || member.Name.Contains('<'))
            return false;

        var included = member.GetCustomAttribute<MemoryPackIncludeAttribute>() != null;

        return member switch
        {
            FieldInfo field => included || (field.IsPublic && field.IsLiteral == false),
            PropertyInfo property => property.GetIndexParameters().Length == 0 &&
                                     (included || (property.GetMethod?.IsPublic == true && property.SetMethod != null)),
            _ => false
        };
    }
}
