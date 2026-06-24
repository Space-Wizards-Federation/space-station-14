using Content.IntegrationTests.Fixtures;
using Content.Server.Doors;
using Content.Server.Power;
using Content.Server.Wires;
using Content.Shared.Doors;
using Content.Shared.Wires;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.Wires;

[TestFixture]
[Parallelizable(ParallelScope.All)]
[TestOf(typeof(WiresSystem))]
public sealed class WireLayoutTest : GameTest
{
    [TestPrototypes]
    public const string Prototypes = """
        - type: wireLayout
          id: WireLayoutTest
          dummyWires: 2
          wires:
          - !type:PowerWireAction
          - !type:DoorBoltWireAction

        - type: wireLayout
          id: WireLayoutTest2
          parent: WireLayoutTest
          wires:
          - !type:PowerWireAction

        - type: wireLayout
          id: WireLayoutTest3
          parent: WireLayoutTest

        - type: entity
          id: WireLayoutTest
          components:
          - type: Wires
            layoutId: WireLayoutTest

        - type: entity
          id: WireLayoutTest2
          components:
          - type: Wires
            layoutId: WireLayoutTest2

        - type: entity
          id: WireLayoutTest3
          components:
          - type: Wires
            layoutId: WireLayoutTest3
        """;

    [Test]
    public async Task TestLayoutInheritance()
    {
        var pair = Pair;
        var server = pair.Server;
        var client = pair.Client;
        var testMap = await pair.CreateTestMap();

        EntityUid ent1 = default;
        EntityUid ent2 = default;
        EntityUid ent3 = default;

        await server.WaitAssertion(() =>
        {
            // Need to spawn these entities to make sure the wire layouts are initialized.
            ent1 = SpawnWithComp<WiresComponent>(server.EntMan, "WireLayoutTest", testMap.MapCoords);
            ent2 = SpawnWithComp<WiresComponent>(server.EntMan, "WireLayoutTest2", testMap.MapCoords);
            ent3 = SpawnWithComp<WiresComponent>(server.EntMan, "WireLayoutTest3", testMap.MapCoords);

            // Assert.That(wires.TryGetLayout("WireLayoutTest", out var layout1));
            // Assert.That(wires.TryGetLayout("WireLayoutTest2", out var layout2));
            // Assert.That(wires.TryGetLayout("WireLayoutTest3", out var layout3));

            Assert.Multiple(() =>
            {
                // Entity 1.
                var comp1 = server.EntMan.GetComponent<WiresComponent>(ent1);
                Assert.That(comp1.WiresList, Has.Count.EqualTo(4));
                Assert.That(comp1.WiresList, Has.Exactly(2).With.Property("Action").Null, "2 dummy wires");
                Assert.That(comp1.WiresList, Has.One.With.Property("Action").InstanceOf<PowerWireAction>(), "1 power wire");
                Assert.That(comp1.WiresList, Has.One.With.Property("Action").InstanceOf<DoorBoltWireAction>(), "1 door bolt wire");

                var comp2 = server.EntMan.GetComponent<WiresComponent>(ent2);
                Assert.That(comp2.WiresList, Has.Count.EqualTo(5));
                Assert.That(comp2.WiresList, Has.Exactly(2).With.Property("Action").Null, "2 dummy wires");
                Assert.That(comp2.WiresList, Has.Exactly(2).With.Property("Action").InstanceOf<PowerWireAction>(), "2 power wire");
                Assert.That(comp2.WiresList, Has.One.With.Property("Action").InstanceOf<DoorBoltWireAction>(), "1 door bolt wire");

                var comp3 = server.EntMan.GetComponent<WiresComponent>(ent3);
                Assert.That(comp3.WiresList, Has.Count.EqualTo(4));
                Assert.That(comp3.WiresList, Has.Exactly(2).With.Property("Action").Null, "2 dummy wires");
                Assert.That(comp3.WiresList, Has.One.With.Property("Action").InstanceOf<PowerWireAction>(), "1 power wire");
                Assert.That(comp3.WiresList, Has.One.With.Property("Action").InstanceOf<DoorBoltWireAction>(), "1 door bolt wire");
            });
        });

        await pair.RunTicksSync(5);

        await client.WaitAssertion(() =>
        {
            var clientUid = client.EntMan.GetEntity(server.EntMan.GetNetEntity(ent1));
            var comp = client.EntMan.GetComponent<WiresComponent>(clientUid);

            // The UI uses these networked fields directly.
            Assert.That(comp.ClientWires, Has.Length.EqualTo(4));
            Assert.That(comp.StatusEntries, Is.Not.Empty);
        });

        await client.WaitAssertion(() =>
        {
            var clientUid = client.EntMan.GetEntity(server.EntMan.GetNetEntity(ent1));
            var comp = client.EntMan.GetComponent<WiresComponent>(clientUid);
            var statusCount = comp.StatusEntries.Length;

            // Client-side layout actions may be null due to sussy serializer; refreshing the UI data should still keep it set.
            client.EntMan.System<SharedWiresSystem>().SetData(clientUid, nameof(TestLayoutInheritance), true, comp);

            Assert.That(comp.StatusEntries, Has.Length.EqualTo(statusCount));
            Assert.That(comp.ClientWires, Has.Length.EqualTo(4));
        });
    }

    private static EntityUid SpawnWithComp<T>(IEntityManager entityManager, string prototype, MapCoordinates coords)
        where T : IComponent, new()
    {
        var ent = entityManager.Spawn(prototype, coords);
        entityManager.EnsureComponent<T>(ent);
        return ent;
    }
}
