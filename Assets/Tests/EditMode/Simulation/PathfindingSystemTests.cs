using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Groundwork.Simulation;
using Groundwork.TestHelpers;

namespace Groundwork.Tests.Simulation
{
    [TestFixture]
    public class PathfindingSystemTests
    {
        [Test]
        public void ComputesPath_BetweenTwoTiles()
        {
            using var world = new SimulationTestWorld();
            var citizen = world.CreateCitizen(age: 30f, position: new int2(5, 5));

            world.EntityManager.AddComponentData(citizen, new PathRequest
            {
                Destination = new int2(10, 5) // 5 tiles east
            });

            world.UpdateSystem<PathfindingSystem>();

            // Should have computed a path
            var path = world.EntityManager.GetBuffer<PathFollowing>(citizen);
            Assert.That(path.Length, Is.GreaterThan(0), "Should have computed a path");

            // Last waypoint should be the destination
            var lastWaypoint = path[path.Length - 1];
            Assert.That(lastWaypoint.TileCoordinate.x, Is.EqualTo(10));
            Assert.That(lastWaypoint.TileCoordinate.y, Is.EqualTo(5));

            // PathRequest should be removed
            Assert.That(world.EntityManager.HasComponent<PathRequest>(citizen), Is.False);
        }

        [Test]
        public void ComputesPath_Diagonal()
        {
            using var world = new SimulationTestWorld();
            var citizen = world.CreateCitizen(age: 30f, position: new int2(5, 5));

            world.EntityManager.AddComponentData(citizen, new PathRequest
            {
                Destination = new int2(10, 10) // diagonal
            });

            world.UpdateSystem<PathfindingSystem>();

            var path = world.EntityManager.GetBuffer<PathFollowing>(citizen);
            Assert.That(path.Length, Is.GreaterThan(0));

            var last = path[path.Length - 1];
            Assert.That(last.TileCoordinate.x, Is.EqualTo(10));
            Assert.That(last.TileCoordinate.y, Is.EqualTo(10));
        }

        [Test]
        public void ReturnsEmptyPath_WhenSameTile()
        {
            using var world = new SimulationTestWorld();
            var citizen = world.CreateCitizen(age: 30f, position: new int2(5, 5));

            world.EntityManager.AddComponentData(citizen, new PathRequest
            {
                Destination = new int2(5, 5) // same tile
            });

            world.UpdateSystem<PathfindingSystem>();

            var path = world.EntityManager.GetBuffer<PathFollowing>(citizen);
            Assert.That(path.Length, Is.EqualTo(0),
                "Path to same tile should be empty");

            Assert.That(world.EntityManager.HasComponent<PathRequest>(citizen), Is.False);
        }

        [Test]
        public void Handles_MultipleCitizens()
        {
            using var world = new SimulationTestWorld();
            var c1 = world.CreateCitizen(age: 30f, position: new int2(0, 0));
            var c2 = world.CreateCitizen(age: 30f, position: new int2(5, 5));
            var c3 = world.CreateCitizen(age: 30f, position: new int2(10, 10));

            world.EntityManager.AddComponentData(c1, new PathRequest { Destination = new(15, 0) });
            world.EntityManager.AddComponentData(c2, new PathRequest { Destination = new(0, 10) });
            world.EntityManager.AddComponentData(c3, new PathRequest { Destination = new(15, 15) });

            world.UpdateSystem<PathfindingSystem>();

            Assert.That(world.EntityManager.HasComponent<PathRequest>(c1), Is.False);
            Assert.That(world.EntityManager.HasComponent<PathRequest>(c2), Is.False);
            Assert.That(world.EntityManager.HasComponent<PathRequest>(c3), Is.False);

            var path1 = world.EntityManager.GetBuffer<PathFollowing>(c1);
            var path2 = world.EntityManager.GetBuffer<PathFollowing>(c2);
            var path3 = world.EntityManager.GetBuffer<PathFollowing>(c3);
            Assert.That(path1.Length, Is.GreaterThan(0));
            Assert.That(path2.Length, Is.GreaterThan(0));
            Assert.That(path3.Length, Is.GreaterThan(0));
        }
    }
}