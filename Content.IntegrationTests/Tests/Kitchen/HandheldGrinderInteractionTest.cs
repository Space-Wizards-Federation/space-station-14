using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.Kitchen.Components;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Kitchen;

public sealed class HandheldGrinderInteractionTest : InteractionTest
{
    private static readonly EntProtoId Mortar = "MortarAndPestle";
    private static readonly EntProtoId Juicer = "HandheldJuicer";

    private static readonly EntProtoId SteelSheet = "SheetSteel1";
    private static readonly EntProtoId Banana = "TestFoodBanana";

    [TestPrototypes]
    private static readonly string TestPrototypes = $@"
# A modified banana that can only be juiced.
- type: entity
  parent: FoodBanana
  id: {Banana.Id}
  components:
  - type: Extractable
    grindableSolutionName: null
";

    /// <summary>
    /// Spawns a mortar and grinds steel in it, then grinds an ungrindable banana.
    /// Does the same with a juicer, but first a banana and then steel.
    /// </summary>
    [Test]
    public async Task GrindAndJuiceInHandheldGrindersTest()
    {
        await SpawnTarget(Mortar);
        var grinderComp = Comp<HandheldGrinderComponent>();

        await InteractUsing(SteelSheet);

        Assert.That(grinderComp.GrinderSolution, Is.Not.Null); // The grinder needs to have its valid solution resolved after interaction.
        Assert.That(grinderComp.GrinderSolution.Value.Comp.Solution.Volume > 0f); // Steel shouldve been grinded just fine.

        grinderComp.GrinderSolution.Value.Comp.Solution.RemoveAllSolution(); // Clean the solution, since we check if its empty after next interaction.

        await InteractUsing(Banana);

        Assert.That(grinderComp.GrinderSolution.Value.Comp.Solution.Volume == 0f); // The banana shouldn't have been grinded, since it can only be juiced.

        await SpawnTarget(Juicer);
        grinderComp = Comp<HandheldGrinderComponent>();

        await InteractUsing(Banana);

        Assert.That(grinderComp.GrinderSolution, Is.Not.Null); // Juicer has a valid solution.
        Assert.That(grinderComp.GrinderSolution.Value.Comp.Solution.Volume > 0f); // The banana has been juiced.

        grinderComp.GrinderSolution.Value.Comp.Solution.RemoveAllSolution(); // Clean the solution, since we check if its empty after next interaction.

        await InteractUsing(SteelSheet);

        Assert.That(grinderComp.GrinderSolution.Value.Comp.Solution.Volume == 0f); // The steel cannot be juiced.
    }
}
