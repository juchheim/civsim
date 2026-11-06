using System;
using System.Collections.Generic;
using CivSim.Game.Ui;
using CivSim.Sim.Economy;
using CivSim.Sim.Research;
using CivSim.Sim.View;
using Godot;

namespace CivSim.Game;

/// <summary>
/// The report that closes every age. When the viewer's people enter a new age, time pauses and this sheet shows where they stand
/// against the peoples they have met; Continue opens Projects (the first step of any age) and time stays paused until Projects
/// closes. The final age of a chapter keeps the end screen instead.
/// </summary>
public partial class Game
{
    private ModalView? _eraModal;
    private VBoxContainer? _eraContent;
    private bool _eraReportOpen, _eraWasRunning;

    /// <summary>What the last report saw, so the next one can say what changed during the age.</summary>
    private sealed class EraSnapshot
    {
        public long Tick;
        public int Projects, People, Settlements;
        public long Insight;
        public long[] Goods = Array.Empty<long>();
    }

    private EraSnapshot _eraSnap = new();

    private static readonly string[] WeaponNames = { "bare hands", "clubs", "spears", "fire-hardened spears", "atlatls", "bows" };

    private void BuildEraReport()
    {
        _eraModal = MakeModal("era", 1240, 700, "A new age", "", () => { ContinueFromEraReport(); return true; });
        _eraContent = UiKit.Column(UiTokens.Sp3);
        _eraModal.Body.AddChild(UiKit.Scroll(_eraContent));
        _eraModal.Actions.AddChild(UiKit.Primary("Continue to Projects", ContinueFromEraReport, null, UiGlyph.None, UiTokens.ControlM, true));
    }

    private void ResetEraReport()
    {
        _eraSnap = new EraSnapshot();
        _eraReportOpen = false;
        if (_eraModal is { Open: true }) CloseModal(_eraModal);
    }

    /// <summary>Pauses and shows the report when it applies. Returns false when it does not (replays, the chapter's last age).</summary>
    private bool TryShowEraReport(ViewEvent item, MatchFrame frame, int viewer)
    {
        if (_eraModal is null || _eraContent is null || _session is null || _eraReportOpen) return false;
        if (_session.IsReplay || _session.MatchEnded || _benchmarkSeconds != 0) return false;
        if (item.A >= (_session.IsIron ? 5 : 4)) return false;   // the end screen tells this one
        if (_researchWebVisible) CloseResearchWeb();
        _eraWasRunning = !_session.Paused;
        _session.SetPaused(true);
        BuildEraReportContent(item, frame, viewer);
        _eraReportOpen = true;
        OpenModal(_eraModal);
        return true;
    }

    private void ContinueFromEraReport()
    {
        if (!_eraReportOpen) return;
        _eraReportOpen = false;
        if (_eraModal is not null) CloseModal(_eraModal);
        // Projects is the first step of a new age. Time was paused for the report and stays paused until Projects closes.
        OpenResearchWeb(FirstOpenSlot());
        if (_eraWasRunning) _pausedByWeb = true;
    }

