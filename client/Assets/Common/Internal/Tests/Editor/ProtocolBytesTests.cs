using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using MemoryPack;
using NUnit.Framework;
using Shared;
using UnityEngine;

namespace Internal.Tests
{
    // The client reads the bytes the backend writes and writes them back. The golden file is produced by the
    // backend test Tests.Protocol.ProtocolBytesTests (every [MemoryPackable] type of shared as itself, every union
    // member through its base with its explicit tag) and read here from the backend folder - one file, no copy. Every
    // line must deserialize to its type and serialize back to the same bytes (padding aside, see AssertWrittenBack):
    // the Unity types and the Unity MemoryPack build agree with the backend on the wire.
    public class ProtocolBytesTests
    {
        private const int MaxDepth = 32;

        private static readonly string GoldenPath = Path.GetFullPath(
            Path.Combine(Application.dataPath, "../../backend/Tools/Tests/Protocol/protocol_bytes.golden.txt"));

        [Test]
        public void GoldenBytes_ReadAndWrittenBack_Unchanged()
        {
            var lines = File.ReadAllLines(GoldenPath).Where(line => line.Length > 0).ToList();
            var failures = new List<string>();

            Assert.Greater(lines.Count, 100, "the golden file looks truncated");

            foreach (var line in lines)
            {
                try
                {
                    Check(line);
                }
                catch (Exception exception)
                {
                    failures.Add($"{line.Substring(0, Math.Min(line.Length, 120))}: {exception.Message}");
                }
            }

            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        private static void Check(string line)
        {
            var parts = line.Split(' ');

            switch (parts[0])
            {
                case "type":
                {
                    var type = FindType(parts[1]);
                    var bytes = FromHex(parts[2]);
                    var restored = MemoryPackSerializer.Deserialize(type, bytes);

                    Assert.IsNotNull(restored);
                    Assert.AreEqual(type, restored.GetType());
                    AssertWrittenBack(type, bytes, restored);
                    break;
                }
                case "union":
                {
                    var unionBase = FindType(parts[1]);
                    var type = FindType(parts[3]);
                    var bytes = FromHex(parts[4]);
                    var restored = MemoryPackSerializer.Deserialize(unionBase, bytes);

                    Assert.IsNotNull(restored);
                    Assert.AreEqual(type, restored.GetType(), $"tag {parts[2]}");
                    AssertWrittenBack(unionBase, bytes, restored);
                    break;
                }
                default:
                    throw new InvalidOperationException($"Unknown line kind {parts[0]}");
            }
        }

        // MemoryPack copies unmanaged structs as raw memory, padding included (KeyValuePair<int, long> of a
        // Dictionary<enum, long>: 4 bytes after the key; CardType?: 3 bytes after HasValue). In Unity those bytes are
        // stack garbage, on the backend zeros, so they are not compared: the bytes written back must be equal, or have
        // the same length and read back to the same value. Any other difference (tag, member order or type, length)
        // still fails.
        private static void AssertWrittenBack(Type type, byte[] golden, object restored)
        {
            var written = MemoryPackSerializer.Serialize(type, restored);

            if (written.SequenceEqual(golden))
                return;

            Assert.AreEqual(golden.Length, written.Length, "written back with another length");

            var reread = MemoryPackSerializer.Deserialize(type, written);
            Assert.AreEqual(ValueText(restored), ValueText(reread), "written back to another value");
        }

        // A decoded value as text: runtime type and every member MemoryPack writes, recursively; padding is not a
        // member. Same as the backend Tests.Protocol.ProtocolValueText.
        private static string ValueText(object value)
        {
            var builder = new StringBuilder();
            AppendValue(builder, value, 0);
            return builder.ToString();
        }

        private static void AppendValue(StringBuilder builder, object value, int depth)
        {
            if (depth > MaxDepth)
                throw new InvalidOperationException($"Value is deeper than {MaxDepth}: {value?.GetType()}");

            if (value == null)
            {
                builder.Append("null");
                return;
            }

            var type = value.GetType();

            switch (value)
            {
                case string text:
                    builder.Append('"').Append(text).Append('"');
                    return;
                case Enum:
                    builder.Append(type.FullName).Append('.')
                           .Append(Convert.ToInt64(value, CultureInfo.InvariantCulture));
                    return;
                case float single:
                    builder.Append(single.ToString("R", CultureInfo.InvariantCulture));
                    return;
                case double number:
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
                {
                    var pair = new StringBuilder();
                    AppendValue(pair, entry.Key, depth + 1);
                    pair.Append(':');
                    AppendValue(pair, entry.Value, depth + 1);
                    entries.Add(pair.ToString());
                }

                entries.Sort(StringComparer.Ordinal);
                builder.Append('{').Append(string.Join(",", entries)).Append('}');
                return;
            }

            if (value is IEnumerable sequence)
            {
                builder.Append('[');

                foreach (var item in sequence)
                {
                    AppendValue(builder, item, depth + 1);
                    builder.Append(',');
                }

                builder.Append(']');
                return;
            }

            builder.Append(type.FullName).Append('(');

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            // A plain struct (Position, Vector2) is copied as raw memory: every field is on the wire, padding is not.
            var members = type.IsValueType && type.GetCustomAttribute<MemoryPackableAttribute>() == null
                ? type.GetFields(flags).OrderBy(m => m.Name, StringComparer.Ordinal).Cast<MemberInfo>()
                : type.GetMembers(flags).Where(IsWritten).OrderBy(m => m.Name, StringComparer.Ordinal);

            foreach (var member in members)
            {
                var memberValue = member is FieldInfo field
                    ? field.GetValue(value)
                    : ((PropertyInfo)member).GetValue(value);

                builder.Append(member.Name).Append('=');
                AppendValue(builder, memberValue, depth + 1);
                builder.Append(';');
            }

            builder.Append(')');
        }

        private static bool IsWritten(MemberInfo member)
        {
            if (member.GetCustomAttribute<MemoryPackIgnoreAttribute>() != null || member.Name.Contains("<"))
                return false;

            var included = member.GetCustomAttribute<MemoryPackIncludeAttribute>() != null;

            switch (member)
            {
                case FieldInfo field:
                    return included || (field.IsPublic && field.IsLiteral == false);
                case PropertyInfo property:
                    return property.GetIndexParameters().Length == 0 &&
                           (included || (property.GetMethod != null && property.GetMethod.IsPublic &&
                                         property.SetMethod != null));
                default:
                    return false;
            }
        }

        // The protocol types live in the framework assembly (INetworkContext) and in the game shared assembly.
        private static Type FindType(string fullName)
        {
            var type = typeof(INetworkContext).Assembly.GetType(fullName) ??
                       typeof(NetworkContextUnion).Assembly.GetType(fullName);

            if (type == null)
                throw new InvalidOperationException($"Type {fullName} is not in the shared assemblies");

            return type;
        }

        private static byte[] FromHex(string hex)
        {
            if (hex.Length % 2 != 0)
                throw new FormatException($"Odd hex length {hex.Length}");

            var bytes = new byte[hex.Length / 2];

            for (var i = 0; i < bytes.Length; i++)
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);

            return bytes;
        }
    }
}
