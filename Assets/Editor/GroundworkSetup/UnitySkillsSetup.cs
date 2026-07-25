using UnityEditor;
using UnityEngine;

namespace Groundwork.Setup
{
    /// <summary>
    /// One-time setup for UnitySkills REST server. Configures Bypass mode
    /// and auto-start so Hermes can interact with Unity without GUI interaction.
    /// Run once via: Unity -executeMethod Groundwork.Setup.UnitySkillsSetup.Run
    /// </summary>
    public static class UnitySkillsSetup
    {
        [MenuItem("Groundwork/Setup UnitySkills for Agent")]
        public static void Run()
        {
            // Force Bypass mode — no approval prompts, no restrictions
            EditorPrefs.SetString("UnitySkills_OperatingMode", "Bypass");
            Debug.Log("[Groundwork] UnitySkills mode set to Bypass");

            // Ensure panel approval is NOT required (default: false)
            EditorPrefs.SetBool("UnitySkills_PanelApprovalRequired", false);
            Debug.Log("[Groundwork] Panel approval disabled");

            // Auto-start is on by default (UnitySkills_{InstanceId}_AutoStart = true)
            // The server will start automatically when Unity opens the project

            // Set preferred port to 8090 (default)
            EditorPrefs.SetInt("UnitySkills_PreferredPort", 8090);
            Debug.Log("[Groundwork] Preferred port set to 8090");

            // Disable Sentry in batch mode to avoid noise
            EditorPrefs.SetBool("UnitySkills_TelemetryEnabled", false);
            Debug.Log("[Groundwork] UnitySkills telemetry disabled");

            Debug.Log("[Groundwork] UnitySkills setup complete. Server will auto-start on project open.");
            Debug.Log("[Groundwork] Verify: curl http://localhost:8090/health");
        }
    }
}