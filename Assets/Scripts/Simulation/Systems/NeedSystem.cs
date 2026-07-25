using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

namespace Groundwork.Simulation
{
    /// <summary>
    /// Updates citizen needs each game-day. Reads NeedDefinition components to determine
    /// satisfaction items, urgency growth rates, climate modifiers, and critical thresholds.
    /// Buildings are public resources — any building on the citizen's tile can be used.
    /// Runs after CalendarSystem, before DeathSystem.
    /// </summary>
    public partial struct NeedSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var config = SystemAPI.GetSingleton<SimulationConfig>();
            if (config.CurrentTick % config.TicksPerDay != 0)
                return;

            var calendar = SystemAPI.GetSingleton<CalendarSingleton>();
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Build need definition lookup: NeedType → NeedDefinition
            var needDefs = new NativeHashMap<FixedString32Bytes, NeedDefinition>(16, Allocator.Temp);
            foreach (var nd in SystemAPI.Query<RefRO<NeedDefinition>>())
            {
                needDefs.TryAdd(nd.ValueRO.NeedType, nd.ValueRO);
            }

            // Determine if we're in a cold season (fall=2, winter=3)
            bool isColdSeason = calendar.Season >= 2;

            // Build building position lookup for public building access
            var buildingPositions = new NativeHashMap<int2, Entity>(64, Allocator.Temp);
            foreach (var (bldg, bpos, bEntity) in
                     SystemAPI.Query<RefRO<Building>, RefRO<MapPosition>>()
                         .WithAll<OutputSlot>()
                         .WithNone<UnderConstruction>()
                         .WithEntityAccess())
            {
                buildingPositions.TryAdd(bpos.ValueRO.TileCoordinate, bEntity);
            }

            foreach (var (citizen, lb, position, entity) in
                     SystemAPI.Query<RefRW<Citizen>, RefRW<LivingBeing>, RefRO<MapPosition>>()
                         .WithNone<Dead>()
                         .WithEntityAccess())
            {
                var needs = state.EntityManager.GetBuffer<CitizenNeed>(entity);
                var personalInv = state.EntityManager.GetBuffer<InventorySlot>(entity);
                var citizenPos = position.ValueRO.TileCoordinate;

                // ─── Satisfy commodity needs (defined by NeedDefinition.SatisfyingItem) ───

                for (int i = 0; i < needs.Length; i++)
                {
                    var need = needs[i];

                    // Look up the definition for this need type
                    if (!needDefs.TryGetValue(need.NeedType, out var ndef))
                        continue;

                    // Only process commodity needs (those with a satisfying item)
                    if (ndef.SatisfyingItem.Length == 0)
                        continue;

                    // Skip if urgency below threshold for consumption
                    if (need.Urgency < 0.3f)
                        continue;

                    bool satisfied = false;

                    // 1. Try personal inventory
                    satisfied = TryConsumeFromInventory(personalInv, ndef.SatisfyingItem);

                    // 2. Try workplace inventory
                    if (!satisfied && citizen.ValueRO.WorkplaceBuilding != Entity.Null)
                    {
                        var workInv = state.EntityManager.GetBuffer<OutputSlot>(
                            citizen.ValueRO.WorkplaceBuilding);
                        satisfied = TryConsumeFromInventory(workInv, ndef.SatisfyingItem);
                    }

                    // 3. Try home inventory
                    if (!satisfied && citizen.ValueRO.HomeBuilding != Entity.Null)
                    {
                        var homeInv = state.EntityManager.GetBuffer<OutputSlot>(
                            citizen.ValueRO.HomeBuilding);
                        satisfied = TryConsumeFromInventory(homeInv, ndef.SatisfyingItem);
                    }

                    // 4. Try any building on this tile (public)
                    if (!satisfied)
                    {
                        satisfied = TryConsumeFromPublicBuilding(ref state,
                            buildingPositions, citizenPos, ndef.SatisfyingItem);
                    }

                    if (satisfied)
                    {
                        need.Urgency = math.max(0f, need.Urgency - ndef.SatisfactionReduction);
                        needs[i] = need;
                    }
                }

                // ─── Need growth (data-driven) ───

                for (int i = 0; i < needs.Length; i++)
                {
                    var need = needs[i];
                    if (!needDefs.TryGetValue(need.NeedType, out var ndef))
                        continue;

                    float growth = ndef.UrgencyGrowthPerDay;
                    if (isColdSeason)
                        growth *= ndef.ColdSeasonGrowthMultiplier;

                    need.Urgency = math.min(1f, need.Urgency + growth);
                    needs[i] = need;
                }

                // ─── Condition-based needs (triggered by runtime state, parameters from definitions) ───

                // Shelter need — triggered by homelessness
                if (needDefs.TryGetValue("shelter", out var shelterDef) &&
                    citizen.ValueRO.HomeBuilding == Entity.Null)
                    UpsertNeed(needs, "shelter", shelterDef.UrgencyGrowthPerDay);

                // Health need — triggered by low health
                if (needDefs.TryGetValue("health", out var healthDef) &&
                    lb.ValueRO.Health < 30f)
                    UpsertNeed(needs, "health", healthDef.UrgencyGrowthPerDay);

                // ─── Health decay (data-driven critical threshold) ───

                for (int i = 0; i < needs.Length; i++)
                {
                    var need = needs[i];
                    if (!needDefs.TryGetValue(need.NeedType, out var ndef))
                        continue;

                    if (need.Urgency > ndef.CriticalThreshold)
                    {
                        float decay = (need.Urgency - ndef.CriticalThreshold) * ndef.HealthDecayRate;
                        lb.ValueRW.Health -= decay;
                        if (lb.ValueRW.Health < 0f)
                            lb.ValueRW.Health = 0f;
                    }
                }

                // Death by health
                if (lb.ValueRO.Health <= 0f && !SystemAPI.HasComponent<Dead>(entity))
                    ecb.AddComponent<Dead>(entity);
            }

