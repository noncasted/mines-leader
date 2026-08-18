#if !MCP_FOR_UNITY
using System;

namespace MCPForUnity.Editor.Tools {
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class McpForUnityToolAttribute : Attribute {
        public McpForUnityToolAttribute() {
        }

        public McpForUnityToolAttribute(string name) {
            Name = name;
        }

        public string Name { get; set; }
        public string Description { get; set; }
        public bool StructuredOutput { get; set; } = true;
        public bool AutoRegister { get; set; } = true;
        public string Group { get; set; } = "core";
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public sealed class ToolParameterAttribute : Attribute {
        public ToolParameterAttribute(string description) {
            Description = description;
        }

        public string Name { get; }
        public string Description { get; set; }
        public bool Required { get; set; } = true;
        public string DefaultValue { get; set; }
    }
}
#endif
