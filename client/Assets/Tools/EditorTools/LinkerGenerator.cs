using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Tools
{
    public class LinkerGenerator : IPreprocessBuildWithReport
    {
        private const string _sourcesFolder = "/";

        public int callbackOrder { get; }

        public void OnPreprocessBuild(BuildReport report)
        {
            Generate();
        }

        [MenuItem("Tools/Generate link.xml")]
        public static void Generate()
        {
            var assetsDir = Application.dataPath;

            var linkXmlFilePath = Path.Combine(assetsDir, Application.dataPath + "/Tools/Settings/", "link.xml");

            Directory.CreateDirectory(Path.GetDirectoryName(linkXmlFilePath) ??
                                      throw new InvalidOperationException(
                                          $"No directory in file name {linkXmlFilePath}"));

            var assembliesToPreserve = Enumerable.Empty<string>()
                .Concat(GetDllAssemblyNames(assetsDir + _sourcesFolder))
                .Distinct()
                .OrderBy(s => s)
                .ToList();

            assembliesToPreserve.Add("Shared");

            var content = Enumerable.Empty<string>()
                .Concat("<linker>")
                .Concat(string.Empty)
                .Concat(assembliesToPreserve.Select(assemblyName =>
                    $"    <assembly fullname=\"{assemblyName}\" preserve=\"all\" />"))
                .Concat(string.Empty)
                .Concat("</linker>")
                .Aggregate(new StringBuilder(), (builder, line) => builder.AppendLine(line));

            using var fileStream = File.Open(linkXmlFilePath, FileMode.Create);
            using var streamWriter = new StreamWriter(fileStream);

            streamWriter.Write(content);
        }

        private static IEnumerable<string> GetDllAssemblyNames(string assetsDir)
        {
            var asmdefs = Directory.EnumerateFiles(assetsDir, "*.asmdef", SearchOption.AllDirectories)
                .Distinct()
                .Select(Path.GetFileNameWithoutExtension);

            var dlss = Directory.EnumerateFiles(assetsDir, "*.dll", SearchOption.AllDirectories)
                .Distinct()
                .Select(Path.GetFileNameWithoutExtension);

            var all = new List<string>();

            all.AddRange(asmdefs);
            all.AddRange(dlss);

            return all.Where(t =>
                t.Contains("Editor") == false &&
                t.Contains("Test") == false &&
                t.Contains("Tests") == false &&
                t.Contains("Demo") == false);
        }
    }

    public static class LinkerGeneratorExtensions
    {
        public static IEnumerable<TItem> Concat<TItem>(this IEnumerable<TItem> enumerable, TItem item) =>
            enumerable.Concat(new[] { item });
    }
}