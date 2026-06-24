using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Prototypes;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Damageable;

[TestOf(typeof(DamageableComponent))]
[TestOf(typeof(DamageableSystem))]
public sealed class DamageAllPrototypesTest : GameTest
{
    [SidedDependency(Side.Server)]
    private readonly DamageableSystem _damageableSystem = default!;

    [SidedDependency(Side.Server)]
    private readonly IComponentFactory _sComp = default!;

    [Test]
    [TestOf(typeof(DamageableSystem))]
    [Description("Ensures all Entity Prototypes with damageable can be damaged.")]
    public async Task TestDamageableComponentsOnPrototypes()
    {
        await Pair.CreateTestMap();
        var coords = Pair.TestMap!.GridCoords;
        var protoIds = Pair.GetPrototypesWithComponent<DamageableComponent>();

        var damageTypes = SProtoMan.EnumeratePrototypes<DamageTypePrototype>();

        foreach (var (damageable, comp) in protoIds)
        {
            // Intentionally cannot take damage, ignore it.
            if (damageable.HasComponent<GodmodeComponent>(_sComp))
                return;

            var entity = await SpawnAtPosition(damageable.ID, coords);

            var canBeDamaged = false;

            foreach (var type in damageTypes)
            {
                if (!_damageableSystem.CanBeDamagedBy(entity, type))
                    continue;

                canBeDamaged = true;

                await Server.WaitPost(() =>
                {
                    var damage = new DamageSpecifier(type, FixedPoint2.Epsilon);
                    var previousDamage = _damageableSystem.GetTotalDamage(entity);
                    _damageableSystem.ChangeDamage(entity, damage, ignoreResistances: true);
                    Assert.That(
                        _damageableSystem.GetTotalDamage(entity),
                        Is.EqualTo(FixedPoint2.Epsilon + previousDamage),
                        $"{nameof(EntityPrototype)} {damageable.ID} takes incorrect damage from {nameof(DamageTypePrototype)} {type.ID}"
                    );
                    _damageableSystem.ClearAllDamage(entity);
                });
            }
            // Ensure that this entity can actually be damaged.
            Assert.That(
                canBeDamaged,
                $"prototype {damageable.ID} has {nameof(DamageableComponent)} but can not take any types of damage"
            );
            SQueueDel(entity);
        }
    }
}
