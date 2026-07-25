using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Groundwork.TestHelpers;
using Groundwork.Simulation;
using Groundwork.TestHelpers;

namespace Groundwork.Tests.Simulation
{
    [TestFixture]
    public class CitizenMovementSystemTests
    {
        /// <summary>Helper: add waypoints via a short-lived buffer handle.</summary>
        private static void AddPath(SimulationTestWorld world, Entity citizen, params int2[] waypoints)
        {
            var buf = world.EntityManager.GetBuffer<PathFollowing>(citizen);
            foreach (var wp in waypoints)
                buf.Add(new PathFollowing { TileCoordinate = wp });
        }

        [Test]
        public void MovesCitizen_OneTilePerTick()
        {
            using var world = new SimulationTestWorld();
            var startPos = new int2(5, 5);
            var citizen = world.CreateCitizen(age: 30f, position: startPos);

            AddPath(world, citizen, new int2(6, 5), new int2(7, 5), new int2(8, 5));

            // Tick 1: move + re-path
            world.UpdateSystem<CitizenMovementSystem>();
            var pos = world.EntityManager.GetComponentData<MapPosition>(citizen);
            Assert.That(pos.TileCoordinate.x, Is.EqualTo(6));
            Assert.That(pos.TileCoordinate.y, Is.EqualTo(5));

            world.UpdateSystem<PathfindingSystem>();

            // Tick 2
            world.UpdateSystem<CitizenMovementSystem>();
            pos = world.EntityManager.GetComponentData<MapPosition>(citizen);
            Assert.That(pos.TileCoordinate.x, Is.EqualTo(7));

            world.UpdateSystem<PathfindingSystem>();

            // Tick 3
            world.UpdateSystem<CitizenMovementSystem>();
            pos = world.EntityManager.GetComponentData<MapPosition>(citizen);
            Assert.That(pos.TileCoordinate.x, Is.EqualTo(8));
            Assert.That(world.EntityManager.GetBuffer<PathFollowing>(citizen).Length,
                Is.EqualTo(0), "All waypoints consumed");
        }

        [Test]
        public void SetsTaskToIdle_WhenArrived()
        {
            using var world = new SimulationTestWorld();
            var citizen = world.CreateCitizen(age: 30f, position: new int2(5, 5));

            AddPath(world, citizen, new int2(6, 5), new int2(7, 5));

            world.UpdateSystem<CitizenMovementSystem>();
            var task = world.EntityManager.GetComponentData<CitizenTask>(citizen);
            Assert.That(task.TaskType.ToString(), Is.EqualTo("walking"));

            world.UpdateSystem<PathfindingSystem>();
            world.UpdateSystem<CitizenMovementSystem>();
            task = world.EntityManager.GetComponentData<CitizenTask>(citizen);
            Assert.That(task.TaskType.ToString(), Is.EqualTo("idle"));
        }

        [Test]
        public void EmitsTileLeaveAndTileEnter_OnMove()
        {
            using var world = new SimulationTestWorld();
            var startPos = new int2(5, 5);
            var citizen = world.CreateCitizen(age: 30f, position: startPos);

            AddPath(world, citizen, new int2(6, 5));

            world.UpdateSystem<CitizenMovementSystem>();

            var eventEntity = world.GetOrCreateEventBufferEntity();
            var events = world.EntityManager.GetBuffer<SimulationEvent>(eventEntity);
            Assert.That(events.Length, Is.EqualTo(2));

            Assert.That(events[0].Type, Is.EqualTo(EventType.TileLeave));
            Assert.That(events[0].Data0, Is.EqualTo(5f));
            Assert.That(events[0].Data1, Is.EqualTo(5f));

            Assert.That(events[1].Type, Is.EqualTo(EventType.TileEnter));
            Assert.That(events[1].Data0, Is.EqualTo(6f));
            Assert.That(events[1].Data1, Is.EqualTo(5f));
        }

        [Test]
        public void NoEvents_WhenPathIsEmpty()
        {
            using var world = new SimulationTestWorld();
            var citizen = world.CreateCitizen(age: 30f, position: new int2(5, 5));

            world.UpdateSystem<CitizenMovementSystem>();

            var eventEntity = world.GetOrCreateEventBufferEntity();
            var events = world.EntityManager.GetBuffer<SimulationEvent>(eventEntity);
            Assert.That(events.Length, Is.EqualTo(0));
        }

        [Test]
        public void IssuesPathRequest_AfterRePath()
        {
            using var world = new SimulationTestWorld();
            var citizen = world.CreateCitizen(age: 30f, position: new int2(5, 5));

            AddPath(world, citizen, new int2(6, 5), new int2(10, 10));

            world.UpdateSystem<CitizenMovementSystem>();

            // Re-path should have issued PathRequest
            Assert.That(world.EntityManager.HasComponent<PathRequest>(citizen), Is.True);
            var req = world.EntityManager.GetComponentData<PathRequest>(citizen);
            Assert.That(req.Destination.x, Is.EqualTo(10));
            Assert.That(req.Destination.y, Is.EqualTo(10));

            // Pathfinding should fill a new path from current position
            world.UpdateSystem<PathfindingSystem>();
            var newPath = world.EntityManager.GetBuffer<PathFollowing>(citizen);
            Assert.That(newPath.Length, Is.GreaterThan(0),
                "Pathfinding should fill new path from (6,5) to (10,10)");
        }
    }
}