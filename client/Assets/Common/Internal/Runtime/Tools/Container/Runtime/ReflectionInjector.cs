using System;
using System.Reflection;

namespace Internal
{
    internal sealed class ReflectionInjector : IInjector
    {
        private ReflectionInjector(
            Type type,
            ConstructorInfo constructor,
            int[] constructorSlots,
            MethodInfo construct,
            int[] constructSlots)
        {
            _type = type;
            _constructor = constructor;
            _constructorSlots = constructorSlots;
            _construct = construct;
            _constructSlots = constructSlots;
        }

        private readonly Type _type;
        private readonly ConstructorInfo _constructor;
        private readonly int[] _constructorSlots;
        private readonly MethodInfo _construct;
        private readonly int[] _constructSlots;

        internal static ReflectionInjector Create(Type type, int[] slots)
        {
            var signature = InjectionAnalyzer.Analyze(type);
            var constructorSlots = Slice(slots, 0, signature.ConstructorParameters.Length);
            var constructSlots = Slice(
                slots,
                signature.ConstructorParameters.Length,
                signature.ConstructParameters.Length);

            return new ReflectionInjector(
                type,
                signature.Constructor,
                constructorSlots,
                signature.Construct,
                constructSlots);
        }

        public object Create(IResolvePlan plan)
        {
            if (_constructor == null)
            {
                throw new InvalidOperationException(
                    $"Type {_type.FullName} has no public constructor.");
            }

            object instance;
            try
            {
                instance = _constructor.Invoke(ResolveArguments(_constructorSlots, plan));
            }
            catch (TargetInvocationException exception)
            {
                throw Wrap(exception, _type);
            }

            try
            {
                Construct(instance, plan);
            }
            catch
            {
                if (instance is IDisposable disposable)
                {
                    try
                    {
                        disposable.Dispose();
                    }
                    catch (Exception disposeException)
                    {
                        UnityEngine.Debug.LogException(disposeException);
                    }
                }

                throw;
            }

            return instance;
        }

        public void Construct(object instance, IResolvePlan plan)
        {
            if (_construct == null)
                return;

            try
            {
                _construct.Invoke(instance, ResolveArguments(_constructSlots, plan));
            }
            catch (TargetInvocationException exception)
            {
                throw Wrap(exception, _type);
            }
        }

        private static object[] ResolveArguments(int[] slots, IResolvePlan plan)
        {
            if (slots.Length == 0)
                return Array.Empty<object>();

            var arguments = new object[slots.Length];
            for (var i = 0; i < slots.Length; i++)
                arguments[i] = plan.Get(slots[i]);

            return arguments;
        }

        private static int[] Slice(int[] slots, int offset, int length)
        {
            if (length <= 0)
                return Array.Empty<int>();

            slots ??= Array.Empty<int>();
            if (offset == 0 && length == slots.Length)
                return slots;

            var slice = new int[length];
            Array.Copy(slots, offset, slice, 0, length);
            return slice;
        }

        private static InvalidOperationException Wrap(TargetInvocationException exception, Type type)
        {
            var inner = exception.InnerException ?? exception;
            return new InvalidOperationException(
                $"Failed to construct {type.FullName}: {inner.Message}",
                inner);
        }
    }
}
