using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;

namespace CivSim.Harness;

internal readonly record struct FiefSiegeTerminalExposure(
    int Settlement, int Attacker, int Defender, Fix64 Integrity, bool PowderBreach, string Outcome, string CensorReason);

internal static class FiefSiegeTerminalTrace
{
    public static void Append(StringBuilder output, ulong seed, int season, LivingEconomyModel economy,
        IEnumerable<FiefSiegeTerminalExposure> exposures)
    {
        FiefSiegeTerminalExposure[] rows = exposures.OrderBy(row => row.Settlement).ToArray();
        if (rows.Length == 0)
        {
            output.AppendLine($"fief-siege-terminal seed={seed} season={season} exposure=none reason=no_siege_investment_observed");
            return;
        }

        output.AppendLine($"fief-siege-terminal seed={seed} season={season} exposure=observed settlements={rows.Length}");
        for (int civ = 0; civ < economy.CivilizationCount; civ++)
        {
            Fix64 cannons = Fix64.Zero, powder = Fix64.Zero;
            for (int settlement = 0; settlement < economy.SettlementCount; settlement++)
            {
                if (economy.SettlementLite(settlement).Civilization != civ) continue;
                cannons += economy.GoodStock(settlement, "G-CANNON");
                powder += economy.GoodStock(settlement, "G-POWDER");
            }
            output.AppendLine($"fief-siege-terminal-inventory seed={seed} civ={civ} cannonStock={F(cannons)} powderStock={F(powder)}");
        }

        int wallsKind = LivingEconomyModel.StructureIndex("stone_walls");
        int gatehouseKind = IronWorkIndex(economy, "gatehouse");
        int castleKind = IronWorkIndex(economy, "castle");
        int concentricKind = IronWorkIndex(economy, "concentric_castle");
        foreach (FiefSiegeTerminalExposure row in rows)
        {
            bool walls = wallsKind >= 0 && economy.StructureCount(row.Settlement, wallsKind) > 0;
            bool gatehouse = gatehouseKind >= 0 && economy.IronWorkAt(row.Settlement, gatehouseKind) > 0;
            bool castle = castleKind >= 0 && economy.IronWorkAt(row.Settlement, castleKind) > 0;
            bool concentric = concentricKind >= 0 && economy.IronWorkAt(row.Settlement, concentricKind) > 0;
            bool garrisoned = castle && economy.CastleGarrisoned(row.Settlement);
            output.AppendLine($"fief-siege-terminal-settlement seed={seed} settlement={row.Settlement} attacker={row.Attacker} defender={row.Defender} stoneWalls={B(walls)} gatehouse={B(gatehouse)} castle={B(castle)} castleGarrisoned={B(garrisoned)} concentricCastle={B(concentric)} integrity={F(row.Integrity)} powderBreach={B(row.PowderBreach)} outcome={row.Outcome} censorReason={row.CensorReason}");
        }
    }

    private static int IronWorkIndex(LivingEconomyModel economy, string id)
    {
        for (int kind = 0; kind < LivingEconomyModel.IronWorkKindCount; kind++)
            if (LivingEconomyModel.IronWorkId(kind) == id) return kind;
        return -1;
    }

    private static string F(Fix64 value) => value.ToDoubleForDisplay().ToString("F2", CultureInfo.InvariantCulture);
    private static string B(bool value) => value ? "true" : "false";
}
