using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

namespace Groundwork.Simulation
{
    /// <summary>
    /// Updates citizen needs each game-day. Decays health when critical needs go unmet.
    /// Needs are satisfied by consuming resources from inventory (e.g. eating food).
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

            foreach (var (citizen, entity) in
                     SystemAPI.Query<RefRW<Citizen>>()
                         .WithNone<Dead>()
                         .WithEntityAccess())
            {
                var needs = state.EntityManager.GetBuffer<CitizenNeed>(entity);
                var inventory = state.EntityManager.GetBuffer<InventorySlot>(entity);

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

                    // Check home building inventory for firewood
                    if (citizen.ValueRO.HomeBuilding != Entity.Null)
                    {
                        var homeInv = state.EntityManager.GetBuffer<InventorySlot>(
                            citizen.ValueRO.HomeBuilding);
                        for (int j = 0; j < homeInv.Length; j++)
                        {
                            if (homeInv[j].ItemId != "firewood" || homeInv[j].Quantity <= 0)
                                continue;

                            var slot = homeInv[j];
                            slot.Quantity -= 1;
                            homeInv[j] = slot;

                            var need = needs[i];
                            need.Urgency = math.max(0f, need.Urgency - 0.5f);
                            needs[i] = need;
                            break;
                        }
                    }
                    break;
                }

                // ─── Need growth ───

                // Food need — always present, grows daily
                UpsertNeed(needs, "food", 0.15f);

                // Warmth need
                if (calendar.Season >= 2)
                    UpsertNeed(needs, "warmth", 0.1f); // reduced for sustainability
                else
                    UpsertNeed(needs, "warmth", 0.01f);

                // Shelter need — homeless citizens
                if (citizen.ValueRO.HomeBuilding == Entity.Null)
                    UpsertNeed(needs, "shelter", 0.25f);

                // Health need — escalates when health is low
                if (citizen.ValueRO.Health < 30f)
                    UpsertNeed(needs, "health", 0.05f);

                // Social need
                UpsertNeed(needs, "social", 0.01f);

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

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
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