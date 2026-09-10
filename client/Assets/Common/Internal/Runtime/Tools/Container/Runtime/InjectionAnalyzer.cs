using System;
using System.Collections.Generic;
using System.Reflection;

namespace Internal
{
    internal readonly struct InjectorSignature
    {
        public InjectorSignature(
            ConstructorInfo constructor,
            ParameterInfo[] constructorParameters,
            MethodInfo construct,
            ParameterInfo[] constructParameters)
        {
            Constructor = constructor;
            ConstructorParameters = constructorParameters ?? Array.Empty<ParameterInfo>();
            Construct = construct;
            ConstructParameters = constructParameters ?? Array.Empty<ParameterInfo>();
        }

        public readonly ConstructorInfo Constructor;
        public readonly ParameterInfo[] ConstructorParameters;
        public readonly MethodInfo Construct;
        public readonly ParameterInfo[] ConstructParameters;
    }

    internal static class InjectionAnalyzer
    {
        private static readonly Dictionary<Type, InjectorSignature> _cache = new();

        public static InjectorSignature Analyze(Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            if (_cache.TryGetValue(type, out var cached) == true)
                return cached;

            var signature = AnalyzeCore(type);
            _cache[type] = signature;
            return signature;
        }

        public static int[] CombineSlots(int[] constructorSlots, int[] constructSlots)
        {
            constructorSlots ??= Array.Empty<int>();
            constructSlots ??= Array.Empty<int>();

            if (constructorSlots.Length == 0)
                return constructSlots;
            if (constructSlots.Length == 0)
                return constructorSlots;

            var combined = new int[constructorSlots.Length + constructSlots.Length];
            Array.Copy(constructorSlots, 0, combined, 0, constructorSlots.Length);
            Array.Copy(constructSlots, 0, combined, constructorSlots.Length, constructSlots.Length);
            return combined;
        }

        public static int[] BindParameters(
            ParameterInfo[] parameters,
            Type ownerType,
            Dictionary<Type, int> typeToSlot,
            Dictionary<Type, object> overrides,
            Dictionary<Type, int> localExtras,
            List<object> extras,
            int extraStart,
            List<int> dependencies)
        {
            if (parameters == null || parameters.Length == 0)
                return Array.Empty<int>();

            var slots = new int[parameters.Length];
            for (var i = 0; i < parameters.Length; i++)
            {
                var parameter = parameters[i];
                var parameterType = parameter.ParameterType;

                if (overrides != null &&
                    extras != null &&
                    localExtras != null &&
                    overrides.TryGetValue(parameterType, out var value) == true)
                {
                    if (localExtras.TryGetValue(parameterType, out var extraSlot) == false)
                    {
                        extraSlot = extraStart + extras.Count;
                        extras.Add(value);
                        localExtras[parameterType] = extraSlot;
                    }

                    slots[i] = extraSlot;
                    continue;
                }

                if (typeToSlot.TryGetValue(parameterType, out var slot) == false)
                {
                    throw new InvalidOperationException(
                        $"Missing dependency of type {parameterType.FullName} for parameter '{parameter.Name}' of {ownerType.FullName}.");
                }

                slots[i] = slot;
                AddUnique(dependencies, slot);
            }

            return slots;
        }

        private static InjectorSignature AnalyzeCore(Type type)
        {
            var constructor = SelectConstructor(type);
            var construct = SelectConstruct(type);

            return new InjectorSignature(
                constructor,
                constructor == null ? Array.Empty<ParameterInfo>() : constructor.GetParameters(),
                construct,
                construct == null ? Array.Empty<ParameterInfo>() : construct.GetParameters());
        }

        private static ConstructorInfo SelectConstructor(Type type)
        {
            var constructors = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public);
            if (constructors.Length == 0)
                return null;

            var maxArity = -1;
            ConstructorInfo selected = null;
            var conflict = false;

            for (var i = 0; i < constructors.Length; i++)
            {
                var constructor = constructors[i];
                var arity = constructor.GetParameters().Length;
                if (arity > maxArity)
                {
                    maxArity = arity;
                    selected = constructor;
                    conflict = false;
                }
                else if (arity == maxArity)
                {
                    conflict = true;
                }
            }

            if (conflict == true)
            {
                throw new InvalidOperationException(
                    $"Type {type.FullName} has multiple public constructors with arity {maxArity}.");
            }

            return selected;
        }

        private static MethodInfo SelectConstruct(Type type)
        {
            var methods = type.GetMethods(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            MethodInfo selected = null;

            for (var i = 0; i < methods.Length; i++)
            {
                var method = methods[i];
                if (method.Name != "Construct")
                    continue;
                if (method.IsSpecialName == true)
                    continue;
                if (method.IsGenericMethodDefinition == true)
                    continue;

                if (selected != null)
                {
                    throw new InvalidOperationException(
                        $"Type {type.FullName} has multiple Construct methods.");
                }

                selected = method;
            }

            return selected;
        }

        private static void AddUnique(List<int> values, int value)
        {
            if (values == null)
                return;

            for (var i = 0; i < values.Count; i++)
            {
                if (values[i] == value)
                    return;
            }

            values.Add(value);
        }
    }
}
