using System.Text;
using CivSim.Sim.Economy;

namespace CivSim.Sim.View;

/// <summary>
/// The words for a Discovery work that is not running (docs/MVP/39 §8.1, A-4): the line the tile card shows when the player points at a town whose engine, oven,
/// factory, garden, society, exchange house, galleon or coal pit is dormant or blocked. It reads the frame's own state byte and reason code, so the text can never
/// disagree with the mark drawn on the map. Pure text; nothing here is stored or hashed.
/// </summary>
public static class DiscoveryWorkText
{
    /// <summary>The works' names in <see cref="DiscoveryFrame.Work"/> order.</summary>
    public static string WorkName(int kind) => kind switch
    {
        DiscoveryFrame.WorkMine => "Coal pit",
        DiscoveryFrame.WorkEngine => "Atmospheric engine",
        DiscoveryFrame.WorkOven => "Coke oven",
        DiscoveryFrame.WorkFactory => "Factory",
        DiscoveryFrame.WorkGarden => "Botanical garden",
        DiscoveryFrame.WorkSociety => "Learned society",
        DiscoveryFrame.WorkExchange => "Exchange house",
        DiscoveryFrame.WorkGalleon => "Galleon",
        _ => "Work",
    };

    /// <summary>Why a work is not running, in the words the build guidance uses for the same shortages (<see cref="BuildText"/>).</summary>
    public static string Reason(DiscoveryWorkReason reason) => reason switch
    {
        DiscoveryWorkReason.NoEngineer => "no engineer to tend it",
        DiscoveryWorkReason.NoCoal => "no coal in store",
        DiscoveryWorkReason.NoMiners => "no miners at the pit",
        DiscoveryWorkReason.Flooded => "flooded: an engine must pump it dry",
        DiscoveryWorkReason.NoOperatives => "no operatives to run it",
        DiscoveryWorkReason.NoPower => "no power reaches it (it needs a water mill, a windmill or a fired engine)",
        DiscoveryWorkReason.NoBotanist => "no botanist to tend it",
        DiscoveryWorkReason.NoScholar => "no scholars to gather",
        DiscoveryWorkReason.NoBroker => "no broker to run it",
        DiscoveryWorkReason.NoCrew => "no crew to sail her",
        DiscoveryWorkReason.NoHarbor => "it needs a harbor first",
        _ => "",
    };

    /// <summary>
    /// One line for the work packed in <paramref name="packed"/>, or an empty string when it is working, queued or not there. A dormant work reads "idle", a blocked one
    /// "stopped"; a coal pit an engine is draining reads "pumped dry" so a working pit says why it works.
    /// </summary>
    public static string Line(int kind, byte packed, bool pumped = false)
    {
        DiscoveryWorkState state = DiscoveryFrame.WorkStateOf(packed);
        if (state == DiscoveryWorkState.Working && kind == DiscoveryFrame.WorkMine && pumped) return "Coal pit · working, an engine keeps it pumped dry";
        if (state != DiscoveryWorkState.Dormant && state != DiscoveryWorkState.Blocked) return "";
        string why = Reason(DiscoveryFrame.WorkReasonOf(packed));
        string word = state == DiscoveryWorkState.Dormant ? "idle" : "stopped";
        return why.Length == 0 ? $"{WorkName(kind)} · {word}" : $"{WorkName(kind)} · {word}: {why}";
    }

    /// <summary>The lines for one own town (a frame settlement row), one per work that is idle or stopped plus the pumped pit; empty when all run or there are no works.</summary>
    public static string Lines(DiscoveryFrame d, int row)
    {
        if (!d.Active || row < 0 || (row + 1) * DiscoveryFrame.WorkKinds > d.Work.Length) return "";
        var text = new StringBuilder();
        for (int kind = 0; kind < DiscoveryFrame.WorkKinds; kind++)
        {
            string line = Line(kind, d.Work[row * DiscoveryFrame.WorkKinds + kind], row < d.Pumping.Length && d.Pumping[row] != 0);
            if (line.Length == 0) continue;
            if (text.Length > 0) text.Append('\n');
            text.Append(line);
        }
        return text.ToString();
    }
}
