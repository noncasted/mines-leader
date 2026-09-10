using System;

namespace Internal
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true, Inherited = false)]
    public sealed class ContainerInstallerAttribute : Attribute
    {
        public ContainerInstallerAttribute(
            string methodId,
            bool isRoot,
            string file,
            int line,
            Type[] implementations,
            Type[] services,
            string[] calls,
            string blob)
        {
            MethodId = methodId ?? "";
            IsRoot = isRoot;
            File = file ?? "";
            Line = line;
            Implementations = implementations ?? Array.Empty<Type>();
            Services = services ?? Array.Empty<Type>();
            Calls = calls ?? Array.Empty<string>();
            Blob = blob ?? "";
        }

        public string MethodId { get; }
        public bool IsRoot { get; }
        public string File { get; }
        public int Line { get; }
        public Type[] Implementations { get; }
        public Type[] Services { get; }
        public string[] Calls { get; }
        public string Blob { get; }
    }
}