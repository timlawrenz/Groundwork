using Unity.Entities;
using Unity.Collections;
using UnityEngine;
using System.IO;

namespace Groundwork.Simulation
{
    /// <summary>
    /// Creates recipe, building, and need definition entities at startup
    /// from JSON files in StreamingAssets. No hardcoded content — all game
    /// data is moddable without recompilation.
    /// Runs once before SimulationBootstrap.
    /// </summary>
    public partial struct ContentLoaderSystem : ISystem
    {
        private bool _initialized;

        // JSON wrapper types for Unity's JsonUtility (can't deserialize top-level arrays)
        [System.Serializable]
        private struct RecipeListWrapper { public RecipeJson[] recipes; }
        [System.Serializable]
        private struct RecipeJson
        {
            public string id;
            public int ticksPerCycle;
            public RecipeInputJson[] inputs;
            public RecipeOutputJson[] outputs;
        }
        [System.Serializable]
        private struct RecipeInputJson { public string itemId; public int quantity; }
        [System.Serializable]
        private struct RecipeOutputJson { public string itemId; public int quantity; }

        [System.Serializable]
        private struct BuildingListWrapper { public BuildingJson[] buildings; }
        [System.Serializable]
        private struct BuildingJson
        {
            public string type;
            public int maxWorkers;
            public bool requiresWorkers;
            public int inputCapacity;
            public int outputCapacity;
            public string archetype;
            public int gatheringRadius;
            public int footprintSize;
            public string[] recipes;
        }

        [System.Serializable]
        private struct NeedListWrapper { public NeedJson[] needs; }
        [System.Serializable]
        private struct NeedJson
        {
            public string type;
            public string satisfyingItem;
            public float urgencyGrowthPerDay;
            public float coldSeasonGrowthMultiplier;
            public float criticalThreshold;
            public float healthDecayRate;
            public float satisfactionReduction;
            public float initialUrgency;
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_initialized)
                return;
            _initialized = true;

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var basePath = GetStreamingAssetsPath();

            // ─── Load recipe definitions ───
            var recipeJson = LoadJson<RecipeListWrapper>(basePath, "Recipes.json");
            if (recipeJson.recipes != null)
            {
                foreach (var r in recipeJson.recipes)
                {
                    var entity = ecb.CreateEntity();
                    ecb.AddComponent(entity, new RecipeDefinitionData
                    {
                        RecipeId = r.id,
                        TicksPerCycle = r.ticksPerCycle,
                    });
                    var inputs = ecb.AddBuffer<RecipeInput>(entity);
                    if (r.inputs != null)
                        foreach (var inp in r.inputs)
                            inputs.Add(new RecipeInput { ItemId = inp.itemId, Quantity = inp.quantity });
                    var outputs = ecb.AddBuffer<RecipeOutput>(entity);
                    if (r.outputs != null)
                        foreach (var outp in r.outputs)
                            outputs.Add(new RecipeOutput { ItemId = outp.itemId, Quantity = outp.quantity });
                }
            }

            // ─── Load building definitions ───
            var buildingJson = LoadJson<BuildingListWrapper>(basePath, "Buildings.json");
            if (buildingJson.buildings != null)
            {
                foreach (var b in buildingJson.buildings)
                {
                    var entity = ecb.CreateEntity();
                    ecb.AddComponent(entity, new BuildingDefinitionData
                    {
                        BuildingType = b.type,
                        MaxWorkers = b.maxWorkers,
                        RequiresWorkers = b.requiresWorkers,
                        InputCapacity = b.inputCapacity,
                        OutputCapacity = b.outputCapacity,
                        Archetype = ParseArchetype(b.archetype),
                        GatheringRadius = b.gatheringRadius,
                        FootprintSize = (byte)(b.footprintSize > 0 ? b.footprintSize : 1),
                    });
                    var recipes = ecb.AddBuffer<BuildingRecipe>(entity);
                    if (b.recipes != null)
                        foreach (var r in b.recipes)
                            recipes.Add(new BuildingRecipe { RecipeId = r });
                }
            }

            // ─── Load need definitions ───
            var needJson = LoadJson<NeedListWrapper>(basePath, "Needs.json");
            if (needJson.needs != null)
            {
                foreach (var n in needJson.needs)
                {
                    var entity = ecb.CreateEntity();
                    ecb.AddComponent(entity, new NeedDefinition
                    {
                        NeedType = n.type,
                        SatisfyingItem = n.satisfyingItem,
                        UrgencyGrowthPerDay = n.urgencyGrowthPerDay,
                        ColdSeasonGrowthMultiplier = n.coldSeasonGrowthMultiplier,
                        CriticalThreshold = n.criticalThreshold,
                        HealthDecayRate = n.healthDecayRate,
                        SatisfactionReduction = n.satisfactionReduction,
                        InitialUrgency = n.initialUrgency,
                    });
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private static string GetStreamingAssetsPath()
        {
            // In the Unity Editor (including EditMode tests), streamingAssetsPath
            // points to <project>/Assets/StreamingAssets
            return Application.streamingAssetsPath;
        }

        private static T LoadJson<T>(string basePath, string filename)
        {
            var path = Path.Combine(basePath, filename);
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[ContentLoader] JSON file not found: {path}");
                return default;
            }
            var json = File.ReadAllText(path);
            return JsonUtility.FromJson<T>(json);
        }

        private static ProductionArchetype ParseArchetype(string archetype)
        {
            switch (archetype)
            {
                case "Workshop": return ProductionArchetype.Workshop;
                case "Gathering": return ProductionArchetype.Gathering;
                case "Source": return ProductionArchetype.Source;
                case "Service": return ProductionArchetype.Service;
                default: return ProductionArchetype.Service;
            }
        }
    }
}