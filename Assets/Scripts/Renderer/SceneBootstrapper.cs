using UnityEngine;
using UnityEngine.SceneManagement;

namespace Groundwork.Renderer
{
    /// <summary>
    /// Automatically sets up the renderer scene objects when entering Play mode.
    /// No manual scene setup required — just open any scene and press Play.
    ///
    /// Creates:
    /// - Groundwork Renderer (GameLoop + MapRenderer)
    /// - Main Camera with CameraController (if none exists)
    /// - Directional Light (if none exists)
    /// </summary>
    public static class SceneBootstrapper
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void OnSceneLoaded()
        {
            SetupRenderer();
            SetupCamera();
            SetupLighting();
        }

        private static void SetupRenderer()
        {
            // Only create if not already present in the scene
            if (Object.FindAnyObjectByType<GameLoop>() != null)
                return;

            var go = new GameObject("Groundwork Renderer");
            go.AddComponent<GameLoop>();
            go.AddComponent<MapRenderer>();

            Debug.Log("[SceneBootstrapper] Created Groundwork Renderer.");
        }

        private static void SetupCamera()
        {
            // Find existing main camera
            var mainCam = Camera.main;
            if (mainCam != null)
            {
                // Add controller if missing
                if (mainCam.GetComponent<CameraController>() == null)
                {
                    mainCam.gameObject.AddComponent<CameraController>();
                    Debug.Log("[SceneBootstrapper] Added CameraController to existing Main Camera.");
                }
                return;
            }

            // Create new camera
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.AddComponent<Camera>();
            camGo.AddComponent<CameraController>();

            Debug.Log("[SceneBootstrapper] Created Main Camera with CameraController.");
        }

        private static void SetupLighting()
        {
            // Only add if no directional lights exist
            var existingLights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            foreach (var existingLight in existingLights)
            {
                if (existingLight.type == LightType.Directional)
                    return;
            }

            var lightGo = new GameObject("Directional Light");
            var dirLight = lightGo.AddComponent<Light>();
            dirLight.type = LightType.Directional;
            dirLight.intensity = 1.2f;
            dirLight.shadows = LightShadows.None; // minimal perf
            dirLight.transform.rotation = Quaternion.Euler(60f, -30f, 0f);

            Debug.Log("[SceneBootstrapper] Created Directional Light.");
        }
    }
}