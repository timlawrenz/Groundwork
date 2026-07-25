using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

namespace Groundwork.Simulation
{
    /// <summary>
    /// Processes production orders in buildings. Consumes inputs from InventorySlot
    /// (input inventory), advances recipe progress, and deposits outputs into OutputSlot
    /// (output inventory). Input/Output separation prevents haulers from stealing
    /// raw materials before processing.
    /// 
    /// Archetype-aware per ADR 2026-07-25 — Building Abstraction & Production Archetypes:
    ///   Workshop  — worker at building tile, input→output transform
    ///   Gathering — worker in zone, harvests with overlap penalty
    ///   Source    — no worker, replenishes each tick
    ///   Service   — no inventory, need relief only (handled by NeedSystem)
    /// </summary>
    public partial struct BuildingProductionSystem : ISystem
    {
        private EntityQuery _buildingQuery;
        private EntityQuery _citizenQuery;
        private EntityQuery _recipeQuery;
        private EntityQuery _buildingDefQuery;

        public void OnCreate(ref SystemState state)
        {
            _buildingQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<Building>(),
                ComponentType.ReadWrite<OutputSlot>(),
                ComponentType.ReadWrite<ProductionOrder>(),
                ComponentType.Exclude<UnderConstruction>());

            _citizenQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<Citizen>(),
                ComponentType.ReadOnly<MapPosition>(),
                ComponentType.Exclude<Dead>());

            _recipeQuery = state.GetEntityQuery(
                typeof(RecipeDefinitionData),
                typeof(RecipeInput),
                typeof(RecipeOutput));

