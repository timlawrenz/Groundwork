#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Groundwork.Renderer
{
    /// <summary>
    /// CLI-driven build entry point. Invoke via:
    ///   Unity -batchmode -quit -executeMethod Groundwork.Renderer.GroundworkBuilder.BuildAndroid
    ///
    /// Handles: scene registration, player settings, Android keystore config, build.
    /// </summary>
    public static class GroundworkBuilder
    {
        private const string ScenePath = "Assets/Scenes/DefaultScene.unity";
        private const string BundleId = "com.timlawrenz.groundwork";
        private const string KeystorePath = "Assets/Plugins/Android/groundwork-debug.keystore";
        private const string KeystorePass = "groundwork";
        private const string KeyAlias = "groundwork";
        private const string KeyAliasPass = "groundwork";

        // ── Android Build ────────────────────────

        public static void BuildAndroid()
        {
            EnsureSceneInBuildSettings();
            ConfigurePlayerSettings();
            SwitchToAndroid();

            var report = BuildPipeline.BuildPlayer(
                new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = "Builds/Android/Groundwork.apk",
                    target = BuildTarget.Android,
                    options = BuildOptions.None,
                });

            if (report.summary.result == BuildResult.Succeeded)
                Debug.Log($"[GroundworkBuilder] APK built: {report.summary.outputPath} " +
                    $"({new System.IO.FileInfo(report.summary.outputPath).Length / 1024 / 1024} MB)");
            else
                Debug.LogError($"[GroundworkBuilder] Build FAILED: {report.summary.result} — {report.summary.totalErrors} errors");
        }

        // ── WebGL Build ──────────────────────────

        public static void BuildWebGL()
        {
            EnsureSceneInBuildSettings();
            ConfigurePlayerSettings();
            SwitchToWebGL();

            var report = BuildPipeline.BuildPlayer(
                new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = "Builds/WebGL",
                    target = BuildTarget.WebGL,
                    options = BuildOptions.None,
                });

            if (report.summary.result == BuildResult.Succeeded)
                Debug.Log($"[GroundworkBuilder] WebGL build complete: {report.summary.outputPath}");
            else
                Debug.LogError($"[GroundworkBuilder] WebGL build FAILED: {report.summary.result}");
        }

        // ── Setup Only ───────────────────────────

        public static void SetupAndroid()
        {
            EnsureSceneInBuildSettings();
            ConfigurePlayerSettings();
            SwitchToAndroid();
            Debug.Log("[GroundworkBuilder] Android setup complete. Ready to build.");
        }

        // ── Internals ────────────────────────────

        private static void EnsureSceneInBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes;
            foreach (var s in scenes)
            {
                if (s.path == ScenePath)
                    return; // already registered
            }

            var newScenes = new EditorBuildSettingsScene[scenes.Length + 1];
            scenes.CopyTo(newScenes, 0);
            newScenes[^1] = new EditorBuildSettingsScene(ScenePath, true);
            EditorBuildSettings.scenes = newScenes;

            Debug.Log($"[GroundworkBuilder] Added scene to Build Settings: {ScenePath}");
        }

        private static void ConfigurePlayerSettings()
        {
            // Bundle identifier
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, BundleId);
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.WebGL, BundleId);
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Standalone, BundleId);

            // Company / product
            PlayerSettings.companyName = "Groundwork";
            PlayerSettings.productName = "Groundwork";

            // Android-specific
            PlayerSettings.Android.keystoreName = KeystorePath;
            PlayerSettings.Android.keystorePass = KeystorePass;
            PlayerSettings.Android.keyaliasName = KeyAlias;
            PlayerSettings.Android.keyaliasPass = KeyAliasPass;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minifyWithR8 = false; // faster debug builds

            // Graphics — force GLES3 for compatibility
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] {
                UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3,
                UnityEngine.Rendering.GraphicsDeviceType.Vulkan,
            });

            // Touch input
            // Gamepad: not needed for touch-first city builder
            PlayerSettings.Android.disableDepthAndStencilBuffers = false;

            // Screen — landscape preferred for map view
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;

            Debug.Log("[GroundworkBuilder] Player settings configured.");
        }

        private static void SwitchToAndroid()
        {
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android)
                return;

            EditorUserBuildSettings.SwitchActiveBuildTarget(
                BuildTargetGroup.Android, BuildTarget.Android);
            Debug.Log("[GroundworkBuilder] Switched build target to Android.");
        }

        private static void SwitchToWebGL()
        {
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.WebGL)
                return;

            EditorUserBuildSettings.SwitchActiveBuildTarget(
                BuildTargetGroup.WebGL, BuildTarget.WebGL);
            Debug.Log("[GroundworkBuilder] Switched build target to WebGL.");
        }
    }
}
#endif