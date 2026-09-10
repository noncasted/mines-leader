using System;
using System.Collections.Generic;

namespace Internal
{
    internal sealed class ContainerSlot
    {
        public int Index;
        public Type ImplementationType;
        public Type[] ServiceTypes;
        public ServiceLifetime Lifetime;
        public Dictionary<Type, object> Parameters;
        public object ExistingInstance;
        public bool SuppressCreate;
        public bool SuppressConstruct;
        public bool OwnsInstance;
        public bool IsGenerated;
        public bool IsExternal;
        public bool IsSelfResolvable;
        public int[] Dependencies;
        public IInjector Injector;
    }
}