            _buildingDefQuery = state.GetEntityQuery(
                typeof(BuildingDefinitionData),
                typeof(BuildingRecipe));
        }

        public void OnUpdate(ref SystemState state)
        {
            var config = SystemAPI.GetSingleton<SimulationConfig>();
            var buildings = _buildingQuery.ToComponentDataArray<Building>(Allocator.Temp);
            var entities = _buildingQuery.ToEntityArray(Allocator.Temp);

            if (!SystemAPI.TryGetSingletonEntity<SimulationEventSingleton>(out var eventEntity))
            {
                buildings.Dispose();
                entities.Dispose();
                return;
            }
            var eventBuffer = state.EntityManager.GetBuffer<SimulationEvent>(eventEntity);

            // ─── Lookup tables ───

            var recipeEntities = _recipeQuery.ToEntityArray(Allocator.Temp);
            var recipeDefs = _recipeQuery.ToComponentDataArray<RecipeDefinitionData>(Allocator.Temp);
            var recipeLookup = new NativeHashMap<FixedString32Bytes, Entity>(
                recipeDefs.Length, Allocator.Temp);
            for (int i = 0; i < recipeDefs.Length; i++)
                recipeLookup.TryAdd(recipeDefs[i].RecipeId, recipeEntities[i]);

            var bDefEntities = _buildingDefQuery.ToEntityArray(Allocator.Temp);
            var bDefs = _buildingDefQuery.ToComponentDataArray<BuildingDefinitionData>(Allocator.Temp);
            var bDefLookup = new NativeHashMap<FixedString32Bytes, BuildingDefinitionData>(
                bDefs.Length, Allocator.Temp);
            for (int i = 0; i < bDefs.Length; i++)
                bDefLookup.TryAdd(bDefs[i].BuildingType, bDefs[i]);

            // ─── Worker presence maps (for Workshop and Gathering) ───

            // workerCounts: how many citizens assigned to each building
            var workerCounts = new NativeHashMap<Entity, int>(entities.Length, Allocator.Temp);
            // workersAtTile: whether at least one assigned worker is at the building tile (Workshop)
            var workersAtTile = new NativeHashMap<Entity, bool>(entities.Length, Allocator.Temp);
            // workersInZone: whether at least one assigned worker is within zone (Gathering)
            var workersInZone = new NativeHashMap<Entity, bool>(entities.Length, Allocator.Temp);
            // building positions for overlap/zone checks
            var buildingPositions = new NativeHashMap<Entity, int2>(entities.Length, Allocator.Temp);

            // Collect building positions
            for (int i = 0; i < entities.Length; i++)
            {
                if (state.EntityManager.HasComponent<MapPosition>(entities[i]))
                {
                    var pos = state.EntityManager.GetComponentData<MapPosition>(entities[i]);
                    buildingPositions.TryAdd(entities[i], pos.TileCoordinate);
                }
            }

            // Collect citizen work assignments and positions
            var citizens = _citizenQuery.ToComponentDataArray<Citizen>(Allocator.Temp);
            var citizenPositions = _citizenQuery.ToComponentDataArray<MapPosition>(Allocator.Temp);
            for (int i = 0; i < citizens.Length; i++)
            {
                if (citizens[i].WorkplaceBuilding == Entity.Null)
                    continue;

                var wp = citizens[i].WorkplaceBuilding;
                if (workerCounts.TryGetValue(wp, out int c))
                    workerCounts[wp] = c + 1;
                else
                    workerCounts.Add(wp, 1);

                var citizenPos = citizenPositions[i].TileCoordinate;

                // Check if worker is at building tile (Workshop)
                if (buildingPositions.TryGetValue(wp, out var bp) &&
                    math.all(citizenPos == bp))
                    workersAtTile[wp] = true;

                // Check if worker is within gathering zone
                if (state.EntityManager.HasComponent<GatheringZone>(wp))
                {
                    var zone = state.EntityManager.GetComponentData<GatheringZone>(wp);
                    int dx = math.abs(citizenPos.x - bp.x);
                    int dy = math.abs(citizenPos.y - bp.y);
                    if (dx <= zone.Radius && dy <= zone.Radius)
                        workersInZone[wp] = true;
                }
            }
            citizens.Dispose();
            citizenPositions.Dispose();

            // ─── Compute overlap penalties for Gathering buildings ───

            var overlapPenalty = new NativeHashMap<Entity, float>(entities.Length, Allocator.Temp);
            ComputeGatheringOverlaps(ref state, entities, buildingPositions, overlapPenalty);

            // ─── Process each building ───

            for (int i = 0; i < entities.Length; i++)
            {
                var building = buildings[i];
                if (!building.IsOperational)
                    continue;

                if (!bDefLookup.TryGetValue(building.BuildingType, out var bDef))
                {
                    // Unknown building type — fallback to legacy behavior:
                    // check workers by count only, no archetype-specific logic
                    if (building.MaxWorkers > 0)
                    {
                        if (!workerCounts.TryGetValue(entities[i], out int count) || count == 0)
                            continue;
                    }
                    bDef = default; // continue with default capacities
                }

                var archetype = bDef.Archetype;

                // ─── Worker presence checks per archetype ───

                if (archetype == ProductionArchetype.Workshop)
                {
                    // Workshop: need at least one worker at the building tile
                    if (bDef.RequiresWorkers && building.MaxWorkers > 0)
                    {
                        if (!workersAtTile.TryGetValue(entities[i], out bool atTile) || !atTile)
                            continue;
                    }
                }
                else if (archetype == ProductionArchetype.Gathering)
                {
                    // Gathering: need at least one worker within zone
                    if (bDef.RequiresWorkers && building.MaxWorkers > 0)
                    {
                        if (!workersInZone.TryGetValue(entities[i], out bool inZone) || !inZone)
                            continue;

                        // Also check worker count (fallback for definitions without zone data)
                        if (!workerCounts.TryGetValue(entities[i], out int count) || count == 0)
                            continue;
                    }
                }
                else
                {
                    // Source, Service, or unspecified: use original worker-count check
                    if (bDef.RequiresWorkers && building.MaxWorkers > 0)
                    {
                        if (!workerCounts.TryGetValue(entities[i], out int count) || count == 0)
                            continue;
                    }
                }

                // ─── Inventories ───

                var outputInv = state.EntityManager.GetBuffer<OutputSlot>(entities[i]);
                var hasInputInv = state.EntityManager.HasBuffer<InventorySlot>(entities[i]);
                DynamicBuffer<InventorySlot> inputInv = default;
                if (hasInputInv)
                    inputInv = state.EntityManager.GetBuffer<InventorySlot>(entities[i]);

                var productionQueue = state.EntityManager.GetBuffer<ProductionOrder>(entities[i]);

                // ─── Process production orders ───

                for (int j = 0; j < productionQueue.Length; j++)
                {
                    var order = productionQueue[j];

                    if (!recipeLookup.TryGetValue(order.RecipeId, out var recipeEntity))
                        continue;

                    var recipe = state.EntityManager.GetComponentData<RecipeDefinitionData>(recipeEntity);
                    var recipeInputs = state.EntityManager.GetBuffer<RecipeInput>(recipeEntity);
                    var recipeOutputs = state.EntityManager.GetBuffer<RecipeOutput>(recipeEntity);

                    // Consume inputs from input inventory (Workshop only)
                    bool allInputsAvailable = true;
                    if (recipeInputs.Length > 0 && hasInputInv)
                    {
                        for (int k = 0; k < recipeInputs.Length; k++)
                        {
                            int needed = recipeInputs[k].Quantity;
                            int found = 0;
                            for (int m = 0; m < inputInv.Length; m++)
                            {
                                if (inputInv[m].ItemId == recipeInputs[k].ItemId)
                                    found += inputInv[m].Quantity;
                            }
                            if (found < needed)
                            {
                                allInputsAvailable = false;
                                break;
                            }
                        }
                    }

                    if (!allInputsAvailable)
                        continue;

                    // Consume one unit of each input per tick
                    if (recipeInputs.Length > 0 && hasInputInv)
                    {
                        for (int k = 0; k < recipeInputs.Length; k++)
                        {
                            for (int m = 0; m < inputInv.Length; m++)
                            {
                                if (inputInv[m].ItemId == recipeInputs[k].ItemId && inputInv[m].Quantity > 0)
                                {
                                    var slot = inputInv[m];
                                    slot.Quantity -= 1;
                                    inputInv[m] = slot;
                                    break;
                                }
                            }
                        }
                    }

                    // Advance progress
                    float progressPerTick = 1f / recipe.TicksPerCycle;
                    order.Progress += progressPerTick;

                    if (order.Progress >= 1f)
                    {
                        // Check output capacity — stall if full
                        if (bDef.OutputCapacity > 0
                            && CountItems(outputInv) >= bDef.OutputCapacity)
                        {
                            continue;
                        }

                        // Apply gathering overlap penalty probabilistically
                        float outputMultiplier = 1.0f;
                        if (archetype == ProductionArchetype.Gathering)
                        {
                            if (overlapPenalty.TryGetValue(entities[i], out float penalty))
                                outputMultiplier = penalty;
                        }

                        // Deposit outputs into output inventory
                        for (int k = 0; k < recipeOutputs.Length; k++)
                        {
                            int qty = recipeOutputs[k].Quantity;
                            if (outputMultiplier < 1.0f)
                            {
                                // Apply probabilistic penalty: fractional yield
                                float adjusted = qty * outputMultiplier;
                                qty = (int)math.floor(adjusted);
                                // Carry fractional remainder: add extra item with probability = fraction
                                float fractional = adjusted - qty;
                                var random = Unity.Mathematics.Random.CreateFromIndex(
                                    (uint)(config.CurrentTick * 1000 + entities[i].Index * 100 + j + (int)(outputMultiplier * 10000)));
                                if (random.NextFloat() < fractional)
                                    qty += 1;
                                // No minimum guarantee — allows penalty to reduce output to 0
                            }
                            AddToOutput(outputInv, recipeOutputs[k].ItemId, qty);
                        }

                        eventBuffer.Add(new SimulationEvent
                        {
                            Type = EventType.ProductionComplete,
                            EntityId = entities[i].Index,
                        });

                        order.Progress = 0f;
                    }

                    productionQueue[j] = order;
                }
            }

            // ─── Cleanup ───

            recipeEntities.Dispose();
            recipeDefs.Dispose();
            recipeLookup.Dispose();
            bDefEntities.Dispose();
            bDefs.Dispose();
            bDefLookup.Dispose();
            workerCounts.Dispose();
            workersAtTile.Dispose();
            workersInZone.Dispose();
            buildingPositions.Dispose();
            overlapPenalty.Dispose();
            buildings.Dispose();
            entities.Dispose();
        }

        /// <summary>
        /// Compute overlap penalty for each Gathering building.
        /// For each building, penalty = 1.0 / (1.0 + sum_overlap_ratios).
        /// Overlap ratio between two buildings = shared_tiles / zone_tiles.
        /// </summary>
        private static void ComputeGatheringOverlaps(
            ref SystemState state,
            NativeArray<Entity> entities,
            NativeHashMap<Entity, int2> buildingPositions,
            NativeHashMap<Entity, float> overlapPenalty)
        {
            for (int i = 0; i < entities.Length; i++)
            {
                if (!state.EntityManager.HasComponent<GatheringZone>(entities[i]))
                    continue;

                var zoneA = state.EntityManager.GetComponentData<GatheringZone>(entities[i]);
                if (!buildingPositions.TryGetValue(entities[i], out var posA))
                    continue;

                int zoneTiles = (2 * zoneA.Radius + 1) * (2 * zoneA.Radius + 1);
                float totalOverlapRatio = 0f;

                for (int j = 0; j < entities.Length; j++)
                {
                    if (i == j)
                        continue;
                    if (!state.EntityManager.HasComponent<GatheringZone>(entities[j]))
                        continue;

                    var zoneB = state.EntityManager.GetComponentData<GatheringZone>(entities[j]);
                    if (!buildingPositions.TryGetValue(entities[j], out var posB))
                        continue;

                    // Compute intersection area of the two square zones
                    int sharedTiles = ComputeZoneIntersection(
                        posA, zoneA.Radius, posB, zoneB.Radius);
                    totalOverlapRatio += (float)sharedTiles / zoneTiles;
                }

                float penalty = 1.0f / (1.0f + totalOverlapRatio);
                overlapPenalty.TryAdd(entities[i], penalty);
            }
        }

        /// <summary>
        /// Compute the number of tiles shared between two square zones.
        /// Each zone is a square of side (2*radius + 1) centered at (pos.x, pos.y).
        /// </summary>
        private static int ComputeZoneIntersection(
            int2 centerA, int radiusA, int2 centerB, int radiusB)
        {
            int ax1 = centerA.x - radiusA;
            int ax2 = centerA.x + radiusA;
            int ay1 = centerA.y - radiusA;
            int ay2 = centerA.y + radiusA;

            int bx1 = centerB.x - radiusB;
            int bx2 = centerB.x + radiusB;
            int by1 = centerB.y - radiusB;
            int by2 = centerB.y + radiusB;

            int ix1 = math.max(ax1, bx1);
            int ix2 = math.min(ax2, bx2);
            int iy1 = math.max(ay1, by1);
            int iy2 = math.min(ay2, by2);

            if (ix1 > ix2 || iy1 > iy2)
                return 0;

            return (ix2 - ix1 + 1) * (iy2 - iy1 + 1);
        }

        private static void AddToOutput(DynamicBuffer<OutputSlot> outputInv, FixedString32Bytes itemId, int quantity)
        {
            for (int i = 0; i < outputInv.Length; i++)
            {
                if (outputInv[i].ItemId == itemId)
                {
                    var slot = outputInv[i];
                    slot.Quantity += quantity;
                    outputInv[i] = slot;
                    return;
                }
            }
            outputInv.Add(new OutputSlot { ItemId = itemId, Quantity = quantity });
        }

        private static int CountItems(DynamicBuffer<OutputSlot> inventory)
        {
            int total = 0;
            for (int i = 0; i < inventory.Length; i++)
                total += inventory[i].Quantity;
            return total;
        }
    }
}