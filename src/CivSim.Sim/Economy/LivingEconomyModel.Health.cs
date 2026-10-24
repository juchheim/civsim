using System;
using CivSim.Sim.Core;
using CivSim.Sim.Numerics;
using CivSim.Sim.Randomness;
using CivSim.Sim.World;

namespace CivSim.Sim.Economy;

/// <summary>Epidemic classes of docs/MVP/09 §10.2. Values are stable (snapshots store them).</summary>
public enum OutbreakClass : byte { None = 0, Fever = 1, Flux = 2, Pox = 3, Plague = 4 }

/// <summary>
/// Health at E3 scale (WP7, docs/MVP/22 §10, 09 §10): for settlements of a civilization past KEY-3 the E2 epidemic cause budget
/// is replaced by discrete outbreaks (density pressure × (1 − Sanitation) × (1 + inflow), SIR-lite final size, two seasons),
/// with Plague possible from KEY-3 and imported along routes through <see cref="ImportOutbreak"/> (WP8's caravans and compacts
/// call it). Soap gives Sanitation (CHM-05), GOV-04 halves local famine deaths.
/// </summary>
public sealed partial class LivingEconomyModel
{
    private const int OutbreakClasses = 5;
    private static readonly ulong OutbreakStream = Hash64.OfString("economy.outbreak");
    private static readonly ulong InfectionStream = Hash64.OfString("economy.infection");
    private static readonly Fix64 Log2E = Fix64.Parse("1.442695040888963");

    // docs/MVP/09 §10.2: R0, case fatality and seasons of immunity by class (Fever, Flux, Pox, Plague).
    private static readonly Fix64[] OutbreakR0 = { Fix64.Zero, Fix64.FromRatio(3, 2), Fix64.FromInt(2), Fix64.FromInt(4), Fix64.FromInt(2) };
    private static readonly Fix64[] OutbreakCfr = { Fix64.Zero, Fix64.FromRatio(3, 100), Fix64.FromRatio(8, 100), Fix64.FromRatio(15, 100), Fix64.FromRatio(35, 100) };
    private static readonly byte[] ImmunitySeasons = { 0, 8, 8, 255, 0 };
    // E3 class weights (per cent): between 09's E2 (fever 60, flux 30, pox 10) and E4 (35/30/25 and plague 10) columns; pox is
    // endemic only in settlements of 1,000 (none at 1 : 1 in E3), so it arrives by import only and its weight goes to fever.
    private static readonly int[] E3ClassWeights = { 0, 55, 35, 0, 10 };

    private Fix64 R0Of(int cls) => cls == (int)OutbreakClass.Plague ? _civic.PlagueR0 : OutbreakR0[cls];
    private Fix64 CfrOf(int cls) => cls == (int)OutbreakClass.Plague ? _civic.PlagueCfr : OutbreakCfr[cls];

    /// <summary>Sanitation (09 §10.1): 1 − Π(1 − s) over completed `sanitation` effects; soap (CHM-05) counts in full only where a
    /// settlement holds a cake per 20 people, which it uses up each season, else at half.</summary>
    private Fix64 ComputeSanitation(SettlementState s, int pop)
    {
        Fix64 keep = Fix64.One;
        bool anySoap = false;
        foreach (EconomyEffect e in _content.Effects)
            if (e.Kind == "sanitation" && e.Value is { } v && _completedProjects[s.Civilization][e.ProjectIndex]) { keep *= Fix64.One - v; anySoap = true; }
        if (!anySoap) return Fix64.Zero;
        Fix64 full = Fix64.One - keep;
        Fix64 soapNeed = Fix64.FromInt(Math.Max(1, (pop + 19) / 20));
        if (s.Metal is { } m && m.Goods[(int)MetalGood.Soap] >= soapNeed)
        {
            m.Goods[(int)MetalGood.Soap] -= soapNeed;
            return full;
        }
        return full * Fix64.Half;
    }

