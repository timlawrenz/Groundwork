using NUnit.Framework;
using Unity.Entities;
using Unity.Collections;
using Groundwork.Simulation;
using Groundwork.TestHelpers;

namespace Groundwork.Tests.Simulation
{
    [TestFixture]
    public class CitizenNeedSystemTests
    {
        [Test]
        public void GeneratesFoodNeed_Daily()
        {
            using var world = new SimulationTestWorld();
            var citizen = world.CreateCitizen(age: 30f, health: 100f);
            world.SetTick(24);

            world.UpdateSystem<CitizenNeedSystem>();

            var needs = world.EntityManager.GetBuffer<CitizenNeed>(citizen);
            float foodUrgency = 0f;
            for (int i = 0; i < needs.Length; i++)
                if (needs[i].NeedType == "food")
                    foodUrgency = needs[i].Urgency;

            Assert.That(foodUrgency, Is.GreaterThan(0.3f),
                "Food urgency should increase from initial 0.3");
        }

        [Test]
        public void GeneratesWarmthNeed_HigherInWinter()
        {
            using var world = new SimulationTestWorld();

            // Set calendar to winter (season 3)
            var calQuery = world.EntityManager.CreateEntityQuery(typeof(CalendarSingleton));
            var calEntity = calQuery.GetSingletonEntity();
            var cal = world.EntityManager.GetComponentData<CalendarSingleton>(calEntity);
            cal.Season = 3; // winter
            cal.Temperature = -5f;
            world.EntityManager.SetComponentData(calEntity, cal);

            var citizen = world.CreateCitizen(age: 30f);
            world.SetTick(24);

            world.UpdateSystem<CitizenNeedSystem>();

            var needs = world.EntityManager.GetBuffer<CitizenNeed>(citizen);
            float warmthUrgency = 0f;
            for (int i = 0; i < needs.Length; i++)
                if (needs[i].NeedType == "warmth")
                    warmthUrgency = needs[i].Urgency;

            Assert.That(warmthUrgency, Is.GreaterThan(0.1f),
                "Warmth urgency should be higher in winter");
        }

        [Test]
        public void DecaysHealth_WhenCriticalNeedUnmet()
        {
            using var world = new SimulationTestWorld();
            var citizen = world.CreateCitizen(age: 30f, health: 100f);

            // Set food need to critical
            var needs = world.EntityManager.GetBuffer<CitizenNeed>(citizen);
            for (int i = 0; i < needs.Length; i++)
            {
                if (needs[i].NeedType == "food")
                {
                    var need = needs[i];
                    need.Urgency = 0.9f; // critical
                    needs[i] = need;
                }
            }

            world.SetTick(24);
            world.UpdateSystem<CitizenNeedSystem>();

            var c = world.EntityManager.GetComponentData<Citizen>(citizen);
            Assert.That(c.Health, Is.LessThan(100f),
                "Health should decay when critical need is unmet");
        }

        [Test]
        public void TagsDead_WhenHealthReachesZero()
        {
            using var world = new SimulationTestWorld();
            var citizen = world.CreateCitizen(age: 30f, health: 0.1f); // nearly dead

            // Set food need to critical
            var needs = world.EntityManager.GetBuffer<CitizenNeed>(citizen);
            for (int i = 0; i < needs.Length; i++)
            {
                if (needs[i].NeedType == "food")
                {
                    var need = needs[i];
                    need.Urgency = 0.95f;
                    needs[i] = need;
                }
            }

            world.SetTick(24);
            world.UpdateSystem<CitizenNeedSystem>();

            Assert.That(world.EntityManager.HasComponent<Dead>(citizen),
                "Citizen with zero health should be tagged Dead");
        }

        [Test]
        public void GeneratesShelterNeed_WhenHomeless()
        {
            using var world = new SimulationTestWorld();
            var citizen = world.CreateCitizen(age: 30f);
            world.SetTick(24);

            world.UpdateSystem<CitizenNeedSystem>();

            var needs = world.EntityManager.GetBuffer<CitizenNeed>(citizen);
            bool hasShelter = false;
            for (int i = 0; i < needs.Length; i++)
                if (needs[i].NeedType == "shelter")
                    hasShelter = true;

            Assert.That(hasShelter, "Homeless citizens should generate shelter need");
        }

        [Test]
        public void CapsUrgency_AtOne()
        {
            using var world = new SimulationTestWorld();
            var citizen = world.CreateCitizen(age: 30f);

            // Run many days
            for (int day = 0; day < 100; day++)
            {
                world.SetTick(day * 24 + 24);
                world.UpdateSystem<CitizenNeedSystem>();
            }

            var needs = world.EntityManager.GetBuffer<CitizenNeed>(citizen);
            for (int i = 0; i < needs.Length; i++)
            {
                Assert.That(needs[i].Urgency, Is.LessThanOrEqualTo(1f),
                    $"Need '{needs[i].NeedType}' should not exceed 1.0");
            }
        }
    }
}