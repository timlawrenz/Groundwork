using NUnit.Framework;
using Unity.Entities;
using Groundwork.Simulation;
using Groundwork.TestHelpers;
using Groundwork.TestHelpers;

namespace Groundwork.Tests.Simulation
{
    [TestFixture]
    public class EventDispatchSystemTests
    {
        [Test]
        public void EventBuffer_IsCleared_AfterDispatch()
        {
            using var world = new SimulationTestWorld();
            var bufferEntity = world.GetOrCreateEventBufferEntity();

            // Arrange: append events
            var events = world.EntityManager.GetBuffer<SimulationEvent>(bufferEntity);
            events.Add(new SimulationEvent { Type = EventType.CitizenBorn, EntityId = 42 });
            events.Add(new SimulationEvent { Type = EventType.SeasonChanged, EntityId = 0, Data0 = 2f });

            Assert.That(events.Length, Is.EqualTo(2),
                "Should have 2 events before dispatch");

            // Act
            world.UpdateSystem<EventDispatchSystem>();

            // Assert: buffer should be empty after dispatch
            Assert.That(events.Length, Is.EqualTo(0),
                "Buffer should be cleared after EventDispatchSystem runs");
        }

        [Test]
        public void EventBuffer_RetainsEvents_BeforeDispatch()
        {
            using var world = new SimulationTestWorld();
            var bufferEntity = world.GetOrCreateEventBufferEntity();

            var events = world.EntityManager.GetBuffer<SimulationEvent>(bufferEntity);
            events.Add(new SimulationEvent { Type = EventType.DayChanged, EntityId = 0 });

            // Run an unrelated system — events should NOT be cleared
            world.UpdateSystem<DeathSystem>();

            Assert.That(events.Length, Is.EqualTo(1),
                "Events should persist until EventDispatchSystem runs");
        }

        [Test]
        public void EventBuffer_PreservesOrder_OnRead()
        {
            using var world = new SimulationTestWorld();
            var bufferEntity = world.GetOrCreateEventBufferEntity();

            var events = world.EntityManager.GetBuffer<SimulationEvent>(bufferEntity);
            events.Add(new SimulationEvent { Type = EventType.CitizenBorn, EntityId = 1 });
            events.Add(new SimulationEvent { Type = EventType.CitizenDied, EntityId = 2 });
            events.Add(new SimulationEvent { Type = EventType.CitizenBorn, EntityId = 3 });

            Assert.That(events[0].Type, Is.EqualTo(EventType.CitizenBorn));
            Assert.That(events[0].EntityId, Is.EqualTo(1));
            Assert.That(events[1].Type, Is.EqualTo(EventType.CitizenDied));
            Assert.That(events[1].EntityId, Is.EqualTo(2));
            Assert.That(events[2].Type, Is.EqualTo(EventType.CitizenBorn));
            Assert.That(events[2].EntityId, Is.EqualTo(3));
        }
    }
}