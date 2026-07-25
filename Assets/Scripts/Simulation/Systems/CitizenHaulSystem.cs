using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

namespace Groundwork.Simulation
{
    /// <summary>
    /// Assigns idle citizens to haul goods between buildings.
    /// Scans for surplus buildings (producers with stock) and deficit buildings
    /// (consumers with low stock), then dispatches the nearest idle citizen.
    /// Runs after BuildingProductionSystem, before PathfindingSystem.
    /// Part of ADR 2026-07-25 §2 — Citizen-Driven Goods Transport.
    /// </summary>
    public partial struct CitizenHaulSystem : ISystem
    {
        private const int SURPLUS_THRESHOLD = 10;  // building has surplus above this
        private const int DEFICIT_THRESHOLD = 10;   // building needs goods below this

        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Collect all buildings with positions and inventories
            var buildingPositions = new NativeHashMap<Entity, int2>(32, Allocator.Temp);

            foreach (var (bldg, pos, bEntity) in
                     SystemAPI.Query<RefRO<Building>, RefRO<MapPosition>>()
                         .WithAll<OutputSlot>()
                         .WithNone<UnderConstruction>()
                         .WithEntityAccess())
            {
                buildingPositions.TryAdd(bEntity, pos.ValueRO.TileCoordinate);
            }

            // Find idle citizens (no HaulTask, no PathRequest, not doing anything)
            foreach (var (task, citizen, cPos, entity) in
                     SystemAPI.Query<RefRW<CitizenTask>, RefRO<Citizen>, RefRO<MapPosition>>()
                         .WithNone<Dead>()
                         .WithNone<Child>()
                         .WithNone<HaulTask>()
                         .WithNone<PathRequest>()
                         .WithEntityAccess())
            {
                if (task.ValueRO.TaskType != "idle")
                    continue;

                // Don't assign haul jobs if citizen's own needs are critical
                var needs = state.EntityManager.GetBuffer<CitizenNeed>(entity);
                bool ownNeedsUrgent = false;
                for (int i = 0; i < needs.Length; i++)
                {
                    if (needs[i].Urgency > 0.6f)
                    {
                        ownNeedsUrgent = true;
                        break;
                    }
                }
                if (ownNeedsUrgent)
                    continue;

                // Find a haul job: surplus producer → deficit consumer
                if (TryFindHaulJob(ref state, buildingPositions, cPos.ValueRO.TileCoordinate,
                        out var sourceBuilding, out var destBuilding, out var itemId, out var quantity))
                {
                    // Assign the haul task
                    ecb.AddComponent(entity, new HaulTask
                    {
                        SourceBuilding = sourceBuilding,
                        DestinationBuilding = destBuilding,
                        ItemId = itemId,
                        Quantity = quantity,
                        Phase = 0,
                    });

                    // Issue path request to the source building
                    if (buildingPositions.TryGetValue(sourceBuilding, out var sourcePos))
                    {
                        ecb.AddComponent(entity, new PathRequest { Destination = sourcePos });
                    }

                    task.ValueRW.TaskType = "hauling";
                    task.ValueRW.TargetEntity = sourceBuilding;
                }
            }

            buildingPositions.Dispose();
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        /// <summary>
        /// Find a haul job: a building with surplus and a building with deficit of the same item.
        /// Picks the pair where the citizen is closest to the source.
        /// </summary>
        private static bool TryFindHaulJob(
            ref SystemState state,
            NativeHashMap<Entity, int2> buildingPositions,
            int2 citizenPos,
            out Entity sourceBuilding,
            out Entity destBuilding,
            out FixedString32Bytes itemId,
            out int quantity)
        {
            sourceBuilding = Entity.Null;
            destBuilding = Entity.Null;
            itemId = default;
            quantity = 0;

            float bestDist = float.MaxValue;

            foreach (var srcKvp in buildingPositions)
            {
                var srcBldg = srcKvp.Key;
                var srcPos = srcKvp.Value;

                // Check if this building has surplus of any item
                var srcInv = state.EntityManager.GetBuffer<OutputSlot>(srcBldg);
                for (int s = 0; s < srcInv.Length; s++)
                {
                    if (srcInv[s].Quantity <= SURPLUS_THRESHOLD)
                        continue;

                    var good = srcInv[s].ItemId;

                    // Find a consumer that needs this good
                    foreach (var dstKvp in buildingPositions)
                    {
                        var dstBldg = dstKvp.Key;

                        if (srcBldg == dstBldg)
                            continue;

                        var dstInv = state.EntityManager.GetBuffer<OutputSlot>(dstBldg);
                        for (int d = 0; d < dstInv.Length; d++)
                        {
                            if (dstInv[d].ItemId != good)
                                continue;
                            if (dstInv[d].Quantity > DEFICIT_THRESHOLD)
                                continue;

                            // Found a match! Pick the closest source
                            float dist = math.distancesq(citizenPos, srcPos);
                            if (dist < bestDist)
                            {
                                bestDist = dist;
                                sourceBuilding = srcBldg;
                                destBuilding = dstBldg;
                                itemId = good;
                                quantity = math.min(srcInv[s].Quantity - SURPLUS_THRESHOLD,
                                    DEFICIT_THRESHOLD - dstInv[d].Quantity + 1);
                                if (quantity < 1) quantity = 1;
                            }
                            break;
                        }
                    }
                }
            }

            return sourceBuilding != Entity.Null;
        }
    }
}