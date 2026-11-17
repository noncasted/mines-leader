using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using FluentAssertions;
using MemoryPack;
using UnityEngine;
using Xunit;

namespace Tests.Protocol;

/// <summary>
/// Every [MemoryPackable] type of shared survives serialize -> deserialize -> serialize with the same bytes,
/// both as itself and through each union base it is tagged in. Instances are filled with non-default values, so a
/// member that is written but not read back (or the other way round) changes the bytes.
/// </summary>
public class SharedSerializationTests
{
    public static TheoryData<string> ConcreteTypes()
    {
        var data = new TheoryData<string>();

        foreach (var type in ProtocolSchema.PackableTypes().Where(IsConcrete))
            data.Add(type.FullName!);

        return data;
    }

    public static TheoryData<string, ushort> UnionMembers()
    {
        var data = new TheoryData<string, ushort>();

        foreach (var unionBase in ProtocolSchema.UnionBases())
        {
            foreach (var (tag, _) in ProtocolSchema.UnionTags(unionBase))
                data.Add(unionBase.FullName!, tag);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(ConcreteTypes))]
    public void Type_RoundTrips(string typeName)
    {
        var type = ProtocolSchema.GetType(typeName);
        var value = new ValueFiller().Create(type, 0);

        var bytes = MemoryPackSerializer.Serialize(type, value);
        var restored = MemoryPackSerializer.Deserialize(type, bytes);

        restored.Should().NotBeNull().And.BeOfType(type);
        MemoryPackSerializer.Serialize(type, restored).Should().Equal(bytes);
    }

    [Theory]
    [MemberData(nameof(UnionMembers))]
    public void UnionMember_RoundTripsThroughBase(string baseName, ushort tag)
    {
        var unionBase = ProtocolSchema.GetType(baseName);
        var type = ProtocolSchema.UnionTags(unionBase).Single(t => t.Tag == tag).Type;
        var value = new ValueFiller().Create(type, 0);

        var bytes = MemoryPackSerializer.Serialize(unionBase, value);
        var restored = MemoryPackSerializer.Deserialize(unionBase, bytes);

        restored.Should().BeOfType(type);
        MemoryPackSerializer.Serialize(unionBase, restored).Should().Equal(bytes);
        MemoryPackSerializer.Serialize(type, restored).Should().Equal(MemoryPackSerializer.Serialize(type, value));
    }

    private static bool IsConcrete(Type type)
    {
        return type.IsInterface == false && type.IsAbstract == false && type.IsGenericTypeDefinition == false;
    }

    internal class ValueFiller
    {
        private const int MaxDepth = 3;

        private int _counter;

        public object Create(Type type, int depth)
        {
            var value = type.IsValueType || type.GetConstructor(Type.EmptyTypes) != null
                ? Activator.CreateInstance(type)!
                : RuntimeHelpers.GetUninitializedObject(type);

            if (depth >= MaxDepth)
                return value;

            foreach (var member in ProtocolSchema.SerializedMembers(type))
            {
                var memberValue = Value(ProtocolSchema.MemberType(member), depth + 1);

                if (memberValue == null)
                    continue;

                switch (member)
                {
                    case FieldInfo field:
                        field.SetValue(value, memberValue);
                        break;
                    case PropertyInfo property:
                        property.SetValue(value, memberValue);
                        break;
                }
            }

            return value;
        }

        // Null keeps the default of the instance (types the filler does not know).
        private object Value(Type type, int depth)
        {
            var next = ++_counter;

            if (type == typeof(string))
                return "value" + next;

            if (type == typeof(bool))
                return true;

            if (type.IsEnum)
            {
                var values = Enum.GetValues(type);
                return values.GetValue(values.Length - 1);
            }

            if (type == typeof(Guid))
                return new Guid(next, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10);

            if (type == typeof(DateTime))
                return new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMinutes(next);

            if (type == typeof(TimeSpan))
                return TimeSpan.FromSeconds(next);

            if (type == typeof(Vector2))
                return new Vector2(next, next + 0.5f);

            if (type == typeof(Vector3))
                return new Vector3(next, next + 0.5f, next + 0.25f);

            if (type == typeof(Vector2Int))
                return new Vector2Int(next, next + 1);

            if (type.IsPrimitive)
                return Convert.ChangeType(next % 100 + 1, type);

            if (type.IsArray)
            {
                var element = type.GetElementType()!;
                var array = Array.CreateInstance(element, 2);

                for (var i = 0; i < array.Length; i++)
                    array.SetValue(Value(element, depth) ?? Default(element), i);

                return array;
            }

            if (type.IsGenericType)
            {
                var definition = type.GetGenericTypeDefinition();
                var arguments = type.GetGenericArguments();

                if (definition == typeof(Nullable<>))
                    return Value(arguments[0], depth);

                if (definition == typeof(List<>) || definition == typeof(IReadOnlyList<>) ||
                    definition == typeof(IList<>) || definition == typeof(HashSet<>))
                {
                    var concrete = definition == typeof(HashSet<>)
                        ? typeof(HashSet<>).MakeGenericType(arguments)
                        : typeof(List<>).MakeGenericType(arguments);
                    var collection = Activator.CreateInstance(concrete)!;
                    var item = Value(arguments[0], depth);

                    if (item != null)
                        concrete.GetMethod("Add")!.Invoke(collection, new[] { item });

                    return collection;
                }

                if (definition == typeof(Dictionary<,>) || definition == typeof(IReadOnlyDictionary<,>))
                {
                    var concrete = typeof(Dictionary<,>).MakeGenericType(arguments);
                    var dictionary = (IDictionary)Activator.CreateInstance(concrete)!;
                    var key = Value(arguments[0], depth);
                    var item = Value(arguments[1], depth);

                    if (key != null && item != null)
                        dictionary.Add(key, item);

                    return dictionary;
                }

                return null;
            }

            if (depth >= MaxDepth)
                return null;

            if (ProtocolSchema.UnionBases().Contains(type))
            {
                var tags = ProtocolSchema.UnionTags(type);
                return tags.Count == 0 ? null : Create(tags[next % tags.Count].Type, depth);
            }

            if (type.GetCustomAttribute<MemoryPackableAttribute>() == null || type.IsInterface || type.IsAbstract)
                return null;

            return Create(type, depth);
        }

        private static object Default(Type type)
        {
            return type.IsValueType ? Activator.CreateInstance(type)! : null!;
        }
    }
}
