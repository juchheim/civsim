using System;
using System.Collections.Generic;
using CivSim.Sim.Content;
using CivSim.Sim.Core;

namespace CivSim.Sim.Research;

/// <summary>
/// A scripted "engaged player": follows one of the validated openings of docs/MVP/04 §7 (the openings registry),
/// putting the earliest unfinished step that is Available into any empty focus slot, and handing over to the
/// Council when the script runs out. The commands it returns are ordinary commands, so a match it plays is a
/// replay like any other. M6's scripted-bot harness builds on this.
/// </summary>
public sealed class OpeningBot
{
    private readonly int[] _steps;

    public OpeningBot(ResearchCatalog catalog, string openingId)
    {
        foreach (ContentValue o in catalog.Pack.Root.Get("registries")!.Get("openings")!.Items)
        {
            if (o.GetString("id") != openingId) continue;
            var steps = new List<int>();
            foreach (string id in o.GetStringList("steps")) steps.Add(catalog.IndexOf(id));
            _steps = steps.ToArray();
            return;
        }
        throw new ArgumentException($"No opening '{openingId}'.", nameof(openingId));
    }

    public IReadOnlyList<int> Steps => _steps;

    /// <summary>True once the script is finished and the Council has been switched back on.</summary>
    public bool HandedOver { get; private set; }

    /// <summary>The commands this player issues for <paramref name="civ"/> now (call before the tick's step).</summary>
    public void Decide(ResearchSystem research, int civ, long tick, List<Command> into)
    {
        if (HandedOver) return;
        bool finished = true;
        foreach (int step in _steps) if (!research.IsDone(civ, step)) { finished = false; break; }
        if (finished)
        {
            into.Add(new Command(tick, (byte)civ, CommandKind.SetCouncil, 1, 0, 0));
            HandedOver = true;
            return;
        }
        for (int slot = 0; slot < CivResearch.SlotCount; slot++)
        {
            if (research.Slot(civ, slot) >= 0) continue;
            foreach (int step in _steps)
            {
                if (research.IsDone(civ, step) || research.State(civ, step) != ProjectState.Available) continue;
                // Several slots may be empty at once: skip steps another command in this batch already took.
                bool taken = false;
                foreach (Command c in into) if (c.Civ == civ && c.Kind == CommandKind.SetSlot && c.B == step) { taken = true; break; }
                if (taken) continue;
                into.Add(new Command(tick, (byte)civ, CommandKind.SetSlot, slot, step, 0));
                break;
            }
        }
    }
}
