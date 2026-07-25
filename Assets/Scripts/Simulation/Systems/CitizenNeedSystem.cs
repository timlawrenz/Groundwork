using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

namespace Groundwork.Simulation
{
    /// <summary>
    /// Updates citizen needs each game-day. Decays health when critical needs go unmet.
    /// Needs are satisfied by consuming resources from inventory (e.g. eating food).
    /// Buildings are public resources — any building on the citizen's tile can be used.
    /// Runs after CalendarSystem, before DeathSystem.
    /// </summary>
    public partial struct CitizenNeedSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var config = SystemAPI.GetSingleton<SimulationConfig>();
            if (config.CurrentTick % config.TicksPerDay != 0)
                return;

            var calendar = SystemAPI.GetSingleton<CalendarSingleton>();
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Build building position lookup for public building access
            var buildingPositions = new NativeHashMap<int2, Entity>(64, Allocator.Temp);
            foreach (var (bldg, bpos, bEntity) in
                     SystemAPI.Query<RefRO<Building>, RefRO<MapPosition>>()
                         .WithAll<InventorySlot>()
                         .WithNone<UnderConstruction>()
                         .WithEntityAccess())
            {
                buildingPositions.Add(bpos.ValueRO.TileCoordinate, bEntity);
            }

            foreach (var (citizen, position, entity) in
                     SystemAPI.Query<RefRW<Citizen>, RefRO<MapPosition>>()
                         .WithNone<Dead>()
                         .WithEntityAccess())
            {
                var needs = state.EntityManager.GetBuffer<CitizenNeed>(entity);
                var inventory = state.EntityManager.GetBuffer<InventorySlot>(entity);
                var citizenPos = position.ValueRO.TileCoordinate;

                // ─── Consume food from inventory to reduce food need ───
                bool ateFromPersonal = false;
                bool ateFromWorkplace = false;
                bool ateFromHome = false;

                for (int i = 0; i < needs.Length; i++)
                {
                    if (needs[i].NeedType != "food")
                        continue;
                    if (needs[i].Urgency < 0.3f)
                        break; // not hungry enough to eat

                    // 1. Try personal inventory first
                    for (int j = 0; j < inventory.Length; j++)
                    {
                        if (inventory[j].ItemId != "food" || inventory[j].Quantity <= 0)
                            continue;

                        var slot = inventory[j];
                        slot.Quantity -= 1;
                        inventory[j] = slot;
                        ateFromPersonal = true;
                        break;
                    }

                    // 2. Fall back to workplace inventory
                    if (!ateFromPersonal && citizen.ValueRO.WorkplaceBuilding != Entity.Null)
                    {
                        var workInv = state.EntityManager.GetBuffer<InventorySlot>(
                            citizen.ValueRO.WorkplaceBuilding);
                        for (int j = 0; j < workInv.Length; j++)
                        {
                            if (workInv[j].ItemId != "food" || workInv[j].Quantity <= 0)
                                continue;

                            var slot = workInv[j];
                            slot.Quantity -= 1;
                            workInv[j] = slot;
                            ateFromWorkplace = true;
                            break;
                        }
                    }

                    // 3. Fall back to home building inventory
                    if (!ateFromPersonal && !ateFromWorkplace && citizen.ValueRO.HomeBuilding != Entity.Null)
                    {
                        var homeInv = state.EntityManager.GetBuffer<InventorySlot>(
                            citizen.ValueRO.HomeBuilding);
                        for (int j = 0; j < homeInv.Length; j++)
                        {
                            if (homeInv[j].ItemId != "food" || homeInv[j].Quantity <= 0)
                                continue;

                            var slot = homeInv[j];
                            slot.Quantity -= 1;
                            homeInv[j] = slot;
                            ateFromHome = true;
                            break;
                        }
                    }

                    // 4. Fall back to any building on this tile (public)
                    if (!ateFromPersonal && !ateFromWorkplace && !ateFromHome)
                    {
                        if (TryConsumeFromPublicBuilding(ref state, buildingPositions, citizenPos, "food"))
                            ateFromHome = true; // semantically "ate from a building"
                    }

                    // Reduce urgency if we ate anything
                    if (ateFromPersonal || ateFromWorkplace || ateFromHome)
                    {
                        var need = needs[i];
                        need.Urgency = math.max(0f, need.Urgency - 0.5f);
                        needs[i] = need;
                    }
                    break; // only process food need once per citizen per day
                }

                // ─── Consume firewood for warmth ───
                for (int i = 0; i < needs.Length; i++)
                {
                    if (needs[i].NeedType != "warmth")
                        continue;
                    if (needs[i].Urgency < 0.3f)
                        break;

                    bool warmedUp = false;

                    // 1. Try home building
                    if (citizen.ValueRO.HomeBuilding != Entity.Null)
                    {
                        warmedUp = TryConsumeFromBuilding(ref state,
                            citizen.ValueRO.HomeBuilding, "firewood");
                    }

                    // 2. Try any building on this tile (public)
                    if (!warmedUp)
                    {
                        warmedUp = TryConsumeFromPublicBuilding(ref state,
                            buildingPositions, citizenPos, "firewood", citizen.ValueRO.HomeBuilding);
                    }

                    if (warmedUp)
                    {
                        var need = needs[i];
                        need.Urgency = math.max(0f, need.Urgency - 0.5f);
                        needs[i] = need;
                    }
                    break;
                }

                // ─── Need growth ───
                UpsertNeed(needs, "food", 0.15f);

                if (calendar.Season >= 2)
                    UpsertNeed(needs, "warmth", 0.1f);
                else
                    UpsertNeed(needs, "warmth", 0.01f);

                if (citizen.ValueRO.HomeBuilding == Entity.Null)
                    UpsertNeed(needs, "shelter", 0.25f);

                if (citizen.ValueRO.Health < 30f)
                    UpsertNeed(needs, "health", 0.05f);

                // Social need reserved for future DLC

                // Apply critical needs → health decay
                for (int i = 0; i < needs.Length; i++)
                {
                    var need = needs[i];
                    if (need.Urgency > 0.8f)
                    {
                        float decay = (need.Urgency - 0.8f) * 2f;
                        citizen.ValueRW.Health -= decay;
                        if (citizen.ValueRW.Health < 0f)
                            citizen.ValueRW.Health = 0f;
                    }
                }

                // Death by health
                if (citizen.ValueRO.Health <= 0f && !SystemAPI.HasComponent<Dead>(entity))
                    ecb.AddComponent<Dead>(entity);
            }

