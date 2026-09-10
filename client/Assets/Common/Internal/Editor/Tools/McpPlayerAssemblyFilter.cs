using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;

namespace Internal
{
    /// <summary>
    /// MCP for Unity ships MCPForUnity.Runtime for all platforms, but only editor code uses it.
    /// The linker keeps it because of a MonoBehaviour inside, and its converters drag Newtonsoft.Json
    /// into the player. The package lives in PackageCache, so the assembly is dropped from player builds here.
    /// </summary>
    public class McpPlayerAssemblyFilter : IFilterBuildAssemblies
    {
        private const string AssemblyName = "MCPForUnity.Runtime";

        public int callbackOrder => 0;

        public string[] OnFilterAssemblies(BuildOptions buildOptions, string[] assemblies)
        {
            return assemblies
                   .Where(path => Path.GetFileNameWithoutExtension(path) != AssemblyName)
                   .ToArray();
        }
    }
}
