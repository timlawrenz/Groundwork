using NUnit.Framework;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Groundwork.TestHelpers;
using Groundwork.Simulation;

namespace Groundwork.Tests.Simulation
{
    [TestFixture]
    public class BuildingProductionSystemTests
    {
        [Test]
        public void GatherFood_ProducesFood_WithoutInputs()
        {
            using var world = new SimulationTestWorld();
            var hut = world.CreateBuilding("gatherer_hut", new(10, 10), maxWorkers: 0); // autonomous for test
            world.AddProductionOrder(hut, "gather_food");

            // Run production for 10 ticks to complete one cycle
            for (int i = 0; i < 10; i++)
                world.UpdateSystem<BuildingProductionSystem>();

            var inventory = world.EntityManager.GetBuffer<OutputSlot>(hut);
            int foodCount = GetItemCount(inventory, "food");
            Assert.That(foodCount, Is.EqualTo(10), "Should produce 10 food per 10 ticks (TicksPerCycle=1)");
        }

        [Test]
        public void ChopFirewood_ConsumesLogs_ProducesFirewood()
        {
            using var world = new SimulationTestWorld();
            var woodcutter = world.CreateBuilding("woodcutter", new(10, 10), maxWorkers: 0);
            world.EntityManager.GetBuffer<InventorySlot>(woodcutter).Add(new InventorySlot { ItemId = "logs", Quantity = 10 });
            world.AddProductionOrder(woodcutter, "chop_firewood");

            // Run 10 ticks for one complete cycle (0.1 progress per tick → 1.0)
            for (int i = 0; i < 10; i++)
                world.UpdateSystem<BuildingProductionSystem>();

            var inputInv = world.EntityManager.GetBuffer<InventorySlot>(woodcutter);
            var outputInv = world.EntityManager.GetBuffer<OutputSlot>(woodcutter);
            int logsLeft = GetItemCountInput(inputInv, "logs");
            int firewood = GetItemCount(outputInv, "firewood");

            Assert.That(logsLeft, Is.EqualTo(0), "Should consume all 10 logs (1 per tick × 10 ticks = 1 cycle)");
            Assert.That(firewood, Is.EqualTo(10), "Should produce 10 firewood (TicksPerCycle=1)");
        }

        [Test]
        public void ChopFirewood_Stalls_WhenNoLogs()
        {
            using var world = new SimulationTestWorld();
            var woodcutter = world.CreateBuilding("woodcutter", new(10, 10), maxWorkers: 0);
            // No logs in inventory
            world.AddProductionOrder(woodcutter, "chop_firewood");

            // Run 10 ticks
            for (int i = 0; i < 10; i++)
                world.UpdateSystem<BuildingProductionSystem>();

            var inventory = world.EntityManager.GetBuffer<OutputSlot>(woodcutter);
            int firewood = GetItemCount(inventory, "firewood");
            Assert.That(firewood, Is.EqualTo(0), "Should not produce without logs");
        }

        [Test]
        public void CompletedOrder_ResetsProgress_ForNextCycle()
        {
            using var world = new SimulationTestWorld();
            var hut = world.CreateBuilding("gatherer_hut", new(10, 10), maxWorkers: 0);
            var queue = world.EntityManager.GetBuffer<ProductionOrder>(hut);
            queue.Add(new ProductionOrder { RecipeId = "gather_food", Progress = 1f });

            world.UpdateSystem<BuildingProductionSystem>();

            // Progress resets to 0 after completion so the recipe can cycle again
            var updated = world.EntityManager.GetBuffer<ProductionOrder>(hut);
            Assert.That(updated[0].Progress, Is.EqualTo(0f));
        }

        // ─── Regression: continuous production ───

        [Test]
        public void ChopFirewood_ProducesMultipleCycles_WhenEnoughLogs()
        {
            using var world = new SimulationTestWorld();
            var woodcutter = world.CreateBuilding("woodcutter", new(10, 10), maxWorkers: 0);
            world.EntityManager.GetBuffer<InventorySlot>(woodcutter).Add(new InventorySlot { ItemId = "logs", Quantity = 20 }); // 2 cycles
            world.AddProductionOrder(woodcutter, "chop_firewood");

            // Run 20 ticks (2 complete cycles: 10 ticks each)
            for (int i = 0; i < 20; i++)
                world.UpdateSystem<BuildingProductionSystem>();

            var inputInv = world.EntityManager.GetBuffer<InventorySlot>(woodcutter);
            var outputInv = world.EntityManager.GetBuffer<OutputSlot>(woodcutter);
            int logsLeft = GetItemCountInput(inputInv, "logs");
            int firewood = GetItemCount(outputInv, "firewood");

            Assert.That(logsLeft, Is.EqualTo(0), "Should consume all 20 logs");
            Assert.That(firewood, Is.EqualTo(20), "Should produce 20 firewood over 20 ticks (TicksPerCycle=1)");
        }

