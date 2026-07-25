using UnityEditor;
using UnityEngine;
using UnitySkills;

namespace Groundwork.Setup
{
    /// <summary>
    /// Forces the UnitySkills REST server to start by setting ServerShouldRun = true.
    /// Run after UnitySkillsSetup.Run() if the server doesn't auto-start.
    /// Usage: Unity -executeMethod Groundwork.Setup.UnitySkillsServerStart.Run
    /// </summary>
    public static class UnitySkillsServerStart
    {
        [MenuItem("Groundwork/Force Start UnitySkills Server")]
        public static void Run()
        {
            var instanceId = RegistryService.InstanceId;
            Debug.Log($"[Groundwork] Project InstanceId: {instanceId}");

            var prefKey = $"UnitySkills_{instanceId}_ServerShouldRun";
            var autoStartKey = $"UnitySkills_{instanceId}_AutoStart";

            EditorPrefs.SetBool(prefKey, true);
            EditorPrefs.SetBool(autoStartKey, true);

            Debug.Log($"[Groundwork] Set {prefKey} = true");
            Debug.Log($"[Groundwork] Set {autoStartKey} = true");

            // Trigger CheckAndRestoreServer via reflection
            var serverType = typeof(SkillsHttpServer);
            var method = serverType.GetMethod("CheckAndRestoreServer",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            if (method != null)
            {
                method.Invoke(null, null);
                Debug.Log("[Groundwork] Called CheckAndRestoreServer() — server should be starting");
            }
            else
            {
                Debug.LogWarning("[Groundwork] Could not find CheckAndRestoreServer method");
                Debug.Log("[Groundwork] Please close and reopen the project for the server to start");
            }
        }
    }
}