            buildingPositions.Dispose();
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        /// <summary>Try to consume one unit of an item from a specific building.</summary>
        private static bool TryConsumeFromBuilding(ref SystemState state, Entity building, FixedString32Bytes itemId)
        {
            if (building == Entity.Null) return false;
            var inv = state.EntityManager.GetBuffer<InventorySlot>(building);
            for (int j = 0; j < inv.Length; j++)
            {
                if (inv[j].ItemId != itemId || inv[j].Quantity <= 0)
                    continue;
                var slot = inv[j];
                slot.Quantity -= 1;
                inv[j] = slot;
                return true;
            }
            return false;
        }

        /// <summary>Try to consume one unit from any public building on the tile (excluding skipBuilding).</summary>
        private static bool TryConsumeFromPublicBuilding(
            ref SystemState state,
            NativeHashMap<int2, Entity> buildingPositions,
            int2 citizenPos,
            FixedString32Bytes itemId,
            Entity skipBuilding = default)
        {
            if (buildingPositions.TryGetValue(citizenPos, out var building))
            {
                if (building != skipBuilding)
                {
                    var inv = state.EntityManager.GetBuffer<InventorySlot>(building);
                    for (int j = 0; j < inv.Length; j++)
                    {
                        if (inv[j].ItemId != itemId || inv[j].Quantity <= 0)
                            continue;
                        var slot = inv[j];
                        slot.Quantity -= 1;
                        inv[j] = slot;
                        return true;
                    }
                }
            }
            return false;
        }

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