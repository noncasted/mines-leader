using System;

namespace Internal {
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true, Inherited = false)]
    public sealed class ContainerGraphAssetAttribute : Attribute {
        public ContainerGraphAssetAttribute(string assetPath, string holderType, string[] componentTypes) {
            AssetPath = assetPath ?? "";
            HolderType = holderType ?? "";
            ComponentTypes = componentTypes ?? Array.Empty<string>();
        }

        public string AssetPath { get; }
        public string HolderType { get; }
        public string[] ComponentTypes { get; }
    }
}
