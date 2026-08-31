using UnityEditor;

namespace Internal
{
    /// <summary>
    /// MCP for Unity writes screenshots to "Assets/Screenshots" by default, which pollutes the
    /// asset database with imported PNGs. The folder is stored in per-user EditorPrefs, so it is
    /// re-applied on every project load to keep it out of Assets.
    /// </summary>
    [InitializeOnLoad]
    public static class McpScreenshotsFolder
    {
        private const string PrefsKey = "MCPForUnity_ScreenshotsFolder";
        private const string Folder = "Temp/Screenshots";

        static McpScreenshotsFolder()
        {
            if (EditorPrefs.GetString(PrefsKey, string.Empty) == Folder)
                return;

            EditorPrefs.SetString(PrefsKey, Folder);
        }
    }
}
