using System.IO;
using CivSim.Sim.Economy;
using CivSim.Sim.World;

namespace CivSim.Tests;

/// <summary>
/// Tool durability depends on the owner's research. A settlement captured by a civilization with shorter-lived tools
/// must not keep more work on its stock than the new owner's durability allows, or a save made at that moment fails
/// the snapshot validator ("Snapshot contains invalid tool durability"). Found in the Strategist fixture (seed
/// 20261019), where civilization 3 completed Birch-Tar Adhesive and lost the settlement to civilization 2 in one season.
/// </summary>
public class CapturedToolWorkTests
{
    private const ulong Seed = 20261348;

    [Fact]
    public void A_captured_settlements_tools_are_capped_at_the_captors_durability_and_the_snapshot_reloads()
    {
        GeneratedWorld generated = WorldGenerator.Generate(Seed);
        WorldEconomyInput world = WorldEconomyInput.FromGeneratedWorld(generated);
        var economy = new LivingEconomyModel(Seed, EconomyContent.FromPack(TestPack.Pack), world);

        // Civ 0 has the wooden-tool (FIR-04, x1.5) and hafted-tool (CHM-02, x1.4) bonuses: a spear lasts 5 * 1.5 * 1.4 = 10.5.
        economy.FixtureComplete(0, "FIR-04", "CHM-02");
        economy.FixtureStockTool(0, "G-SPEAR", 2);
        economy.TransferSettlement(0, 1); // civ 1 has neither: a spear lasts 5.

        var restored = new LivingEconomyModel(Seed, EconomyContent.FromPack(TestPack.Pack), world);
        using var snapshot = new MemoryStream();
        using (var writer = new BinaryWriter(snapshot, System.Text.Encoding.UTF8, leaveOpen: true)) economy.WriteSnapshot(writer);
        snapshot.Position = 0;
        using (var reader = new BinaryReader(snapshot)) restored.ReadSnapshot(reader);
        Assert.Equal(economy.ComputeChecksum(), restored.ComputeChecksum());
    }
}
