using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Internal.Tests
{
    [Category("Container")]
    public class ScopeCodegenDiagnosticTests
    {
        [Test]
        public void CINGR001_UncoveredSyntax_IsError()
        {
            var block = DescriptorBlock("CINGR001");

            StringAssert.Contains("defaultSeverity: DiagnosticSeverity.Error", block);
            StringAssert.DoesNotContain("DiagnosticSeverity.Warning", block);
        }

        [Test]
        public void CINGR003_MissingRegistration_IsErrorWithParameterName()
        {
            var block = DescriptorBlock("CINGR003");

            StringAssert.Contains("defaultSeverity: DiagnosticSeverity.Error", block);
            StringAssert.Contains("Cannot resolve parameter '{0}' of type '{1}'", block);
        }

        [Test]
        public void CINGR004_Cycle_IsErrorWithPath()
        {
            var block = DescriptorBlock("CINGR004");

            StringAssert.Contains("defaultSeverity: DiagnosticSeverity.Error", block);
            StringAssert.Contains("Circular dependency: {0}", block);
        }

        private static string DescriptorBlock(string id)
        {
            var source = File.ReadAllText(GraphDescriptorsPath());
            var marker = "id: \"" + id + "\"";
            var index = source.IndexOf(marker, StringComparison.Ordinal);
            Assert.GreaterOrEqual(index, 0, "GraphDescriptors is missing " + id);

            var start = source.LastIndexOf("new DiagnosticDescriptor(", index);
            Assert.GreaterOrEqual(start, 0, "descriptor constructor not found for " + id);
            var end = source.IndexOf(");", index);
            Assert.Greater(end, start, "descriptor constructor not closed for " + id);
            return source.Substring(start, end - start);
        }

        private static string GraphDescriptorsPath()
        {
            var path = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "../Tools~/ContainerGenerator/Graph/GraphDescriptors.cs"));
            Assert.IsTrue(File.Exists(path), "GraphDescriptors.cs not found at " + path);
            return path;
        }
    }
}