        [Test]
        public void DoesNotOperate_UnderConstruction()
        {
            using var world = new SimulationTestWorld();
            var hut = world.CreateBuilding("gatherer_hut", new(10, 10), operational: false);
            world.EntityManager.AddComponent<UnderConstruction>(hut);
            world.AddProductionOrder(hut, "gather_food");

            for (int i = 0; i < 10; i++)
                world.UpdateSystem<BuildingProductionSystem>();

            var inventory = world.EntityManager.GetBuffer<OutputSlot>(hut);
            int foodCount = GetItemCount(inventory, "food");
            Assert.That(foodCount, Is.EqualTo(0), "Under construction should not produce");
        }

        // ─── Regression: worker requirement ───

        [Test]
        public void RequiresWorkers_WhenMaxWorkersGreaterThanZero()
        {
            using var world = new SimulationTestWorld();

            // Building with MaxWorkers=3 but no citizens assigned
            var hut = world.CreateBuilding("gatherer_hut", new(10, 10), maxWorkers: 3);
            world.AddProductionOrder(hut, "gather_food");

            // Run 10 ticks
            for (int i = 0; i < 10; i++)
                world.UpdateSystem<BuildingProductionSystem>();

            var inventory = world.EntityManager.GetBuffer<OutputSlot>(hut);
            int foodCount = GetItemCount(inventory, "food");
            Assert.That(foodCount, Is.EqualTo(0),
                "Should not produce without workers when MaxWorkers > 0");
        }

        [Test]
        public void Produces_WhenWorkersAssigned()
        {
            using var world = new SimulationTestWorld();

            var hutPos = new int2(10, 10);
            var hut = world.CreateBuilding("gatherer_hut", hutPos, maxWorkers: 3);
            world.AddProductionOrder(hut, "gather_food");

            // Assign a citizen as worker, placed within the gathering zone
            var citizen = world.CreateCitizen(age: 30f, workplace: hut, position: hutPos);

            for (int i = 0; i < 10; i++)
                world.UpdateSystem<BuildingProductionSystem>();

            var inventory = world.EntityManager.GetBuffer<OutputSlot>(hut);
            int foodCount = GetItemCount(inventory, "food");
            Assert.That(foodCount, Is.GreaterThan(0),
                "Should produce when at least one worker is assigned AND within zone");
        }

        [Test]
        public void AutonomousBuilding_ProducesWithoutWorkers_WhenMaxWorkersZero()
        {
            using var world = new SimulationTestWorld();

            // MaxWorkers=0 means autonomous (e.g. a well, windmill)
            var well = world.CreateBuilding("well", new(10, 10), maxWorkers: 0);
            world.AddProductionOrder(well, "gather_food"); // well produces water

            for (int i = 0; i < 10; i++)
                world.UpdateSystem<BuildingProductionSystem>();

            var inventory = world.EntityManager.GetBuffer<OutputSlot>(well);
            int foodCount = GetItemCount(inventory, "food");
            Assert.That(foodCount, Is.GreaterThan(0),
                "Autonomous buildings (MaxWorkers=0) should produce without workers");
        }

        [Test]
        public void EmitsProductionCompleteEvent()
        {
            using var world = new SimulationTestWorld();
            var hut = world.CreateBuilding("gatherer_hut", new(10, 10), maxWorkers: 0);
            world.AddProductionOrder(hut, "gather_food");

            // Run enough ticks to complete one cycle (10 ticks at 0.1/tick = 1.0)
            for (int i = 0; i < 10; i++)
                world.UpdateSystem<BuildingProductionSystem>();

            var eventEntity = world.GetOrCreateEventBufferEntity();
            var events = world.EntityManager.GetBuffer<SimulationEvent>(eventEntity);
            Assert.That(events.Length, Is.EqualTo(10));  // TicksPerCycle=1 produces 10 events in 10 ticks
            Assert.That(events[0].Type, Is.EqualTo(EventType.ProductionComplete));
            Assert.That(events[0].EntityId, Is.EqualTo(hut.Index));
        }