    private void BuildEraReportContent(ViewEvent item, MatchFrame frame, int viewer)
    {
        int era = item.A;
        int slots = MatchFrame.StoreSlots;
        long yearsTicks = Math.Max(0, item.Tick - _eraSnap.Tick);
        int years = Math.Max(1, (int)Math.Round(yearsTicks / 600.0));

        var met = new List<int>();
        int unmet = 0;
        for (int civ = 0; civ < frame.CivCount; civ++)
        {
            if (civ == viewer) continue;
            if (((CivViewFlags)frame.CivFlags[civ] & CivViewFlags.Contacted) != 0) met.Add(civ); else unmet++;
        }
        var order = new List<int>(met) { viewer };
        order.Sort((a, b) =>
        {
            int c = frame.CivLegacy[b].CompareTo(frame.CivLegacy[a]);
            return c != 0 ? c : a.CompareTo(b);
        });
        int rank = order.IndexOf(viewer) + 1;

        _eraModal!.Title.Text = $"The {UiText.AgeName(era)} age begins";
        string closedBy = item.Project >= 0 ? ProjectName(item.Project) : "a turning point";
        string standing = met.Count == 0
            ? "You have not met another people yet."
            : rank == 1 ? $"Your people lead the {met.Count + 1} peoples you know in Legacy."
            : $"Your people rank {rank} of {met.Count + 1} among the peoples you have met.";
        _eraModal.Subtitle.Text = $"Year {item.Tick / 600 + 1} · {closedBy} turned the age after {years} {(years == 1 ? "year" : "years")}. {standing}";
        _eraModal.Subtitle.Visible = true;

        foreach (Node child in _eraContent!.GetChildren()) { _eraContent.RemoveChild(child); child.QueueFree(); }

        // ---- standings --------------------------------------------------------------------------
        _eraContent.AddChild(UiKit.Text("STANDINGS", "EyebrowTertiary"));
        var grid = new GridContainer { Columns = 10 };
        grid.AddThemeConstantOverride("h_separation", UiTokens.Sp4);
        grid.AddThemeConstantOverride("v_separation", UiTokens.Sp1);
        foreach (string h in new[] { "", "AGE", "LEGACY", "THRIVING", "LAND", "PEOPLE", "TOWNS", "PROJECTS", "INSIGHT / YR", "TOP GOODS" })
            grid.AddChild(UiKit.Text(h, "EyebrowTertiary"));
        for (int row = 0; row < order.Count; row++)
        {
            int civ = order[row];
            bool me = civ == viewer;
            var who = UiKit.Row(UiTokens.Sp2);
            who.AddChild(UiKit.Text($"{row + 1}", "NumStrong", minWidth: 20));
            who.AddChild(new UiEmblem(civ, 26));
            who.AddChild(UiKit.Text(me ? "Your people" : UiText.CivName(civ), me ? "BodyStrong" : "Body", minWidth: 110));
            grid.AddChild(who);
            grid.AddChild(UiKit.Text(UiText.AgeName(frame.CivEra[civ]), "Body"));
            grid.AddChild(UiKit.Text(FormatFixed(frame.CivLegacy[civ]), "Num"));
            grid.AddChild(UiKit.Text(FormatFixed(frame.CivThrivingLegacy[civ]), "Num"));
            grid.AddChild(UiKit.Text($"{frame.CivLandBasisPoints[civ] / 100.0:F1}%", "Num"));
            grid.AddChild(UiKit.Text($"{frame.CivIntelPopulation[civ]:N0}", "Num"));
            grid.AddChild(UiKit.Text($"{frame.CivIntelSettlements[civ]}", "Num"));
            grid.AddChild(UiKit.Text($"{frame.CivIntelProjectsDone[civ]}", "Num"));
            grid.AddChild(UiKit.Text(UiText.FormatInsight(frame.CivIntelInsightPerYear[civ]), "Num"));
            grid.AddChild(UiKit.Text(TopGoodsText(frame, civ, slots), "Body", wrap: true, minWidth: 250));
        }
        // Peoples not yet met keep a row, with nothing about them revealed.
        for (int i = 0; i < unmet; i++)
        {
            grid.AddChild(UiKit.Text($"?   Not yet met", "BodySecondary"));
            for (int c = 1; c < 10; c++) grid.AddChild(UiKit.Text("—", "BodySecondary"));
        }
        _eraContent.AddChild(grid);
        _eraContent.AddChild(UiKit.Text("Legacy = points for knowledge mastered, land held and thriving people (fed and healthy). Top goods = what grew most in each people's stores this age (net of eating, burning and trading). Numbers for a people come from meeting them.", "CaptionTertiary", wrap: true));

        // ---- three columns ----------------------------------------------------------------------
        var columns = UiKit.Row(UiTokens.Sp4);
        columns.AddChild(EraColumn("THIS AGE", col =>
        {
            col.AddChild(UiKit.Text($"{years} {(years == 1 ? "year" : "years")} in the {UiText.AgeName(Math.Max(0, era - 1))} age", "Body", wrap: true));
            col.AddChild(UiKit.Text($"Turned by {closedBy}", "Body", wrap: true));
            int learned = frame.CivIntelProjectsDone[viewer] - _eraSnap.Projects;
            col.AddChild(UiKit.Text($"{learned} {(learned == 1 ? "project" : "projects")} learned", "Body"));
            col.AddChild(UiKit.Text($"Insight per year {UiText.FormatInsight(_eraSnap.Insight)} → {UiText.FormatInsight(frame.CivIntelInsightPerYear[viewer])}", "Body", wrap: true));
            col.AddChild(UiKit.Text($"Top goods: {TopGoodsText(frame, viewer, slots)}", "Body", wrap: true));
        }));
        columns.AddChild(EraColumn("YOUR PEOPLE NOW", col =>
        {
            int people = frame.CivIntelPopulation[viewer], towns = frame.CivIntelSettlements[viewer];
            col.AddChild(UiKit.Text($"{people:N0} people ({Delta(people - _eraSnap.People)})", "Body"));
            col.AddChild(UiKit.Text($"{towns} {(towns == 1 ? "settlement" : "settlements")} ({Delta(towns - _eraSnap.Settlements)})", "Body"));
            int largest = 0;
            double rations = 0;
            for (int i = 0; i < frame.SettlementCount; i++)
            {
                SettlementViewFlags flags = (SettlementViewFlags)frame.SettlementFlags[i];
                if (frame.SettlementCiv[i] != viewer || (flags & (SettlementViewFlags.Remembered | SettlementViewFlags.FreeFolk)) != 0) continue;
                largest = Math.Max(largest, frame.SettlementPopulation[i]);
                rations += frame.SettlementFoodStores[i] / 4294967296.0;
            }
            col.AddChild(UiKit.Text($"Largest settlement {largest:N0}", "Body"));
            col.AddChild(UiKit.Text($"{rations:N0} {(Math.Round(rations) == 1 ? "ration" : "rations")} in store", "Body"));
            col.AddChild(UiKit.Text($"Land held {frame.CivLandBasisPoints[viewer] / 100.0:F1}%", "Body"));
        }));
        columns.AddChild(EraColumn("DIPLOMACY & MILITARY", col =>
        {
            col.AddChild(UiKit.Text($"Your people carry {WeaponNames[Math.Min(frame.CivWeaponTier[viewer], WeaponNames.Length - 1)]}", "Body", wrap: true));
            if (met.Count == 0) col.AddChild(UiKit.Text("No other people met yet.", "BodySecondary"));
            foreach (int civ in met)
            {
                var head = UiKit.Row(UiTokens.Sp2);
                head.AddChild(new UiEmblem(civ, 20));
                head.AddChild(UiKit.Text(UiText.CivName(civ), "BodyStrong", ellipsis: true));
                col.AddChild(head);
                col.AddChild(UiKit.Text($"{EraDiploState(frame, civ)} · {WeaponNames[Math.Min(frame.CivWeaponTier[civ], WeaponNames.Length - 1)]}", "Caption", wrap: true));
            }
            if (unmet > 0) col.AddChild(UiKit.Text($"{unmet} {(unmet == 1 ? "people" : "peoples")} not yet met", "CaptionTertiary"));
        }));
        _eraContent.AddChild(columns);

        // ---- next age ---------------------------------------------------------------------------
        _eraContent.AddChild(UiKit.Text($"THE {UiText.AgeName(era).ToUpperInvariant()} AGE AHEAD", "EyebrowTertiary"));
        var ready = new List<string>();
        int more = 0;
        if (_catalog is not null)
            foreach (ProjectDef def in _catalog.Projects)
            {
                if (def.Era != era || !def.Numeric || (ProjectState)StateOfFrame(frame, def.Index) != ProjectState.Available) continue;
                if (ready.Count < 8) ready.Add(def.Name); else more++;
            }
        _eraContent.AddChild(UiKit.Text(ready.Count == 0
            ? "New projects will open as your people learn the ones before them."
            : $"Ready to learn: {string.Join(", ", ready)}{(more > 0 ? $" and {more} more" : "")}.", "Body", wrap: true));
        _eraContent.AddChild(UiKit.Text("Continue opens Projects. Time stays paused until you close it.", "CaptionTertiary", wrap: true));

        // ---- remember this report for the next one ------------------------------------------------
        _eraSnap = new EraSnapshot
        {
            Tick = item.Tick,
            Projects = frame.CivIntelProjectsDone[viewer],
            People = frame.CivIntelPopulation[viewer],
            Settlements = frame.CivIntelSettlements[viewer],
            Insight = frame.CivIntelInsightPerYear[viewer],
            Goods = (long[])frame.CivGoodsGained.Clone(),
        };
    }

