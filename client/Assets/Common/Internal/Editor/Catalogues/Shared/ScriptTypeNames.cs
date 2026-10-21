using System;
using System.IO;
using System.Text.RegularExpressions;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace Internal
{
    // Type names of project scripts for generators that write them into code. After a failed compile the editor keeps
    // the previous domain, so reflection (GetType, MonoScript.GetClass) reports the names of the last successful build.
    // A generator that rewrites those names after a namespace rename would keep the compile broken (the rename
    // deadlock). While the last compile failed, the name of a top-level script class is read from its source file:
    // the class named after the file and the namespace around it. Nested types and files that do not follow the
    // file-name rule keep the reflected name.
    [NoAutoStaticsCleanup]
    public static class ScriptTypeNames
    {
        private static readonly Regex NamespaceDeclaration = new(@"\bnamespace\s+([A-Za-z0-9_.]+)", RegexOptions.Compiled);

        public static bool IsDomainStale => EditorUtility.scriptCompilationFailed;

        // Full name with '+' for nested types, like Type.FullName.
        public static string FullName(Type type, MonoScript script)
        {
            if (TryReadStale(type, script, out var fullName, out _))
                return fullName;

            return type.FullName ?? type.Name;
        }

        public static string AssemblyName(Type type, MonoScript script)
        {
            if (TryReadStale(type, script, out _, out var assemblyName))
                return assemblyName;

            return type.Assembly.GetName().Name;
        }

        public static MonoScript ScriptOf(UnityEngine.Object target)
        {
            return target switch
            {
                MonoBehaviour behaviour => MonoScript.FromMonoBehaviour(behaviour),
                ScriptableObject scriptable => MonoScript.FromScriptableObject(scriptable),
                _ => null
            };
        }

        // Reads "Namespace.FileName" from a script that declares a class named after the file.
        public static bool TryReadSource(string scriptPath, out string fullName, out string assemblyName)
        {
            fullName = string.Empty;
            assemblyName = string.Empty;

            if (string.IsNullOrEmpty(scriptPath) || scriptPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) == false)
                return false;

            var fullPath = CatalogPaths.ToFullPath(scriptPath);

            if (File.Exists(fullPath) == false)
                return false;

            var source = File.ReadAllText(fullPath);
            var name = Path.GetFileNameWithoutExtension(scriptPath);
            var declaration = Regex.Match(source, @"\bclass\s+" + Regex.Escape(name) + @"\b");

            if (declaration.Success == false)
                return false;

            var ns = string.Empty;

            foreach (Match match in NamespaceDeclaration.Matches(source))
            {
                if (match.Index > declaration.Index)
                    break;

                ns = match.Groups[1].Value;
            }

            fullName = string.IsNullOrEmpty(ns) ? name : ns + "." + name;
            assemblyName = Path.GetFileNameWithoutExtension(
                CompilationPipeline.GetAssemblyNameFromScriptPath(scriptPath) ?? string.Empty);
            return true;
        }

        private static bool TryReadStale(Type type, MonoScript script, out string fullName, out string assemblyName)
        {
            fullName = string.Empty;
            assemblyName = string.Empty;

            if (IsDomainStale == false || script == null || type.DeclaringType != null)
                return false;

            return TryReadSource(AssetDatabase.GetAssetPath(script), out fullName, out assemblyName) &&
                   string.IsNullOrEmpty(assemblyName) == false;
        }
    }
}
