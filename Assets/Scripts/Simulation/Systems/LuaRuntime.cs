using System.Collections.Generic;
using System.IO;
using MoonSharp.Interpreter;
using UnityEngine;

namespace Groundwork.Simulation
{
    /// <summary>
    /// Managed Lua runtime that lives outside the ECS world. Holds sandboxed
    /// MoonSharp Script instances for each loaded mod. Provides hooks that
    /// LuaModSystem calls each tick.
    /// </summary>
    public static class LuaRuntime
    {
        private static readonly List<Script> _scripts = new();
        private static bool _sandboxApplied;

        /// <summary>Load all .lua files from StreamingAssets/Mods/.</summary>
        public static void LoadMods(string modsPath)
        {
            if (!Directory.Exists(modsPath))
            {
                Debug.Log($"[LuaRuntime] No mods directory at {modsPath}");
                return;
            }

            var files = Directory.GetFiles(modsPath, "*.lua");
            foreach (var file in files)
            {
                try
                {
                    var script = CreateSandboxedScript();
                    var code = File.ReadAllText(file);
                    script.DoString(code);
                    _scripts.Add(script);
                    Debug.Log($"[LuaRuntime] Loaded mod: {Path.GetFileName(file)}");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[LuaRuntime] Failed to load mod {Path.GetFileName(file)}: {e.Message}");
                }
            }
        }

        /// <summary>Create a sandboxed MoonSharp Script with dangerous modules removed.</summary>
        private static Script CreateSandboxedScript()
        {
            var script = new Script(CoreModules.Preset_SoftSandbox);
            // Remove debug and package modules entirely
            script.Globals["debug"] = DynValue.Nil;
            script.Globals["package"] = DynValue.Nil;

            // Redirect print to Unity debug log
            script.Options.DebugPrint = s => Debug.Log($"[LuaMod] {s}");

            // Expose safe API functions
            script.Globals["log"] = (System.Action<string>)(msg => Debug.Log($"[LuaMod] {msg}"));

            return script;
        }

        /// <summary>Call on_init() on all mods that haven't been initialized yet.</summary>
        public static void CallOnInit()
        {
            foreach (var script in _scripts)
            {
                CallIfExists(script, "on_init");
            }
        }

        /// <summary>Call on_tick() on all mods.</summary>
        public static void CallOnTick()
        {
            foreach (var script in _scripts)
            {
                CallIfExists(script, "on_tick");
            }
        }

        /// <summary>Call on_season_change(seasonIndex) on all mods.</summary>
        public static void CallOnSeasonChange(int season)
        {
            foreach (var script in _scripts)
            {
                var fn = script.Globals.Get("on_season_change");
                if (fn.Type == DataType.Function)
                {
                    try { fn.Function.Call(season); }
                    catch (ScriptRuntimeException e)
                    {
                        Debug.LogError($"[LuaRuntime] Error in on_season_change: {e.DecoratedMessage}");
                    }
                }
            }
        }

        /// <summary>Call on_event(eventType, entityId) on all mods.</summary>
        public static void CallOnEvent(string eventType, int entityId)
        {
            foreach (var script in _scripts)
            {
                var fn = script.Globals.Get("on_event");
                if (fn.Type == DataType.Function)
                {
                    try { fn.Function.Call(eventType, entityId); }
                    catch (ScriptRuntimeException e)
                    {
                        Debug.LogError($"[LuaRuntime] Error in on_event: {e.DecoratedMessage}");
                    }
                }
            }
        }

        /// <summary>Check if any mods are loaded.</summary>
        public static bool HasMods => _scripts.Count > 0;

        private static void CallIfExists(Script script, string functionName)
        {
            var fn = script.Globals.Get(functionName);
            if (fn.Type == DataType.Function)
            {
                try { fn.Function.Call(); }
                catch (ScriptRuntimeException e)
                {
                    Debug.LogError($"[LuaRuntime] Error in {functionName}: {e.DecoratedMessage}");
                }
            }
        }
    }
}