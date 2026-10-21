namespace UnityEngine.UI
{
    // Clipboard access for InputField and TMP_InputField without the IMGUI module.
    // GUIUtility.systemCopyBuffer (IMGUI) and SystemCopyBuffer.systemCopyBuffer (Core)
    // read and write the same native copy buffer; on WebGL, Core's CopyPaste.js syncs it
    // with the browser copy/cut/paste events.
    internal static class UIClipboard
    {
        public static string text
        {
            get { return SystemCopyBuffer.systemCopyBuffer; }
            set { SystemCopyBuffer.systemCopyBuffer = value; }
        }
    }
}
