#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Groundwork.Renderer
{
    public static class GroundworkBuilder
    {
        private const string ScenePath = "Assets/Scenes/DefaultScene.unity";

        public static void BuildAndroid()
        {
            // Switch to Android with both architectures
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;
            AssetDatabase.SaveAssets();
            
            Debug.Log($"[Builder] ActiveTarget={EditorUserBuildSettings.activeBuildTarget} Arch={PlayerSettings.Android.targetArchitectures} Backend=IL2CPP");
            
            var report = BuildPipeline.BuildPlayer(
                new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = "Builds/Android/Groundwork.apk",
                    target = BuildTarget.Android,
                    options = BuildOptions.None,
                });

            if (report.summary.result == BuildResult.Succeeded)
            {
                var info = new System.IO.FileInfo(report.summary.outputPath);
                Debug.Log($"[Builder] APK built: {report.summary.outputPath} ({info.Length / 1024 / 1024} MB)");
            }
            else
            {
                Debug.LogError($"[Builder] FAILED: {report.summary.result} — {report.summary.totalErrors} errors");
                foreach (var step in report.steps)
                {
                    foreach (var msg in step.messages)
                        if (msg.type == LogType.Error || msg.type == LogType.Exception)
                            Debug.LogError($"  [{step.name}] {msg.content}");
                }
            }
        }
    }
}
#endif