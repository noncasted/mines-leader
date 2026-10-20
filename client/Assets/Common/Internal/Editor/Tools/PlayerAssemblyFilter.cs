using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;

namespace Internal
{
    /// <summary>
    /// Drops runtime assemblies of PackageCache packages that only editor code needs.
    /// - MCPForUnity.Runtime: the linker keeps it because of a MonoBehaviour inside, and its converters
    ///   drag Newtonsoft.Json into the player.
    /// - Unity.2D.Tilemap.Runtime: comes with the Aseprite importer and is [AlwaysLinkAssembly]. It keeps
    ///   the Tilemap engine module, and that one keeps Physics2D. The game has no tilemaps and no physics.
    /// </summary>
    public class PlayerAssemblyFilter : IFilterBuildAssemblies
    {
        private static readonly HashSet<string> AssemblyNames = new()
        {
            "MCPForUnity.Runtime",
            "Unity.2D.Tilemap.Runtime"
        };

        public int callbackOrder => 0;

        public string[] OnFilterAssemblies(BuildOptions buildOptions, string[] assemblies)
        {
            return assemblies
                   .Where(path => AssemblyNames.Contains(Path.GetFileNameWithoutExtension(path)) == false)
                   .ToArray();
        }
    }
}
