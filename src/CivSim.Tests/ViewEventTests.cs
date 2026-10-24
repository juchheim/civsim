using System;
using System.Collections.Generic;
using System.Linq;
using CivSim.Sim.Research;
using CivSim.Sim.View;

namespace CivSim.Tests;

/// <summary>The event stream (docs/MVP/18 §6): ordered, bounded, filtered for the viewer, with text keys and Moment hooks.</summary>
public class ViewEventTests
{
    [Fact]
    public void Events_arrive_in_order_without_gaps_and_the_ring_is_bounded()
    {
        WatchedMatch w = ViewKit.Watched.Value;
        Assert.Empty(w.Violations);
        Assert.NotEmpty(w.AllViewerEvents);
        for (int i = 1; i < w.AllViewerEvents.Count; i++)
        {
            Assert.Equal(w.AllViewerEvents[i - 1].Sequence + 1, w.AllViewerEvents[i].Sequence);
            Assert.True(w.AllViewerEvents[i].Tick >= w.AllViewerEvents[i - 1].Tick);
        }
        Assert.Equal(w.Viewer.LatestEventSequence, w.AllViewerEvents[^1].Sequence);

        // The small ring keeps only its capacity, and the frame only its window: the newest, the same as the big ring's.
        Assert.Equal(64, w.SmallRing.EventCount);
        Assert.Equal(16, w.SmallFrame.EventCount);
        Assert.Equal(w.Viewer.LatestEventSequence, w.SmallRing.LatestEventSequence);
        for (int i = 0; i < w.SmallRing.EventCount; i++)
        {
            ViewEvent small = w.SmallRing.Event(i);
            ViewEvent big = w.Viewer.Event(w.Viewer.EventCount - w.SmallRing.EventCount + i);
            Assert.Equal(ViewKit.HashEvent(big), ViewKit.HashEvent(small));
        }
        Assert.Equal(w.SmallRing.Event(63).Sequence, w.SmallFrame.Events[15].Sequence);
        Assert.Equal(FrameBuilder.DefaultEventWindow, w.ViewerFrame.EventCount);
    }

    [Fact]
    public void Events_cover_the_chronicle_and_carry_text_keys_and_subjects()
    {
        WatchedMatch w = ViewKit.Watched.Value;
        ResearchCatalog catalog = w.Match.Research.Catalog;
        var kinds = w.AllViewerEvents.Select(e => e.Kind).ToHashSet();
        foreach (ViewEventKind expected in new[]
                 {
                     ViewEventKind.SeasonBegan, ViewEventKind.Births, ViewEventKind.Deaths, ViewEventKind.StructureBuilt,
                     ViewEventKind.SlotFilled, ViewEventKind.ProjectCompleted, ViewEventKind.Moment, ViewEventKind.VisibleChange,
                     ViewEventKind.FirstClaimed, ViewEventKind.EraAdvanced, ViewEventKind.SettlementFounded,
                 })
            Assert.Contains(expected, kinds);
        foreach (ViewEvent e in w.AllViewerEvents)
        {
            Assert.NotEqual(ViewEventKind.None, e.Kind);
            Assert.False(string.IsNullOrEmpty(ViewText.Key(e.Text)), $"{e.Kind} has no text key");
            // Only public news of other civilizations, or news that involves the viewer.
            bool ownRecord = e.Civ == 0 || e.Civ == -1 && e.Kind == ViewEventKind.SeasonBegan;
            bool publicNews = e.Kind is ViewEventKind.FirstClaimed or ViewEventKind.EraAdvanced;
            Assert.True(ownRecord || publicNews || e.Other == 0, $"{e.Kind} of civ {e.Civ} leaked to viewer 0");
            if (e.Kind == ViewEventKind.ProjectCompleted)
            {
                Assert.True(e.Project >= 0);
                Assert.True(e.Tile >= 0, "a completion should say where the innovator lives");
            }
            if (e.Kind == ViewEventKind.Moment && e.A == 1)
            {
                ProjectDef def = catalog.Projects[e.Project];
                Assert.True(!def.Numeric || def.Moment.Length > 0, $"{def.Id} has a Moment event but no Moment text");
            }
        }
        // Each in-era completion of the viewer's is followed by its Moment, naming the same innovator.
        var completions = w.AllViewerEvents.Where(e => e.Kind == ViewEventKind.ProjectCompleted).ToList();
        var moments = w.AllViewerEvents.Where(e => e.Kind == ViewEventKind.Moment).ToList();
        Assert.Equal(completions.Count, moments.Count);
        for (int i = 0; i < moments.Count; i++)
        {
            Assert.Equal(completions[i].Project, moments[i].Project);
            Assert.Equal(completions[i].Figure, moments[i].Figure);
        }
    }

