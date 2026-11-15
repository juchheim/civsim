using CivSim.Sim.Politics;
using CivSim.Sim.Research;

namespace CivSim.Sim.View;

public sealed partial class FrameBuilder
{
    private static readonly (string Project, int Bit)[] ArmsResearchFlags =
    {
        ("WAR-04", ArmsFrame.HasWarriors), ("WAR-05", ArmsFrame.HasBronzeArms), ("WAR-06", ArmsFrame.HasChariots), ("WAR-07", ArmsFrame.HasProfessionals),
        ("WAR-08", ArmsFrame.HasIronArms), ("WAR-09", ArmsFrame.HasEngines), ("HNT-05", ArmsFrame.HasAtlatl), ("HNT-06", ArmsFrame.HasBow),
        ("WAR-03", ArmsFrame.HasArchery), ("MET-10", ArmsFrame.HasSteel),
    };

    /// <summary>The viewer's arms (<see cref="ArmsFrame"/>). Always filled for own settlements, in any chapter; the metal rows are zero before the chapter that makes them.</summary>
    private void FillArms(MatchModel match, MatchFrame target)
    {
        ArmsFrame f = target.Arms;
        PoliticsSystem pol = match.Politics;
        int n = target.SettlementCount;
        f.Ensure(n);
        f.RowCount = n;
        f.Total = default;
        f.Research = 0;
        f.BaseTier = 0; f.BaseHundredths = 60;
        if (_viewer < 0 || _viewer >= _civs)
        {
            for (int w = 0; w < n; w++) f.Rows[w] = default;
            return;
        }
        f.BaseTier = pol.BaseWeaponTier(_viewer);
        f.BaseHundredths = pol.BaseWeaponHundredths(_viewer);
        foreach ((string project, int bit) in ArmsResearchFlags)
            if (ProjectDone(match.Research, _catalog.IndexOf(project))) f.Research |= bit;

        int fighters = 0, wounded = 0, warriors = 0, soldiers = 0, sSteel = 0, sIron = 0, sTin = 0, sArs = 0, sCu = 0,
            steel = 0, iron = 0, tin = 0, ars = 0, cu = 0, chariots = 0, crew = 0, engines = 0, strength = 0;
        for (int w = 0; w < n; w++)
        {
            int s = target.SettlementIndex[w];
            SettlementViewFlags flagsOf = (SettlementViewFlags)target.SettlementFlags[w];
            bool own = (flagsOf & (SettlementViewFlags.Own | SettlementViewFlags.Remembered | SettlementViewFlags.FreeFolk)) == SettlementViewFlags.Own;
            if (!own || (uint)s >= (uint)_settlementCount) { f.Rows[w] = default; continue; }
            SettlementArms a = pol.ArmsOf(s);
            f.Rows[w] = a;
            fighters += a.Fighters; wounded += a.Wounded; warriors += a.Warriors; soldiers += a.Soldiers;
            sSteel += a.StockSteel; sIron += a.StockIron; sTin += a.StockTin; sArs += a.StockArsenical; sCu += a.StockCopper;
            steel += a.Steel; iron += a.Iron; tin += a.Tin; ars += a.Arsenical; cu += a.Copper;
            chariots += a.Chariots; crew += a.ChariotCrew; engines += a.Engines; strength += a.AttackStrength;
        }
        f.Total = new SettlementArms(fighters, wounded, warriors, soldiers, sSteel, sIron, sTin, sArs, sCu, steel, iron, tin, ars, cu, chariots, crew, engines, strength);
    }
}
