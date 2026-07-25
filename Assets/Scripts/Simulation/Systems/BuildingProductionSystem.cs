using Unity.Entities;
using Unity.Collections;

namespace Groundwork.Simulation
{
    /// <summary>
    /// Processes production orders in buildings. Consumes inputs from InventorySlot
    /// (input inventory), advances recipe progress, and deposits outputs into OutputSlot
    /// (output inventory). Input/Output separation prevents haulers from stealing
    /// raw materials before processing.
    /// Per ADR 2026-07-25 — Building Abstraction & Production Archetypes.
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
            var buildings = _buildingQuery.ToComponentDataArray<Building>(Allocator.Temp);
            var entities = _buildingQuery.ToEntityArray(Allocator.Temp);

            if (!SystemAPI.TryGetSingletonEntity<SimulationEventSingleton>(out var eventEntity))
                return;
            var eventBuffer = state.EntityManager.GetBuffer<SimulationEvent>(eventEntity);

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

            var workerCounts = new NativeHashMap<Entity, int>(entities.Length, Allocator.Temp);
            var citizens = _citizenQuery.ToComponentDataArray<Citizen>(Allocator.Temp);
            for (int i = 0; i < citizens.Length; i++)
            {
                if (citizens[i].WorkplaceBuilding != Entity.Null)
                {
                    if (workerCounts.TryGetValue(citizens[i].WorkplaceBuilding, out int c))
                        workerCounts[citizens[i].WorkplaceBuilding] = c + 1;
                    else
                        workerCounts.Add(citizens[i].WorkplaceBuilding, 1);
                }
            }
            citizens.Dispose();

            for (int i = 0; i < entities.Length; i++)
            {
                var building = buildings[i];
                if (!building.IsOperational)
                    continue;

                if (bDefLookup.TryGetValue(building.BuildingType, out var bDef))
                {
                    if (bDef.RequiresWorkers && building.MaxWorkers > 0)
                    {
                        if (!workerCounts.TryGetValue(entities[i], out int count) || count == 0)
                            continue;
                    }
                }

                // Input inventory: only exists on buildings that consume goods (woodcutter has logs)
                // Output inventory: exists on all production buildings (firewood, food)
                var outputInv = state.EntityManager.GetBuffer<OutputSlot>(entities[i]);
                var hasInputInv = state.EntityManager.HasBuffer<InventorySlot>(entities[i]);
                DynamicBuffer<InventorySlot> inputInv = default;
                if (hasInputInv)
                    inputInv = state.EntityManager.GetBuffer<InventorySlot>(entities[i]);

                var productionQueue = state.EntityManager.GetBuffer<ProductionOrder>(entities[i]);

                for (int j = 0; j < productionQueue.Length; j++)
                {
                    var order = productionQueue[j];

                    if (!recipeLookup.TryGetValue(order.RecipeId, out var recipeEntity))
                        continue;

                    var recipe = state.EntityManager.GetComponentData<RecipeDefinitionData>(recipeEntity);
                    var recipeInputs = state.EntityManager.GetBuffer<RecipeInput>(recipeEntity);
                    var recipeOutputs = state.EntityManager.GetBuffer<RecipeOutput>(recipeEntity);

                    // Consume inputs from input inventory
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
                        if (bDefLookup.TryGetValue(building.BuildingType, out var bDefCap)
                            && bDefCap.OutputCapacity > 0
                            && CountItems(outputInv) >= bDefCap.OutputCapacity)
                        {
                            // Output full — don't complete, keep progress
                            continue;
                        }

                        // Deposit outputs into output inventory
                        for (int k = 0; k < recipeOutputs.Length; k++)
                            AddToOutput(outputInv, recipeOutputs[k].ItemId, recipeOutputs[k].Quantity);

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

            recipeEntities.Dispose();
            recipeDefs.Dispose();
            recipeLookup.Dispose();
            bDefEntities.Dispose();
            bDefs.Dispose();
            bDefLookup.Dispose();
            workerCounts.Dispose();
            buildings.Dispose();
            entities.Dispose();
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