        private static int GetItemCount(DynamicBuffer<OutputSlot> inventory, FixedString32Bytes itemId)
        {
            for (int i = 0; i < inventory.Length; i++)
                if (inventory[i].ItemId == itemId)
                    return inventory[i].Quantity;
            return 0;
        }

        private static int GetItemCountInput(DynamicBuffer<InventorySlot> inventory, FixedString32Bytes itemId)
        {
            for (int i = 0; i < inventory.Length; i++)
                if (inventory[i].ItemId == itemId)
                    return inventory[i].Quantity;
            return 0;
        }

        // ─── Gathering archetype tests (ADR 2026-07-25) ───

        [Test]
        public void GathererProduces_WhenWorkerInZone()
        {
            using var world = new SimulationTestWorld();

            var hutPos = new int2(10, 10);
            var hut = world.CreateBuilding("gatherer_hut", hutPos, maxWorkers: 3);
            world.AddProductionOrder(hut, "gather_food");

            // Worker within zone (radius 5 → covers 5..15)
            var citizen = world.CreateCitizen(age: 30f, workplace: hut, position: new int2(8, 12));

            for (int i = 0; i < 10; i++)
                world.UpdateSystem<BuildingProductionSystem>();

            var inventory = world.EntityManager.GetBuffer<OutputSlot>(hut);
            int foodCount = GetItemCount(inventory, "food");
            Assert.That(foodCount, Is.GreaterThan(0),
                "Should produce when worker is within the gathering zone");
        }

        [Test]
        public void GathererDoesNotProduce_WhenWorkerOutsideZone()
        {
            using var world = new SimulationTestWorld();

            var hutPos = new int2(10, 10);
            var hut = world.CreateBuilding("gatherer_hut", hutPos, maxWorkers: 3);
            world.AddProductionOrder(hut, "gather_food");

            // Worker far outside zone (radius 5 → zone covers 5..15; worker at 0,0 is outside)
            var citizen = world.CreateCitizen(age: 30f, workplace: hut, position: new int2(0, 0));

            for (int i = 0; i < 10; i++)
                world.UpdateSystem<BuildingProductionSystem>();

            var inventory = world.EntityManager.GetBuffer<OutputSlot>(hut);
            int foodCount = GetItemCount(inventory, "food");
            Assert.That(foodCount, Is.EqualTo(0),
                "Should NOT produce when worker is outside the gathering zone");
        }

        [Test]
        public void NonOverlappingZones_GetFullOutput()
        {
            using var world = new SimulationTestWorld();

            // Two gatherer huts, far apart — no zone overlap
            var hutA = world.CreateBuilding("gatherer_hut", new(10, 10), maxWorkers: 1);
            var hutB = world.CreateBuilding("gatherer_hut", new(50, 50), maxWorkers: 1);
            world.AddProductionOrder(hutA, "gather_food");
            world.AddProductionOrder(hutB, "gather_food");

            // Workers at their respective huts
            world.CreateCitizen(age: 30f, workplace: hutA, position: new int2(10, 10));
            world.CreateCitizen(age: 30f, workplace: hutB, position: new int2(50, 50));

            for (int i = 0; i < 10; i++)
                world.UpdateSystem<BuildingProductionSystem>();

            var invA = world.EntityManager.GetBuffer<OutputSlot>(hutA);
            var invB = world.EntityManager.GetBuffer<OutputSlot>(hutB);
            int foodA = GetItemCount(invA, "food");
            int foodB = GetItemCount(invB, "food");

            // Both should produce full output independently
            Assert.That(foodA, Is.EqualTo(10), "Non-overlapping zone A should produce full output");
            Assert.That(foodB, Is.EqualTo(10), "Non-overlapping zone B should produce full output");
        }

