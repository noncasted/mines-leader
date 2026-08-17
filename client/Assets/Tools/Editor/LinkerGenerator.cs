using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Compilation;
using UnityEngine;

namespace Tools
{
    public class LinkerGenerator : IPreprocessBuildWithReport
    {
        private const string _linkXmlFolder = "Tools/Settings";

        /// <summary>
        /// Сборки вне Assets, которым нужна защита от стриппинга.
        /// Shared.Game — DTO бэкенда, их создаёт рефлексией Newtonsoft.
        /// MemoryPack.Core — предсобранная DLL, asmdef у неё нет.
        /// </summary>
        private static readonly string[] _additionalAssemblies =
        {
            "Shared.Game",
            "MemoryPack.Core"
        };

        public int callbackOrder { get; }

        public void OnPreprocessBuild(BuildReport report)
        {
            Generate();
        }

        [MenuItem("Tools/Generate link.xml")]
        public static void Generate()
        {
            var linkXmlFilePath = Path.Combine(Application.dataPath, _linkXmlFolder, "link.xml");

            Directory.CreateDirectory(Path.GetDirectoryName(linkXmlFilePath) ??
                                      throw new InvalidOperationException(
                                              $"No directory in file name {linkXmlFilePath}"
                                          ));

            var assembliesToPreserve = GetProjectAssemblyNames()
                                       .Concat(_additionalAssemblies)
                                       .Distinct()
                                       .OrderBy(s => s, StringComparer.Ordinal)
                                       .ToList();

            var content = Enumerable.Empty<string>()
                                    .Concat("<linker>")
                                    .Concat(string.Empty)
                                    .Concat(assembliesToPreserve.Select(assemblyName =>
                                                $"    <assembly fullname=\"{assemblyName}\" preserve=\"all\" />"
                                            )
                                        )
                                    .Concat(string.Empty)
                                    .Concat("</linker>")
                                    .Aggregate(new StringBuilder(), (builder, line) => builder.AppendLine(line));

            using var fileStream = File.Open(linkXmlFilePath, FileMode.Create);
            using var streamWriter = new StreamWriter(fileStream);

            streamWriter.Write(content);
        }

        /// <summary>
        /// Только собственные сборки проекта: asmdef лежит под Assets и не в Plugins.
        /// Плагины и пакеты сохранять не нужно — пусть их чистит линкер.
        /// Имена берутся из компилятора, а не из имён файлов: имя asmdef-файла может
        /// расходиться с полем name внутри него, и такая запись в link.xml молча игнорируется.
        /// </summary>
        private static IEnumerable<string> GetProjectAssemblyNames()
        {
            return CompilationPipeline.GetAssemblies(AssembliesType.Player)
                                      .Where(assembly => IsProjectAssembly(assembly.name))
                                      .Select(assembly => assembly.name);
        }

        private static bool IsProjectAssembly(string assemblyName)
        {
            var definitionPath =
                    CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(assemblyName);

            if (string.IsNullOrEmpty(definitionPath))
                return false;

            var normalized = definitionPath.Replace('\\', '/');

            return normalized.StartsWith("Assets/", StringComparison.Ordinal) &&
                   normalized.StartsWith("Assets/Plugins/", StringComparison.Ordinal) == false;
        }
    }

    public static class LinkerGeneratorExtensions
    {
        public static IEnumerable<TItem> Concat<TItem>(this IEnumerable<TItem> enumerable, TItem item) =>
            enumerable.Concat(new[] { item });
    }
}