    /// <summary>The SIR final size z = 1 − e^(−R z) (09 §10.3), by fixed-point iteration (deterministic).</summary>
    internal static Fix64 FinalSize(Fix64 r)
    {
        if (r <= Fix64.One) return Fix64.Zero;
        Fix64 z = Fix64.One;
        for (int i = 0; i < 48; i++)
        {
            Fix64 x = r * z * Log2E;
            Fix64 next = Fix64.One - FixMath.Exp2(-x);
            if (next == z) break;
            z = next;
        }
        return Fix64.Clamp(z, Fix64.Zero, Fix64.One);
    }

    private Fix64 DensityPressure(SettlementState s, CivicState c, int pop)
    {
        // ρ = (people ÷ built tiles) ÷ 50; a settlement is one built tile at game scale (09 §10.1: village 0.8, town 1.2, city 1.8).
        Fix64 rho = Fix64.FromInt(pop) / 50;
        Fix64 inflow = Fix64.Clamp(_civic.Inflow + (TradeRoutes?.InflowAt(s.Id) ?? Fix64.Zero), Fix64.Zero, Fix64.One);
        return rho * (Fix64.One - c.Sanitation) * (Fix64.One + inflow);
    }

    /// <summary>Starts, advances and spreads outbreaks at the season start, and marks this season's infected figures.</summary>
    private void StartOutbreaks(int season)
    {
        int n = _settlements.Count;
        var activeBefore = new byte[n];
        for (int i = 0; i < n; i++) activeBefore[i] = _settlements[i].Civic is { } ac ? ac.Outbreak : (byte)0;
        foreach (FigureState f in _figures) f.Infected = false;
        for (int i = 0; i < n; i++)
        {
            SettlementState s = _settlements[i];
            if (s.Civic is not { } c || !CohesionLive(s)) continue;
            int pop = Population(s.Id);
            if (pop == 0) { c.Outbreak = 0; continue; }
            for (int k = 1; k < OutbreakClasses; k++)
                if (c.ImmuneSeasons[k] > 0 && c.ImmuneSeasons[k] < 255 && --c.ImmuneSeasons[k] == 0) c.Immune[k] = Fix64.Zero;
            c.Sanitation = ComputeSanitation(s, pop);
            if (c.Outbreak != 0)
            {
                if (c.OutbreakSeason == 1) c.OutbreakSeason = 2;
                else
                {
                    int k = c.Outbreak;
                    if (ImmunitySeasons[k] > 0)
                    {
                        c.Immune[k] = Fix64.Max(c.Immune[k], c.AttackRate);
                        c.ImmuneSeasons[k] = ImmunitySeasons[k];
                    }
                    c.Outbreak = 0;
                    c.OutbreakSeason = 0;
                }
            }
            if (c.Outbreak == 0)
            {
                Fix64 pressure = DensityPressure(s, c, pop);
                Pcg32 rng = Pcg32.ForStream(_seed, OutbreakStream, (ulong)(uint)s.Id, season);
                int cls = 0;
                if (c.ImportedOutbreak != 0) { cls = c.ImportedOutbreak; c.ImportedOutbreak = 0; }
                else
                {
                    // Spread from a sister settlement with an outbreak (09 §10.3: 0.25 × link traffic; own villages trade at 0.2).
                    int sister = 0;
                    int reach = Math.Max(0, ShareReach(s.Civilization));
                    for (int o = 0; o < n && sister == 0; o++)
                        if (o != i && activeBefore[o] != 0 && _settlements[o].Civilization == s.Civilization &&
                            HexGrid.Distance(_settlements[o].SiteTile, s.SiteTile, _world.Width) <= Math.Max(reach, 15)) sister = activeBefore[o];
                    Fix64 roll = rng.NextFix01();
                    if (sister != 0 && roll < Fix64.FromRatio(5, 100)) cls = sister;
                    else if (roll < Fix64.Min(Fix64.FromRatio(1, 4), Fix64.FromRatio(1, 100) * pressure))
                    {
                        uint pick = rng.NextBelow(100);
                        int acc = 0;
                        for (int k = 1; k < OutbreakClasses; k++)
                        {
                            acc += E3ClassWeights[k];
                            if (pick < acc) { cls = k; break; }
                        }
                    }
                }
                if (cls != 0)
                {
                    Fix64 r0 = R0Of(cls);
                    if (cls == (int)OutbreakClass.Flux) r0 *= Fix64.One - c.Sanitation;
                    Fix64 reff = r0 * (Fix64.One - c.Immune[cls]) * Fix64.Sqrt(Fix64.Min(Fix64.FromRatio(3, 2), pressure));
                    Fix64 z = FinalSize(reff);
                    if (z > Fix64.Zero) { c.Outbreak = (byte)cls; c.OutbreakSeason = 1; c.AttackRate = z; c.CfrScale = AgeCfrNorm(s); }
                }
            }
            if (c.Outbreak == 0) continue;
            // 60% of cases in the first season, 40% in the second (09 §10.3); the sick rest and are paler.
            Fix64 p = c.AttackRate * (c.OutbreakSeason == 1 ? Fix64.FromRatio(6, 10) : Fix64.FromRatio(4, 10));
            foreach (FigureState f in _figures)
            {
                if (f.SettlementId != s.Id) continue;
                if (Pcg32.ForStream(_seed, InfectionStream, (ulong)f.Id, season).NextFix01() >= p) continue;
                f.Infected = true;
                if (f.IllnessSeasons == 0) f.IllnessSeasons = 1;
            }
        }
    }