    private static byte StateOfFrame(MatchFrame frame, int project)
        => (uint)project < (uint)frame.FocusProjectCount ? frame.FocusProjectState[project] : (byte)ProjectState.Hidden;

    private static VBoxContainer EraColumn(string heading, Action<VBoxContainer> fill)
    {
        var col = UiKit.Column(UiTokens.Sp1);
        col.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        col.AddChild(UiKit.Text(heading, "EyebrowTertiary"));
        fill(col);
        return col;
    }

    private static string Delta(int d) => d >= 0 ? $"+{d}" : $"−{-d}";

    /// <summary>The three goods that grew most in a civilization's stores since the last report, by name.</summary>
    private string TopGoodsText(MatchFrame frame, int civ, int slots)
    {
        var best = new List<(long Gain, int Slot)>();
        long[] before = _eraSnap.Goods;
        for (int slot = 0; slot < slots; slot++)
        {
            int at = civ * slots + slot;
            if (at >= frame.CivGoodsGained.Length) break;
            long gain = frame.CivGoodsGained[at] - (at < before.Length ? before[at] : 0);
            if (gain > 0) best.Add((gain, slot));
        }
        if (best.Count == 0) return "—";
        best.Sort((a, b) => { int c = b.Gain.CompareTo(a.Gain); return c != 0 ? c : a.Slot.CompareTo(b.Slot); });
        var names = new List<string>();
        for (int i = 0; i < Math.Min(3, best.Count); i++) names.Add(UiText.GoodName(LivingEconomyModel.StoreSlotId(best[i].Slot)));
        return string.Join(", ", names);
    }

    private string EraDiploState(MatchFrame frame, int civ)
    {
        CivViewFlags f = (CivViewFlags)frame.CivFlags[civ];
        string state = (f & CivViewFlags.AtWar) != 0 ? "At war" : (f & CivViewFlags.Allied) != 0 ? "Allied" : (f & CivViewFlags.InTruce) != 0 ? "Truce"
            : RelationWord(frame.CivRelations[civ]);
        if ((f & CivViewFlags.Pact) != 0) state += " · pact";
        if ((f & CivViewFlags.Treaty) != 0) state += " · border treaty";
        if (frame.CivEmbargoByViewer[civ] != 0) state += " · you embargo them";
        if (frame.CivEmbargoOnViewer[civ] != 0) state += " · they embargo you";
        return state;
    }
}