            needDefs.Dispose();
            buildingPositions.Dispose();
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        /// <summary>Try to consume one unit of an item from a DynamicBuffer inventory. Returns true if consumed.</summary>
        private static bool TryConsumeFromInventory(DynamicBuffer<OutputSlot> inventory, FixedString32Bytes itemId)
        {
            for (int j = 0; j < inventory.Length; j++)
            {
                if (inventory[j].ItemId != itemId || inventory[j].Quantity <= 0)
                    continue;
                var slot = inventory[j];
                slot.Quantity -= 1;
                inventory[j] = slot;
                return true;
            }
            return false;
        }

        private static bool TryConsumeFromInventory(DynamicBuffer<InventorySlot> inventory, FixedString32Bytes itemId)
        {
            for (int j = 0; j < inventory.Length; j++)
            {
                if (inventory[j].ItemId != itemId || inventory[j].Quantity <= 0)
                    continue;
                var slot = inventory[j];
                slot.Quantity -= 1;
                inventory[j] = slot;
                return true;
            }
            return false;
        }

        /// <summary>Try to consume one unit from any public building on the tile.</summary>
        private static bool TryConsumeFromPublicBuilding(
            ref SystemState state,
            NativeHashMap<int2, Entity> buildingPositions,
            int2 citizenPos,
            FixedString32Bytes itemId)
        {
            if (buildingPositions.TryGetValue(citizenPos, out var building))
            {
                var inv = state.EntityManager.GetBuffer<OutputSlot>(building);
                return TryConsumeFromInventory(inv, itemId);
            }
            return false;
        }

        /// <summary>Add or update a need in the citizen's needs buffer.</summary>
        private static void UpsertNeed(DynamicBuffer<CitizenNeed> needs, FixedString32Bytes needType, float urgencyIncrease)
        {
            for (int i = 0; i < needs.Length; i++)
            {
                if (needs[i].NeedType == needType)
                {
                    var need = needs[i];
                    need.Urgency = math.min(1f, need.Urgency + urgencyIncrease);
                    needs[i] = need;
                    return;
                }
            }
            needs.Add(new CitizenNeed { NeedType = needType, Urgency = urgencyIncrease });
        }
    }
}