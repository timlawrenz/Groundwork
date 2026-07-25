using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

namespace Groundwork.Simulation
{
    /// <summary>
    /// Creates new citizen entities (children) for eligible adult females.
    /// Runs after CalendarSystem, before CitizenAgeSystem.
    /// Eligibility: female, age 16-50, health &gt; 50, has home, hasn't given birth this year.
    /// </summary>
    public partial struct BirthSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var config = SystemAPI.GetSingleton<SimulationConfig>();
            if (config.CurrentTick % config.TicksPerDay != 0)
                return;

            var calendar = SystemAPI.GetSingleton<CalendarSingleton>();
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Get event buffer for emitting birth events
            if (!SystemAPI.TryGetSingletonEntity<SimulationEventSingleton>(out var eventEntity))
                return;
            var eventBuffer = state.EntityManager.GetBuffer<SimulationEvent>(eventEntity);

            foreach (var (citizen, lb, position, entity) in
                     SystemAPI.Query<RefRW<Citizen>, RefRO<LivingBeing>, RefRO<MapPosition>>()
                         .WithNone<Dead, Child>()
                         .WithEntityAccess())
            {
                var c = citizen.ValueRO;

                // Eligibility checks
                if (lb.ValueRO.Sex != 1) continue;                    // must be female
                if (lb.ValueRO.Age < 16f || lb.ValueRO.Age > 50f) continue;   // reproductive age
                if (lb.ValueRO.Health < 50f) continue;                // must be healthy
                if (c.HomeBuilding == Entity.Null) continue; // must have a home
                if (c.LastBirthYear >= calendar.Year) continue; // already had child this year

                // Create child entity
                var child = ecb.CreateEntity();
                ecb.AddComponent(child, new LivingBeing
                {
                    Age = 0f,
                    Sex = (byte)(new Unity.Mathematics.Random((uint)(entity.Index + calendar.Year * 1000)).NextInt(0, 2)),
                    Health = 100f,
                    Happiness = 50f,
                });
                ecb.AddComponent(child, new Citizen
                {
                    Name = $"Child of {c.Name}",
                    EducationLevel = 0,
                    HomeBuilding = c.HomeBuilding,
                    WorkplaceBuilding = Entity.Null,
                    LastBirthYear = 0,
                });
                ecb.AddComponent(child, new MapPosition
                {
                    TileCoordinate = position.ValueRO.TileCoordinate,
                    Rotation = 0,
                });
                ecb.AddComponent(child, new CitizenTask
                {
                    TaskType = "idle",
                    TargetEntity = Entity.Null,
                    Progress = 0f,
                });
                ecb.AddComponent<Child>(child);
                ecb.AddBuffer<CitizenNeed>(child);
                ecb.AddBuffer<InventorySlot>(child);
                ecb.AddBuffer<PathFollowing>(child);

                // Update mother's LastBirthYear
                citizen.ValueRW.LastBirthYear = calendar.Year;

                // Emit birth event
                eventBuffer.Add(new SimulationEvent
                {
                    Type = EventType.CitizenBorn,
                    EntityId = child.Index,
                    Data0 = position.ValueRO.TileCoordinate.x,
                    Data1 = position.ValueRO.TileCoordinate.y,
                });
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}