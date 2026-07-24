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
                for (int i = 0; i < needs.Length; i++)
                {
                    if (needs[i].NeedType != "food")
                        continue;
                    if (needs[i].Urgency < 0.3f)
                        break; // not hungry enough to eat

                    // Find food in inventory
                    for (int j = 0; j < inventory.Length; j++)
                    {
                        if (inventory[j].ItemId != "food" || inventory[j].Quantity <= 0)
                            continue;

                        var slot = inventory[j];
                        slot.Quantity -= 1;
                        inventory[j] = slot;

                        var need = needs[i];
                        need.Urgency = math.max(0f, need.Urgency - 0.5f);
                        needs[i] = need;
                        break;
                    }
                    break; // only process food need once per citizen per day
                }

                // ─── Need growth ───

                // Food need — always present, grows daily
                UpsertNeed(needs, "food", 0.15f);

                // Warmth need
                if (calendar.Season >= 2)
                    UpsertNeed(needs, "warmth", 0.2f);
                else
                    UpsertNeed(needs, "warmth", 0.02f);

                // Shelter need — homeless citizens
                if (citizen.ValueRO.HomeBuilding == Entity.Null)
                    UpsertNeed(needs, "shelter", 0.25f);

                // Health need — escalates when health is low
                if (citizen.ValueRO.Health < 50f)
                    UpsertNeed(needs, "health", 0.1f);

                // Social need
                UpsertNeed(needs, "social", 0.05f);

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