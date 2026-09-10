using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Internal
{
    /// <summary>
    ///     Batchmode entry point for CI/deploy scripts:
    ///     Unity -quit -batchmode -projectPath client -executeMethod Internal.WebGlBuild.Build
    ///     Output goes to client/build, which is what client/deploy/Dockerfile packages.
    /// </summary>
    public static class WebGlBuild
    {
        private const string DefaultOutput = "build";

        [MenuItem("Tools/Build/WebGL")]
        public static void Build()
        {
            var output = GetArgument("-buildOutput") ?? DefaultOutput;

            // В batchmode отложенный пересчёт общих ассетов не успевает отработать до сборки.
            SharedAddressablesSync.Sync();

            var scenes = EditorBuildSettings.scenes
                                            .Where(scene => scene.enabled)
                                            .Select(scene => scene.path)
                                            .ToArray();

            if (scenes.Length == 0)
                throw new Exception("No enabled scenes in Build Settings.");

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;

            Debug.Log($"WebGL build {summary.result}: {summary.totalSize} bytes -> {output}");

            var succeeded = summary.result == BuildResult.Succeeded;

            // -quit alone swallows exceptions and still exits 0, so the deploy
            // script would happily package a stale build. Exit explicitly.
            if (Application.isBatchMode == true)
                EditorApplication.Exit(succeeded ? 0 : 1);

            if (succeeded == false)
                throw new Exception($"WebGL build failed: {summary.result}");
        }

        private static string GetArgument(string name)
        {
            var args = Environment.GetCommandLineArgs();

            for (var i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name)
                    return args[i + 1];
            }

            return null;
        }
    }
}