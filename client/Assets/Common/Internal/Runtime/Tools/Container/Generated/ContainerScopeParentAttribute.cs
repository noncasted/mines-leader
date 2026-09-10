using System;

namespace Internal {
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public sealed class ContainerScopeParentAttribute : Attribute {
        public ContainerScopeParentAttribute(Type rootType, string rootMethod) {
            RootType = rootType;
            RootMethod = rootMethod ?? "";
        }

        public Type RootType { get; }
        public string RootMethod { get; }
    }
}
