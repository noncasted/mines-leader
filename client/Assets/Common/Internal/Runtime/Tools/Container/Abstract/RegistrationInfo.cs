using System;
using System.Collections.Generic;

namespace Internal
{
    public readonly struct RegistrationInfo
    {
        public RegistrationInfo(
            int slot,
            Type implementationType,
            IReadOnlyList<Type> serviceTypes,
            ServiceLifetime lifetime,
            IReadOnlyList<int> dependencies,
            bool isInstantiated,
            bool isExternal)
        {
            Slot = slot;
            ImplementationType = implementationType;
            ServiceTypes = serviceTypes;
            Lifetime = lifetime;
            Dependencies = dependencies;
            IsInstantiated = isInstantiated;
            IsExternal = isExternal;
        }

        public readonly int Slot;
        public readonly Type ImplementationType;
        public readonly IReadOnlyList<Type> ServiceTypes;
        public readonly ServiceLifetime Lifetime;
        public readonly IReadOnlyList<int> Dependencies;
        public readonly bool IsInstantiated;
        public readonly bool IsExternal;
    }
}