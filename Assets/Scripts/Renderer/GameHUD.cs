using UnityEngine;
using UnityEngine.UI;
using Unity.Entities;
using Groundwork.Simulation;

namespace Groundwork.Renderer
{
    /// <summary>
    /// Minimal HUD overlay showing population, food, and firewood stats.
    /// Reads from the ECS simulation world each frame.
    /// Auto-creates UGUI Canvas if none exists.
    /// </summary>
    public class GameHUD : MonoBehaviour
    {
        private GameLoop _gameLoop;
        private Canvas _canvas;
        private Text _statsText;

        void Start()
        {
            _gameLoop = FindAnyObjectByType<GameLoop>();
            CreateCanvas();
        }

        void Update()
        {
            if (_gameLoop?.World == null || !_gameLoop.World.IsCreated)
                return;

            UpdateStats();
        }

        private void CreateCanvas()
        {
            // Find or create canvas
            _canvas = FindAnyObjectByType<Canvas>();
            if (_canvas == null)
            {
                var canvasGo = new GameObject("HUD Canvas");
                _canvas = canvasGo.AddComponent<Canvas>();
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                canvasGo.AddComponent<GraphicRaycaster>();
            }

            // Create stats text
            var textGo = new GameObject("StatsText");
            textGo.transform.SetParent(_canvas.transform, false);
            _statsText = textGo.AddComponent<Text>();

            // Use built-in font
            _statsText.font = Font.CreateDynamicFontFromOSFont("Arial", 16);
            _statsText.fontSize = 26;
            _statsText.color = new Color(1f, 1f, 1f, 0.9f);
            _statsText.alignment = TextAnchor.UpperLeft;

            var rt = _statsText.rectTransform;
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(12, -12);
            rt.sizeDelta = new Vector2(300, 200);

            // Background panel
            var bg = textGo.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.5f);
            textGo.transform.SetAsFirstSibling(); // bg behind text
        }

        private void UpdateStats()
        {
            if (_statsText == null) return;

            int pop = 0, food = 0, firewood = 0, year = 1, tick = 0;

            try
            {
                var statsQuery = _gameLoop.World.EntityManager.CreateEntityQuery(typeof(SimulationStats));
                if (!statsQuery.IsEmpty)
                {
                    var stats = statsQuery.GetSingleton<SimulationStats>();
                    pop = stats.Population;
                    food = stats.TotalFood;
                    firewood = stats.TotalFirewood;
                }
                statsQuery.Dispose();

                var configQuery = _gameLoop.World.EntityManager.CreateEntityQuery(typeof(SimulationConfig));
                if (!configQuery.IsEmpty)
                {
                    tick = (int)configQuery.GetSingleton<SimulationConfig>().CurrentTick;
                }
                configQuery.Dispose();

                var calQuery = _gameLoop.World.EntityManager.CreateEntityQuery(typeof(CalendarSingleton));
                if (!calQuery.IsEmpty)
                {
                    year = calQuery.GetSingleton<CalendarSingleton>().Year;
                }
                calQuery.Dispose();
            }
            catch { /* sim may not be ready yet */ }

            _statsText.text = string.Format(
                "Year {0} | Tick {1}\nPop: {2} | Food: {3} | Firewood: {4}",
                year, tick, pop, food, firewood);
        }
    }
}