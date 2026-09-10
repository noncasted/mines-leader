using System;

namespace Internal {
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public sealed class ContainerRuntimeScopeAttribute : Attribute {
    }
}