    /// <summary>The epidemic hazard of an infected figure this season (09 §10.3): CFR × age (children ×1.5, elders ×2) × MED.</summary>
    private Fix64 InfectedHazard(FigureState figure)
    {
        if (!figure.Infected || _settlements[figure.SettlementId].Civic is not { Outbreak: > 0 } c) return Fix64.Zero;
        Fix64 h = CfrOf(c.Outbreak);
        // Deaths fall 1.5× on children and 2× on elders relative to adults (09 §10.3), keeping the settlement's mean at the CFR.
        if (figure.Age < 14) h *= Fix64.FromRatio(3, 2);
        else if (figure.Age >= 45) h *= Fix64.FromInt(2);
        h *= c.CfrScale;
        if (HasProject(figure.Civilization, "MED-01")) h *= Fix64.FromRatio(9, 10);
        return h;
    }

    /// <summary>1 ÷ the mean age factor of a settlement's people (children 1.5, adults 1, elders 2).</summary>
    private Fix64 AgeCfrNorm(SettlementState s)
    {
        Fix64 sum = Fix64.Zero;
        int n = 0;
        foreach (FigureState f in _figures)
        {
            if (f.SettlementId != s.Id) continue;
            n++;
            sum += f.Age < 14 ? Fix64.FromRatio(3, 2) : f.Age >= 45 ? Fix64.FromInt(2) : Fix64.One;
        }
        return n == 0 || sum <= Fix64.Zero ? Fix64.One : Fix64.FromInt(n) / sum;
    }

    /// <summary>Whether the figure's settlement runs on the E3 outbreak model (its continuous epidemic budget is then zero).</summary>
    private bool DiscreteEpidemics(FigureState figure)
        => (uint)figure.SettlementId < (uint)_settlements.Count && _settlements[figure.SettlementId].Civic is not null &&
           CivInBronze(_settlements[figure.SettlementId].Civilization);

    /// <summary>
    /// A disease arrives from outside (docs/MVP/22 §10: compacts and caravans carry plague; WP8 calls this when a route delivers
    /// from a settlement with an outbreak). It takes hold at the next season start if no outbreak is running there.
    /// </summary>
    public void ImportOutbreak(int settlementIndex, OutbreakClass cls)
    {
        SettlementState s = _settlements[settlementIndex];
        if (cls == OutbreakClass.None || !CivInBronze(s.Civilization)) return;
        EnsureCivic(s).ImportedOutbreak = (byte)cls;
    }

    /// <summary>The outbreak running at a settlement, if any, and its season (1 or 2): what a caravan leaving it carries.</summary>
    public (OutbreakClass Class, int Season) OutbreakAt(int settlementIndex)
        => _settlements[settlementIndex].Civic is { Outbreak: > 0 } c ? ((OutbreakClass)c.Outbreak, c.OutbreakSeason) : (OutbreakClass.None, 0);
}
