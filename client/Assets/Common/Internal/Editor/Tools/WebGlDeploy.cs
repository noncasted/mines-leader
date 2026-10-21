using System;
using System.IO;
using Unity.Web.Stripping.Editor;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEngine;

namespace Internal
{
    /// <summary>
    ///     Editor steps of /deploy-client (.claude/skills/deploy-client), driven over MCP
    ///     with execute_menu_item: MCP execute_code does not work on the CoreCLR editor.
    ///     Every step writes one line to Logs/WebGlDeploy.txt: "&lt;STATE&gt; ..." or "ERROR ...".
    /// </summary>
    public static class WebGlDeploy
    {
        public const string ProfilePath = "Assets/Settings/Build Profiles/web_dev - Desktop - Development.asset";

        private const string StrippingSettingsPath = "Assets/Settings/DefaultSubmoduleStrippingSettings.asset";
        private const string StatusPath = "Logs/WebGlDeploy.txt";
        private const string PreviousProfileKey = "WebGlDeploy.PreviousProfile";
        private const string PreviousTargetKey = "WebGlDeploy.PreviousTarget";

        /// <summary>
        ///     Switches the editor to the web_dev profile. Addressables builds its content inside
        ///     the player build for the ACTIVE build target, so the build must not start from Linux.
        ///     The switch reloads the domain - run "Check Ready" once the editor is idle again.
        /// </summary>
        [MenuItem("Tools/Build/Deploy/1. Activate WebGL Profile")]
        public static void ActivateProfile()
        {
            Run(() =>
            {
                EnsureIdle();

                var profile = LoadProfile();
                var active = BuildProfile.GetActiveBuildProfile();
                var target = EditorUserBuildSettings.activeBuildTarget;

                // Remembered for "Restore Editor Target"; a repeated call on web_dev keeps the original state.
                if (active != profile)
                {
                    SessionState.SetString(PreviousProfileKey, active == null ? "" : AssetDatabase.GetAssetPath(active));
                    SessionState.SetInt(PreviousTargetKey, (int)target);
                }

                ConfigureStripping();
                BuildProfile.SetActiveBuildProfile(profile);

                return $"ACTIVATING {ProfilePath} (was {target})";
            });
        }

        /// <summary>
        ///     Verifies the editor is on WebGL with the web_dev profile and re-applies the stripping
        ///     settings right before the build: they live in editor state that a domain reload resets.
        /// </summary>
        [MenuItem("Tools/Build/Deploy/2. Check Ready")]
        public static void CheckReady()
        {
            Run(() =>
            {
                EnsureIdle();

                var target = EditorUserBuildSettings.activeBuildTarget;

                if (target != BuildTarget.WebGL)
                    throw new Exception($"Active build target is {target}, expected WebGL");

                var activePath = AssetDatabase.GetAssetPath(BuildProfile.GetActiveBuildProfile());

                if (activePath != ProfilePath)
                    throw new Exception($"Active build profile is '{activePath}', expected '{ProfilePath}'");

                var settings = ConfigureStripping();

                // In batchmode and right after a reload the scheduled shared assets sync has not run yet.
                SharedAddressablesSync.Sync();

                return $"READY strip={settings.SubmodulesToStrip.Count}";
            });
        }

        /// <summary>
        ///     Puts the editor back on the profile or platform it had before "Activate WebGL Profile".
        /// </summary>
        [MenuItem("Tools/Build/Deploy/3. Restore Editor Target")]
        public static void RestoreTarget()
        {
            Run(() =>
            {
                EnsureIdle();

                var previousTarget = SessionState.GetInt(PreviousTargetKey, -1);

                if (previousTarget == -1)
                    return $"RESTORED nothing to restore, stays on {EditorUserBuildSettings.activeBuildTarget}";

                var previousProfile = SessionState.GetString(PreviousProfileKey, "");

                SessionState.EraseString(PreviousProfileKey);
                SessionState.EraseInt(PreviousTargetKey);

                if (previousProfile != "")
                {
                    var profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(previousProfile);

                    if (profile == null)
                        throw new Exception($"Previous build profile not found: {previousProfile}");

                    BuildProfile.SetActiveBuildProfile(profile);
                    return $"RESTORED profile {previousProfile}";
                }

                var target = (BuildTarget)previousTarget;
                BuildProfile.SetActiveBuildProfile(null);

                if (EditorUserBuildSettings.SwitchActiveBuildTarget(BuildPipeline.GetBuildTargetGroup(target), target) == false)
                    throw new Exception($"Switch to {target} failed");

                return $"RESTORED platform {target}";
            });
        }

        private static void Run(Func<string> step)
        {
            try
            {
                var result = step();
                File.WriteAllText(StatusPath, result + "\n");
                Debug.Log($"[WebGlDeploy] {result}");
            }
            catch (Exception e)
            {
                File.WriteAllText(StatusPath, $"ERROR {e.Message}\n");
                throw;
            }
        }

        private static void EnsureIdle()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode == true)
                throw new Exception("Editor is in Play mode");

            if (EditorApplication.isCompiling == true)
                throw new Exception("Editor is compiling");

            if (BuildPipeline.isBuildingPlayer == true)
                throw new Exception("A build is already running");
        }

        private static BuildProfile LoadProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(ProfilePath);

            if (profile == null)
                throw new Exception($"Build profile not found: {ProfilePath}");

            return profile;
        }

        // The stripping pass runs only with active settings and StripAutomaticallyAfterBuild on.
        // An empty list makes the tool skip stripping silently, so it is an error here.
        private static SubmoduleStrippingSettings ConfigureStripping()
        {
            var settings = AssetDatabase.LoadAssetAtPath<SubmoduleStrippingSettings>(StrippingSettingsPath);

            if (settings == null)
                throw new Exception($"Stripping settings not found: {StrippingSettingsPath}");

            if (settings.SubmodulesToStrip.Count == 0)
                throw new Exception($"{StrippingSettingsPath} strips no submodules");

            StrippingProjectSettings.ActiveSettings = settings;
            StrippingProjectSettings.StripAutomaticallyAfterBuild = true;

            return settings;
        }
    }
}
