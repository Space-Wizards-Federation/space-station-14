using System.Linq;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Kitchen;
using Content.Shared.Kitchen.Components;
using Content.Shared.Kitchen.EntitySystems;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Kitchen;

public sealed class HandheldGrinderInteractionTest : InteractionTest
{
    private static readonly EntProtoId Mortar = "MortarAndPestle";
    private static readonly EntProtoId Juicer = "HandheldJuicer";

    private static readonly EntProtoId SteelSheet = "SheetSteel1";
    private static readonly string Banana = "TestFoodBanana";

    [TestPrototypes]
    private const string Prototypes = @"
# A modified banana that can only be juiced.
- type: entity
  parent: FoodBanana
  id: TestFoodBanana
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
        var grinderSys = SEntMan.System<SharedReagentGrinderSystem>();
        var solutionSys = SEntMan.System<SharedSolutionContainerSystem>();

        // Spawn an empty mortar
        await SpawnTarget(Mortar);
        var grinderComp = Comp<HandheldGrinderComponent>();

        // Spawn steel sheets and get what solution they should grind into.
        var sheetsEnt = await Spawn(SteelSheet);
        var expectedGrinderSol = grinderSys.GetGrinderSolution(ToServer(sheetsEnt), GrinderProgram.Grind);

        await Pickup(sheetsEnt);
        await Interact();

        Assert.That(grinderComp.GrinderSolution, Is.Not.Null); // The grinder needs to have its valid solution resolved after interaction.
        Assert.That(expectedGrinderSol!.Contents.SequenceEqual(grinderComp.GrinderSolution.Value.Comp.Solution.Contents)); // Check if the solution is the one we expected.


        // Spawn a new grinder
        await SpawnTarget(Mortar);
        grinderComp = Comp<HandheldGrinderComponent>();
        // Manually resolve the solution because the system only resolves it after a VALID interaction, and here we test an invalid one.
        solutionSys.ResolveSolution(STarget.Value, grinderComp.SolutionName, ref grinderComp.GrinderSolution);

        await InteractUsing(Banana);

        Assert.That(grinderComp.GrinderSolution!.Value.Comp.Solution.Volume == 0f); // The banana shouldn't have been grinded, since it can only be juiced.


        // Now we test the juicer, so we spawn one.
        await SpawnTarget(Juicer);
        grinderComp = Comp<HandheldGrinderComponent>();
        var bananaEnt = await Spawn(Banana);
        var expectedJuicerSol = grinderSys.GetGrinderSolution(ToServer(bananaEnt), GrinderProgram.Juice);

        await Pickup(bananaEnt);
        await Interact();

        Assert.That(grinderComp.GrinderSolution, Is.Not.Null); // Juicer has a valid solution.
        Assert.That(expectedJuicerSol!.Contents.SequenceEqual(grinderComp.GrinderSolution.Value.Comp.Solution.Contents)); // The banana has been juiced.


        // Spawn a new juicer
        await SpawnTarget(Juicer);
        grinderComp = Comp<HandheldGrinderComponent>();
        // Manually resolve the solution because the system only resolves it after a VALID interaction, and here we test an invalid one.
        solutionSys.ResolveSolution(STarget.Value, grinderComp.SolutionName, ref grinderComp.GrinderSolution);

        await InteractUsing(SteelSheet);

        Assert.That(grinderComp.GrinderSolution!.Value.Comp.Solution.Volume == 0f); // The steel cannot be juiced.
    }
}
