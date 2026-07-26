using UnityEngine;
using Unity.Entities;
using Groundwork.Simulation;

namespace Groundwork.Renderer
{
    /// <summary>
    /// Creates and manages the ECS simulation World. Runs the simulation pipeline
    /// each frame and exposes the World for the MapRenderer to query.
    ///
    /// The simulation runs at a configurable speed multiplier — one Unity Update
    /// can advance the sim by multiple ticks. At 1x speed, each tick is 1 game-hour.
    /// </summary>
    public class GameLoop : MonoBehaviour
    {
        [Header("Simulation Speed")]
        [Tooltip("Game ticks per Unity frame. 1 = real-time (24 ticks = 1 game day), 10 = fast-forward.")]
        [Range(1, 100)]
        public int ticksPerFrame = 10;

        [Tooltip("Pause the simulation.")]
        public bool paused = false;

        [Header("Info (read-only)")]
        [SerializeField] private int _currentTick;
        [SerializeField] private int _currentYear;
        [SerializeField] private int _population;

        public World World { get; private set; }
        public int CurrentTick => _currentTick;
        public int CurrentYear => _currentYear;
        public int Population => _population;

        private GroundworkSimulationGroup _simGroup;
        private SystemHandle[] _tickSystems;
        private bool _bootstrapped;

        void Awake()
        {
            CreateWorld();
        }

        void Start()
        {
            Bootstrap();
        }

        void Update()
        {
            if (World == null || !World.IsCreated || !_bootstrapped || paused)
                return;

            for (int i = 0; i < ticksPerFrame; i++)
            {
                _simGroup.Update();
                _currentTick++;
            }

            UpdateInfo();
        }

        void OnDestroy()
        {
            if (World != null && World.IsCreated)
            {
                World.Dispose();
                World = null;
            }
        }

        private void CreateWorld()
        {
            World = new World("GroundworkWorld");

            // Create the simulation group (managed) — it auto-sorts child systems
            // by [UpdateInGroup] and [UpdateAfter] attributes.
            _simGroup = World.GetOrCreateSystemManaged<GroundworkSimulationGroup>();
        }

        private void Bootstrap()
        {
            if (_bootstrapped) return;

            // Phase 1: Load content definitions (BuildingDefinitionData, RecipeDefinitionData)
            var contentHandle = World.CreateSystem<ContentLoaderSystem>();
            _simGroup.AddSystemToUpdateList(contentHandle);
            _simGroup.Update();
            _simGroup.RemoveSystemFromUpdateList(contentHandle);
            // NOTE: Do NOT destroy contentHandle — it may hold blob assets
            // referenced by entities in the world.

            // Phase 2: Create initial world state (buildings, citizens, map, resources)
            var bootstrapHandle = World.CreateSystem<SimulationBootstrap>();
            _simGroup.AddSystemToUpdateList(bootstrapHandle);
            _simGroup.Update();
            _simGroup.RemoveSystemFromUpdateList(bootstrapHandle);
            // NOTE: Do NOT destroy bootstrapHandle — its blob assets (MapGridBlob)
            // are referenced by MapGridData entities in the world.

            // Phase 3: Add all tick systems in pipeline order
            _tickSystems = new SystemHandle[]
            {
                World.CreateSystem<TickDispatchSystem>(),
                World.CreateSystem<CalendarSystem>(),
                World.CreateSystem<BirthSystem>(),
                World.CreateSystem<AgeSystem>(),
                World.CreateSystem<NeedSystem>(),
                World.CreateSystem<PathfindingSystem>(),
                World.CreateSystem<CitizenMovementSystem>(),
                World.CreateSystem<HaulCompletionSystem>(),
                World.CreateSystem<BuildingProductionSystem>(),
                World.CreateSystem<CitizenHaulSystem>(),
                World.CreateSystem<DeathSystem>(),
                World.CreateSystem<DebugVizSystem>(),
                World.CreateSystem<SimulationStatsSystem>(),
                World.CreateSystem<EventDispatchSystem>(),
                World.CreateSystem<LuaModSystem>(),
            };

            foreach (var handle in _tickSystems)
                _simGroup.AddSystemToUpdateList(handle);

            _bootstrapped = true;
            Debug.Log("[GameLoop] Simulation bootstrapped — ready to run.");
        }

        private void UpdateInfo()
        {
            var statsQuery = World.EntityManager.CreateEntityQuery(typeof(SimulationStats));
            if (!statsQuery.IsEmpty)
            {
                var stats = statsQuery.GetSingleton<SimulationStats>();
                _population = stats.Population;
            }
            statsQuery.Dispose();

            var calQuery = World.EntityManager.CreateEntityQuery(typeof(CalendarSingleton));
            if (!calQuery.IsEmpty)
            {
                _currentYear = calQuery.GetSingleton<CalendarSingleton>().Year;
            }
            calQuery.Dispose();
        }
    }
}