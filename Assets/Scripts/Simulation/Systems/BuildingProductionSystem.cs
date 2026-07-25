using Unity.Entities;
using Unity.Collections;

namespace Groundwork.Simulation
{
    /// <summary>
    /// Processes production orders in buildings. Consumes inputs from building inventory,
    /// advances recipe progress, and outputs products when complete.
    /// All recipe logic comes from RecipeDefinitionData entities — no hardcoded recipes.
    /// Worker requirements come from BuildingDefinitionData.
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
                ComponentType.ReadWrite<InventorySlot>(),
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

            // Get event buffer for production events
            if (!SystemAPI.TryGetSingletonEntity<SimulationEventSingleton>(out var eventEntity))
                return;
            var eventBuffer = state.EntityManager.GetBuffer<SimulationEvent>(eventEntity);

            // Build recipe lookup: recipe ID → entity
            var recipeEntities = _recipeQuery.ToEntityArray(Allocator.Temp);
            var recipeDefs = _recipeQuery.ToComponentDataArray<RecipeDefinitionData>(Allocator.Temp);
            var recipeLookup = new NativeHashMap<FixedString32Bytes, Entity>(
                recipeDefs.Length, Allocator.Temp);
            for (int i = 0; i < recipeDefs.Length; i++)
                recipeLookup.TryAdd(recipeDefs[i].RecipeId, recipeEntities[i]);

            // Build building definition lookup: building type → BuildingDefinitionData
            var bDefEntities = _buildingDefQuery.ToEntityArray(Allocator.Temp);
            var bDefs = _buildingDefQuery.ToComponentDataArray<BuildingDefinitionData>(Allocator.Temp);
            var bDefLookup = new NativeHashMap<FixedString32Bytes, BuildingDefinitionData>(
                bDefs.Length, Allocator.Temp);
            for (int i = 0; i < bDefs.Length; i++)
                bDefLookup.TryAdd(bDefs[i].BuildingType, bDefs[i]);

            // Count workers per building
            var workerCounts = new NativeHashMap<Entity, int>(
                entities.Length, Allocator.Temp);
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

                // Worker check: uses building instance's MaxWorkers, definition's RequiresWorkers
                if (bDefLookup.TryGetValue(building.BuildingType, out var bDef))
                {
                    if (bDef.RequiresWorkers && building.MaxWorkers > 0)
                    {
                        if (!workerCounts.TryGetValue(entities[i], out int count) || count == 0)
                            continue;
                    }
                }

                var inventory = state.EntityManager.GetBuffer<InventorySlot>(entities[i]);
                var productionQueue = state.EntityManager.GetBuffer<ProductionOrder>(entities[i]);

                for (int j = 0; j < productionQueue.Length; j++)
                {
                    var order = productionQueue[j];

                    // Look up recipe definition
                    if (!recipeLookup.TryGetValue(order.RecipeId, out var recipeEntity))
                        continue;

                    var recipe = state.EntityManager.GetComponentData<RecipeDefinitionData>(recipeEntity);
                    var recipeInputs = state.EntityManager.GetBuffer<RecipeInput>(recipeEntity);
                    var recipeOutputs = state.EntityManager.GetBuffer<RecipeOutput>(recipeEntity);

                    // Consume inputs
                    bool allInputsAvailable = true;
                    for (int k = 0; k < recipeInputs.Length; k++)
                    {
                        int needed = recipeInputs[k].Quantity;
                        int found = 0;
                        for (int m = 0; m < inventory.Length; m++)
                        {
                            if (inventory[m].ItemId == recipeInputs[k].ItemId)
                                found += inventory[m].Quantity;
                        }
                        if (found < needed)
                        {
                            allInputsAvailable = false;
                            break;
                        }
                    }

                    if (!allInputsAvailable)
                        continue;

                    // Consume one unit of each input per tick
                    for (int k = 0; k < recipeInputs.Length; k++)
                    {
                        for (int m = 0; m < inventory.Length; m++)
                        {
                            if (inventory[m].ItemId == recipeInputs[k].ItemId && inventory[m].Quantity > 0)
                            {
                                var slot = inventory[m];
                                slot.Quantity -= 1;
                                inventory[m] = slot;
                                break;
                            }
                        }
                    }

                    // Advance progress
                    float progressPerTick = 1f / recipe.TicksPerCycle;
                    order.Progress += progressPerTick;

                    if (order.Progress >= 1f)
                    {
                        // Produce outputs
                        for (int k = 0; k < recipeOutputs.Length; k++)
                            AddToInventory(inventory, recipeOutputs[k].ItemId, recipeOutputs[k].Quantity);

                        // Emit production complete event
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

        private static void AddToInventory(DynamicBuffer<InventorySlot> inventory, FixedString32Bytes itemId, int quantity)
        {
            for (int i = 0; i < inventory.Length; i++)
            {
                if (inventory[i].ItemId == itemId)
                {
                    var slot = inventory[i];
                    slot.Quantity += quantity;
                    inventory[i] = slot;
                    return;
                }
            }
            inventory.Add(new InventorySlot { ItemId = itemId, Quantity = quantity });
        }
    }
}