    [Fact]
    public void Moments_and_visible_changes_cover_the_E0_to_E2_research_catalog()
    {
        ResearchCatalog catalog = TestPack.Catalog;
        foreach (ProjectDef project in catalog.Projects)
            if (project.Numeric && project.Era <= 2)
            {
                Assert.False(string.IsNullOrWhiteSpace(project.Moment), $"{project.Id} has no Moment text");
                Assert.False(string.IsNullOrWhiteSpace(project.Flavor), $"{project.Id} has no Moment Flavor text");
            }

        WatchedMatch w = ViewKit.Watched.Value;
        List<ViewEvent> completions = w.AllViewerEvents.Where(e => e.Kind == ViewEventKind.ProjectCompleted).ToList();
        for (int completedIndex = 0; completedIndex < completions.Count; completedIndex++)
        {
            ViewEvent completed = completions[completedIndex];
            long nextCompletion = completedIndex + 1 < completions.Count ? completions[completedIndex + 1].Sequence : long.MaxValue;
            ProjectDef project = catalog.Projects[completed.Project];
            int[] expectedEffects = project.Effects
                .Select((effect, index) => (effect, index))
                .Where(pair => pair.effect.Visual != VisibleChange.None)
                .Select(pair => pair.index)
                .ToArray();
            ViewEvent[] changes = w.AllViewerEvents
                .Where(e => e.Sequence > completed.Sequence && e.Sequence < nextCompletion &&
                    e.Kind == ViewEventKind.VisibleChange && e.Project == completed.Project && e.Tick == completed.Tick)
                .ToArray();
            Assert.Equal(expectedEffects, changes.Select(e => e.B).ToArray());
            int firstVisual = expectedEffects.Length == 0 ? (int)VisibleChange.None : (int)project.Effects[expectedEffects[0]].Visual;
            Assert.Equal(firstVisual, completed.A);
            Assert.Contains(w.AllViewerEvents, e => e.Sequence > completed.Sequence && e.Sequence < nextCompletion &&
                e.Kind == ViewEventKind.Moment && e.Project == completed.Project && e.Tick == completed.Tick);
        }
    }

    [Fact]
    public void Every_effect_kind_has_an_explicit_visible_change_classification()
    {
        foreach (EffectKind kind in Enum.GetValues<EffectKind>())
        {
            VisibleChange expected = kind switch
            {
                EffectKind.UnlockAction => VisibleChange.NewAction,
                EffectKind.Method => VisibleChange.ToolOrMethod,
                EffectKind.Good => VisibleChange.Good,
                EffectKind.Build or EffectKind.Wonder => VisibleChange.Structure,
                EffectKind.Role => VisibleChange.Role,
                EffectKind.Tier => VisibleChange.Tier,
                EffectKind.Claim or EffectKind.Reach or EffectKind.Reveal or EffectKind.Show or EffectKind.Highlight
                    or EffectKind.Presence or EffectKind.Lever or EffectKind.Admin or EffectKind.Mandate => VisibleChange.Overlay,
                EffectKind.Sanitation => VisibleChange.Behavior,
                EffectKind.EraAdvance => VisibleChange.EraCelebration,
                EffectKind.Gate or EffectKind.Risk or EffectKind.Need or EffectKind.Legacy or EffectKind.Prose => VisibleChange.None,
                EffectKind.Mod or EffectKind.Cap or EffectKind.Hazard or EffectKind.Cohesion or EffectKind.Adopt
                    or EffectKind.Unlock or EffectKind.Rule or EffectKind.Planner or EffectKind.Diplo or EffectKind.Relations
                    or EffectKind.Combat or EffectKind.Defense or EffectKind.KnowHow or EffectKind.Event => VisibleChange.Behavior,
                _ => throw new Xunit.Sdk.XunitException($"{kind} has no visible-change audit mapping"),
            };
            Assert.Equal(expected, new EffectDef { Kind = kind }.Visual);
        }
    }

    [Fact]
    public void Text_keys_are_unique_and_every_kind_value_is_stable()
    {
        var keys = Enum.GetValues<ChronicleKey>().Where(k => k != ChronicleKey.None).Select(ViewText.Key).ToList();
        Assert.All(keys, k => Assert.False(string.IsNullOrEmpty(k)));
        Assert.Equal(keys.Count, keys.Distinct().Count());
        // Numbering is part of the contract (append only).
        Assert.Equal(22, (int)ViewEventKind.ProjectCompleted);
        Assert.Equal(46, (int)ViewEventKind.RaidResolved);
        Assert.Equal(9, (int)ViewEventKind.PartyIncoming);
    }
}