        [Test]
        public void OverlappingZones_ReducePerWorkerOutput()
        {
            using var world = new SimulationTestWorld();

            // Two gatherer huts close together — zones overlap
            // Radius 5 → each zone is 11×11=121 tiles
            // Centers at (10,10) and (15,10): overlap = 6×11 = 66 tiles
            // Overlap ratio = 66/121 ≈ 0.545
            // Penalty for each = 1/(1+0.545) ≈ 0.647
            var hutA = world.CreateBuilding("gatherer_hut", new(10, 10), maxWorkers: 1);
            var hutB = world.CreateBuilding("gatherer_hut", new(15, 10), maxWorkers: 1);
            world.AddProductionOrder(hutA, "gather_food");
            world.AddProductionOrder(hutB, "gather_food");

            world.CreateCitizen(age: 30f, workplace: hutA, position: new int2(10, 10));
            world.CreateCitizen(age: 30f, workplace: hutB, position: new int2(15, 10));

            // Run many ticks — each tick advances production
            for (int i = 0; i < 100; i++)
            {
                world.AdvanceTicks(1);
                world.UpdateSystem<BuildingProductionSystem>();
            }

            var invA = world.EntityManager.GetBuffer<OutputSlot>(hutA);
            var invB = world.EntityManager.GetBuffer<OutputSlot>(hutB);
            int foodA = GetItemCount(invA, "food");
            int foodB = GetItemCount(invB, "food");

            // With overlap penalty ~0.65, total should be well under 200 (non-overlapping total)
            Assert.That(foodA + foodB, Is.LessThan(200),
                $"Overlapping zones should produce less total than non-overlapping. Got A={foodA} B={foodB}");
            Assert.That(foodA, Is.GreaterThan(0),
                "Overlapping zone A should still produce some output");
            Assert.That(foodB, Is.GreaterThan(0),
                "Overlapping zone B should still produce some output");
        }

        [Test]
        public void WorkshopRequiresWorkerAtBuildingTile()
        {
            using var world = new SimulationTestWorld();

            var woodcutterPos = new int2(10, 10);
            var woodcutter = world.CreateBuilding("woodcutter", woodcutterPos, maxWorkers: 2);
            world.EntityManager.GetBuffer<InventorySlot>(woodcutter)
                .Add(new InventorySlot { ItemId = "logs", Quantity = 20 });
            world.AddProductionOrder(woodcutter, "chop_firewood");

            // Worker assigned but NOT at building tile — no production
            var citizenFar = world.CreateCitizen(age: 30f, workplace: woodcutter, position: new int2(0, 0));

            for (int i = 0; i < 5; i++)
                world.UpdateSystem<BuildingProductionSystem>();

            var invFar = world.EntityManager.GetBuffer<OutputSlot>(woodcutter);
            int firewoodFar = GetItemCount(invFar, "firewood");
            Assert.That(firewoodFar, Is.EqualTo(0),
                "Workshop should NOT produce when worker is not at building tile");

            // Move worker to building tile
            world.EntityManager.SetComponentData(citizenFar,
                new MapPosition { TileCoordinate = woodcutterPos, Rotation = 0 });

            for (int i = 0; i < 5; i++)
                world.UpdateSystem<BuildingProductionSystem>();

            int firewoodAt = GetItemCount(invFar, "firewood");
            Assert.That(firewoodAt, Is.GreaterThan(0),
                "Workshop should produce when worker is at building tile");
        }

        [Test]
        public void Gathering_WorkerCountDoesNotAffectPerTickProduction()
        {
            // Multiple workers in zone should NOT produce more per tick than one worker
            // (each cycle completes at the same rate regardless of worker count)
            using var world = new SimulationTestWorld();

            var hutPos = new int2(10, 10);
            var hut = world.CreateBuilding("gatherer_hut", hutPos, maxWorkers: 4);
            world.AddProductionOrder(hut, "gather_food");

            // Multiple workers all within zone
            world.CreateCitizen(age: 30f, workplace: hut, position: new int2(10, 10));
            world.CreateCitizen(age: 30f, workplace: hut, position: new int2(12, 10));
            world.CreateCitizen(age: 30f, workplace: hut, position: new int2(10, 12));

            for (int i = 0; i < 10; i++)
                world.UpdateSystem<BuildingProductionSystem>();

            var inventory = world.EntityManager.GetBuffer<OutputSlot>(hut);
            int foodCount = GetItemCount(inventory, "food");
            Assert.That(foodCount, Is.EqualTo(10),
                "Production is per-building, not per-worker — 1 cycle per tick regardless of worker count");
        }

        [Test]
        public void SourceArchetype_ProducesWithoutWorker()
        {
            // A building with Source archetype should produce without any workers
            // This tests the non-Workshop/non-Gathering fallback path
            using var world = new SimulationTestWorld();

            var well = world.CreateBuilding("well", new(10, 10), maxWorkers: 0);
            world.AddProductionOrder(well, "gather_food"); // well produces food as stand-in

            // No workers assigned at all
            for (int i = 0; i < 10; i++)
                world.UpdateSystem<BuildingProductionSystem>();

            var inventory = world.EntityManager.GetBuffer<OutputSlot>(well);
            int foodCount = GetItemCount(inventory, "food");
            Assert.That(foodCount, Is.GreaterThan(0),
                "Source archetype (unspecified) should produce without workers when MaxWorkers=0");
        }
    }
}