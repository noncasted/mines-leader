using System;

namespace Internal
{
    // Marks a registration primitive of the builder API. The container generator recognizes these methods by the
    // attribute, not by their name or class, so a primitive may be renamed or moved freely. The numeric values are
    // read by the generator (client/Tools~/ContainerGenerator, PrimitiveKind) and must stay in sync with it.
    public enum ContainerPrimitive
    {
        Register = 1,
        RegisterInstance = 2,
        RegisterComponent = 3,
        As = 4,
        AsSelf = 5,
        AsSelfResolvable = 6,
        WithParameter = 7,
        WithScopeLifetime = 8,
        Inject = 9,
        Injectable = 10,
        Instantiate = 11,
        SceneServices = 12
    }

    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public sealed class ContainerPrimitiveAttribute : Attribute
    {
        public ContainerPrimitiveAttribute(ContainerPrimitive kind)
        {
            Kind = kind;
        }

        public ContainerPrimitive Kind { get; }
    }
}
