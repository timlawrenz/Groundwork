using UnityEditor;
using UnityEngine;
using UnitySkills;

namespace Groundwork.Setup
{
    /// <summary>
    /// Direct server start using EditorApplication.update to ensure the Editor
    /// is fully initialized before starting. Works in batch mode.
    /// Usage: Unity -batchmode -nographics -projectPath . -executeMethod Groundwork.Setup.UnitySkillsServerPersist.Start
    /// </summary>
    public static class UnitySkillsServerPersist
    {
        private static int _frameCount = 0;

        [MenuItem("Groundwork/Start UnitySkills Server (Persist)")]
        public static void Start()
        {
            EditorApplication.update += OnUpdate;
            Debug.Log("[Groundwork] Waiting for Editor initialization...");
        }

        private static void OnUpdate()
        {
            _frameCount++;
            if (_frameCount < 10) return; // Wait 10 frames for full initialization

            EditorApplication.update -= OnUpdate;

            Debug.Log("[Groundwork] Editor initialized — starting server");

            // Set prefs
            var instanceId = RegistryService.InstanceId;
            EditorPrefs.SetString("UnitySkills_OperatingMode", "Bypass");
            EditorPrefs.SetBool($"UnitySkills_{instanceId}_ServerShouldRun", true);
            EditorPrefs.SetBool($"UnitySkills_{instanceId}_AutoStart", true);

            // Call CheckAndRestoreServer
            var serverType = typeof(SkillsHttpServer);
            var method = serverType.GetMethod("CheckAndRestoreServer",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            if (method != null)
            {
                method.Invoke(null, null);
                Debug.Log("[Groundwork] Server start triggered");
            }

            // Schedule a health check
            EditorApplication.delayCall += () =>
            {
                if (SkillsHttpServer.IsRunning)
                {
                    Debug.Log($"[Groundwork] Server is running at {SkillsHttpServer.Url}");
                    Debug.Log("[Groundwork] Bypass mode active — no approval prompts needed");
                }
                else
                {
                    Debug.LogWarning("[Groundwork] Server did not start. Check console for errors.");
                }
            };
        }
    }
}