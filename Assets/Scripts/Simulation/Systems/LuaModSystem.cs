using Unity.Entities;
using Unity.Collections;
using UnityEngine;

namespace Groundwork.Simulation
{
    /// <summary>
    /// Loads Lua mod scripts at startup and dispatches simulation events
    /// to mod hooks (on_init, on_tick, on_season_change, on_event).
    /// Runs after EventDispatchSystem so mods react to all events emitted
    /// during the tick.
    /// </summary>
    public partial struct LuaModSystem : ISystem
    {
        private bool _modsLoaded;

        public void OnUpdate(ref SystemState state)
        {
            // Load mods once on first tick (StreamingAssets available after domain reload)
            if (!_modsLoaded)
            {
                _modsLoaded = true;
                var modsPath = System.IO.Path.Combine(Application.streamingAssetsPath, "Mods");
                LuaRuntime.LoadMods(modsPath);
                LuaRuntime.CallOnInit();
            }

            if (!LuaRuntime.HasMods)
                return;

            // Call on_tick for every game tick
            LuaRuntime.CallOnTick();

            // Check for season change events
            var calendar = SystemAPI.GetSingleton<CalendarSingleton>();
            // We track the last known season to detect transitions
            // For simplicity, call on_season_change every tick with current season
            // (mods can implement their own debouncing)
            LuaRuntime.CallOnSeasonChange(calendar.Season);

            // Dispatch events from the event buffer
            if (SystemAPI.TryGetSingletonEntity<SimulationEventSingleton>(out var eventEntity))
            {
                var events = state.EntityManager.GetBuffer<SimulationEvent>(eventEntity);
                for (int i = 0; i < events.Length; i++)
                {
                    var evt = events[i];
                    LuaRuntime.CallOnEvent(evt.Type.ToString(), evt.EntityId);
                }
            }
        }
    }
}