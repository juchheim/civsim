using System;
using System.Collections.Generic;
using System.Text;
using CivSim.Game.Ui;
using CivSim.Sim.Research;
using CivSim.Sim.View;
using Godot;
using ResearchTheme = CivSim.Sim.Research.Theme;

namespace CivSim.Game;

/// <summary>Binding the research web to the frame: pooled cards, the Map view, the inspector and every assignment action.</summary>
public partial class Game
{
    // ---- refresh -----------------------------------------------------------------------------

    private void RefreshResearchWeb(MatchFrame frame)
    {
        if (!_researchWebVisible || _catalog is null) return;
        int signature = WebSignature(frame);
        if (signature == _webSignature) return;
        _webSignature = signature;
        RefreshResearchWebNow();
    }

    private int WebSignature(MatchFrame frame)
    {
        var hash = new HashCode();
        hash.Add(_selectedResearchProject);
        hash.Add(_webTarget);
        hash.Add(_webView);
        hash.Add(_webThemeFilter);
        hash.Add(_replaceRowOpen);
        hash.Add(_showAllAlmostRows);
        hash.Add(_webSearch?.Text);
        hash.Add(frame.FocusProjectCount);
        for (int p = 0; p < frame.FocusProjectCount; p++) hash.Add(frame.FocusProjectState[p]);
        for (int s = 0; s < 3; s++)
        {
            hash.Add(EffectiveSlotProject(s));
            hash.Add(frame.FocusSlotPermille[s] / 10);
        }
        hash.Add(frame.FocusQueueCount);
        for (int q = 0; q < frame.FocusQueueCount; q++) hash.Add(frame.FocusQueue[q]);
        hash.Add(frame.FocusCouncil);
        hash.Add(frame.FocusEra);
        hash.Add(_session?.Paused ?? false);
        return hash.ToHashCode();
    }

    private void RefreshResearchWebNow()
    {
        if (!_researchWebVisible || _current is null || _catalog is null) return;
        MatchFrame frame = _current;
        UpdateAgePath(frame);
        UpdateStrip(frame);
        UpdateQueueChip(frame);
        _updatingFocusBar = true;
        _webAutoFill?.SetPressedNoSignal(frame.FocusCouncil);
        if (_webAutoFill is not null) _webAutoFill.Disabled = frame.FocusCiv != frame.ViewerCiv || (_session?.IsReplay ?? false);
        _updatingFocusBar = false;
        _navCards.Clear();
        if (_webView == ViewChoose) RefreshChooseView(frame);
        else RefreshMapView(frame);
        UpdateInspector(frame);
    }

    // ---- helpers -----------------------------------------------------------------------------

    private UiProjectCard NewProjectCard(ProjectCardMode mode)
    {
        var card = new UiProjectCard(mode);
        if (mode == ProjectCardMode.Card) card.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        else if (mode == ProjectCardMode.Row) card.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        card.Pressed += () =>
        {
            bool link = card.GetParent() is not null && IsInspectorLink(card);
            if (link) _mapCenterNext = true;
            SelectResearchProject(card.Project, _webView == ViewChoose && link);
        };
        card.GuiInput += input =>
        {
            if (input is InputEventMouseButton { DoubleClick: true, ButtonIndex: MouseButton.Left } && card.Project >= 0)
            {
                SelectResearchProject(card.Project);
                StartSelectedProject();
            }
        };
        return card;
    }

    private bool IsInspectorLink(UiProjectCard card)
        => _needsPool.Contains(card) || _needsAnyPool.Contains(card) || _leadsPool.Contains(card);

    private static UiProjectCard GetPooled(List<UiProjectCard> pool, int index, Func<UiProjectCard> make, Control parent)
    {
        while (pool.Count <= index)
        {
            UiProjectCard card = make();
            pool.Add(card);
            parent.AddChild(card);
        }
        return pool[index];
    }

    private static void HideFrom(List<UiProjectCard> pool, int used)
    {
        for (int i = used; i < pool.Count; i++) if (pool[i].Visible) pool[i].Visible = false;
    }

    private static string StateWord(ProjectState state) => state switch
    {
        ProjectState.Available => "Ready",
        ProjectState.Slotted => "In progress",
        ProjectState.Glimpsed => "Almost ready",
        ProjectState.Completed => "Known",
        ProjectState.Dormant => "Blocked",
        _ => "",
    };

    private double InsightPerYear()
        => _current is { FocusCiv: >= 0 } f && f.FocusCiv < f.CivCount ? f.CivInsightPerYear[f.FocusCiv] / 4294967296.0 : 0;

    /// <summary>Seconds to finish a project if it ran in this slot at today's speed. Negative when unknown.</summary>
    private double EstimateSeconds(ProjectDef def, int slot)
    {
        double insight = InsightPerYear();
        if (insight <= 0.01 || _session is null) return -1;
        if (slot < 0) slot = 0;
        int weight = UiText.SlotWeight[slot];
        int sum = weight;
        for (int s = 0; s < 3; s++) if (s != slot && EffectiveSlotProject(s) >= 0) sum += UiText.SlotWeight[s];
        double share = weight / (double)sum;
        double speed = _session.Paused ? 1.0 : Math.Max(0.25, _session.Speed);
        return def.Cost.ToDoubleForDisplay() / (insight * share) * 60.0 / speed;
    }

    private string EstimateText(ProjectDef def)
    {
        double seconds = EstimateSeconds(def, Math.Max(0, ResolveTargetSlot()));
        return seconds < 0 ? "" : "~" + UiText.Minutes(seconds);
    }

    private void MissingPrerequisite(ProjectDef def, out int missing, out bool hidden)
    {
        missing = -1;
        hidden = false;
        foreach (int r in def.Requires)
        {
            ProjectState s = StateOf(r);
            if (s == ProjectState.Hidden && !IsRevealed(r)) { hidden = true; return; }
            if (s is not (ProjectState.Completed or ProjectState.Dormant)) { missing = r; return; }
        }
        if (def.RequiresAny.Length == 0) return;
        int firstVisible = -1;
        foreach (int r in def.RequiresAny)
        {
            ProjectState s = StateOf(r);
            if (s is ProjectState.Completed or ProjectState.Dormant) return;
            if (IsKnownToPlayer(r) && firstVisible < 0) firstVisible = r;
        }
        if (firstVisible >= 0) missing = firstVisible;
        else hidden = true;
    }

    private string AlmostSub(ProjectDef def)
    {
        MissingPrerequisite(def, out int missing, out bool hidden);
        if (missing >= 0) return $"after {_catalog!.Projects[missing].Name}";
        if (hidden) return "after a project you haven't found yet";
        foreach (Gate gate in def.Gates)
            if (gate.Kind != GateKind.Seen) return UiText.GateText(_catalog!, gate).ToLowerInvariant();
        return "waiting on a condition";
    }

    private ProjectLook BuildLook(ProjectDef def, bool full)
    {
        var frame = _current!;
        ProjectState state = StateOf(def.Index);
        if (IsRevealed(def.Index)) state = ProjectState.Glimpsed;
        int slot = SlotOfProject(def.Index);
        var look = new ProjectLook
        {
            State = state == ProjectState.Available && slot >= 0 ? ProjectState.Slotted : state,
            SlotRank = slot,
            QueueIndex = QueueIndexOf(def.Index),
            Progress = slot >= 0 && _pendingProject[slot] == -2 ? frame.FocusSlotPermille[slot] / 1000f : 0,
            Selected = def.Index == _selectedResearchProject,
            RequiredForNextAge = (uint)def.Index < (uint)_agePathRequired.Length && _agePathRequired[def.Index],
            OptionalNextAgeRoute = (uint)def.Index < (uint)_agePathOption.Length && _agePathOption[def.Index],
            TurnsAge = def.IsKeystone && def.BreadthEra == frame.FocusEra,
            OnBreadthPath = (uint)def.Index < (uint)_breadthPath.Length && _breadthPath[def.Index],
            Line2 = "",
            Line3 = "",
            Sub = "",
        };
        if (look.State == ProjectState.Slotted && slot >= 0 && _pendingProject[slot] != -2) look.Progress = 0;
        if (full)
        {
            look.Line2 = UiText.FirstChangeLine(def);
            string est = state is ProjectState.Available ? EstimateText(def) : "";
            look.Line3 = est.Length > 0 ? $"{est} · {UiText.TypeNames[(int)def.Type]}" : UiText.TypeNames[(int)def.Type];
        }
        return look;
    }

    /// <summary>Marks the current age's turning point and unfinished projects that help reach it.</summary>
    private void UpdateAgePath(MatchFrame frame)
    {
        if (_catalog is null) return;
        _agePath = new bool[_catalog.Count];
        _agePathRequired = new bool[_catalog.Count];
        _agePathOption = new bool[_catalog.Count];
        _breadthPath = new bool[_catalog.Count];
        int civ = frame.FocusCiv;
        int era = frame.FocusEra;
        if ((uint)civ >= (uint)frame.CivCount || era < 0) return;

        ProjectDef? turningPoint = null;
        foreach (ProjectDef def in _catalog.Projects)
        {
            if (def.IsKeystone && def.Numeric && def.BreadthEra == era)
            {
                turningPoint = def;
                break;
            }
        }
        if (turningPoint is null) return;

        // Keep the destination marked even after its prerequisites are complete.
        _agePath[turningPoint.Index] = true;
        var visiting = new[] { new bool[_catalog.Count], new bool[_catalog.Count] };
        var processed = new[] { new bool[_catalog.Count], new bool[_catalog.Count] };
        MarkAgePathPrerequisites(turningPoint, false, visiting, processed);

        // Any unfinished regular project in this age contributes to the keystone's breadth
        // requirement, even while some named prerequisites remain. Keep the age-path sorting
        // behavior focused on breadth choices only after the named chain is complete.
        bool namedPrerequisitesRemain = false;
        for (int p = 0; p < _agePath.Length; p++)
            if (p != turningPoint.Index && _agePath[p])
            {
                namedPrerequisitesRemain = true;
                break;
            }
        if (frame.CivBreadthDone[civ] >= turningPoint.BreadthMin) return;

        foreach (ProjectDef def in _catalog.Projects)
        {
            if (!def.Numeric || def.IsKeystone || def.Era != era) continue;
            if (StateOf(def.Index) is ProjectState.Completed or ProjectState.Dormant) continue;
            _breadthPath[def.Index] = true;
            if (!namedPrerequisitesRemain) _agePath[def.Index] = true;
        }
    }

    private void MarkAgePathProject(int project, bool optional, bool[][] visiting, bool[][] processed)
    {
        if (_catalog is null || (uint)project >= (uint)_catalog.Count) return;
        ProjectState state = StateOf(project);
        if (state is ProjectState.Completed or ProjectState.Dormant) return;
        _agePath[project] = true;
        if (optional)
        {
            if (!_agePathRequired[project]) _agePathOption[project] = true;
        }
        else
        {
            _agePathRequired[project] = true;
            _agePathOption[project] = false;
        }
        int route = optional ? 1 : 0;
        if (processed[route][project] || visiting[route][project]) return;
        visiting[route][project] = true;

        ProjectDef def = _catalog.Projects[project];
        MarkAgePathPrerequisites(def, optional, visiting, processed);
        visiting[route][project] = false;
        processed[route][project] = true;
    }

    private void MarkAgePathPrerequisites(ProjectDef def, bool optional, bool[][] visiting, bool[][] processed)
    {
        foreach (int required in def.Requires)
            if (StateOf(required) is not (ProjectState.Completed or ProjectState.Dormant))
                MarkAgePathProject(required, optional, visiting, processed);

        if (def.RequiresAny.Length > 0)
        {
            bool anyDone = false;
            foreach (int alternative in def.RequiresAny)
                if (StateOf(alternative) is ProjectState.Completed or ProjectState.Dormant)
                {
                    anyDone = true;
                    break;
                }
            if (!anyDone)
                foreach (int alternative in def.RequiresAny)
                    MarkAgePathProject(alternative, true, visiting, processed);
        }
    }

    // ---- Choose view -------------------------------------------------------------------------

    private static readonly List<int>[] ReadyScratch = { new(), new(), new() };
    private static readonly List<int> KeystoneScratch = new(), AlmostScratch = new(), SearchScratch = new();

    private void RefreshChooseView(MatchFrame frame)
    {
        string query = _webSearch?.Text.Trim() ?? "";
        bool searching = query.Length > 0;
        foreach (List<int> list in ReadyScratch) list.Clear();
        KeystoneScratch.Clear();
        AlmostScratch.Clear();
        SearchScratch.Clear();

        int hiddenCount = 0;
        for (int p = 0; p < Math.Min(frame.FocusProjectCount, _catalog!.Count); p++)
        {
            ProjectDef def = _catalog.Projects[p];
            if (!def.Numeric) continue;
            ProjectState state = StateOf(p);
            if (state == ProjectState.Hidden)
            {
                if (IsRevealed(p)) continue;
                if (!def.IsKeystone && def.Era <= frame.FocusEra) hiddenCount++;
                continue;
            }
            if (_webThemeFilter >= 0 && (int)def.Theme != _webThemeFilter) continue;
            if (searching)
            {
                if (ResearchMatches(def, query)) SearchScratch.Add(p);
                continue;
            }
            switch (state)
            {
                case ProjectState.Available:
                case ProjectState.Slotted:
                    if (def.IsKeystone) KeystoneScratch.Add(p);
                    else ReadyScratch[(int)def.Theme].Add(p);
                    break;
                case ProjectState.Glimpsed:
                    AlmostScratch.Add(p);
                    break;
            }
        }

        bool anySlotted = false;
        foreach (List<int> list in ReadyScratch) list.Sort(CompareReady);
        KeystoneScratch.Sort(CompareReady);
        AlmostScratch.Sort(CompareReady);
        SearchScratch.Sort(CompareSearch);
        _ = anySlotted;

        UiKit.SetVisible(_readyBlock!, !searching);
        UiKit.SetVisible(_almostHeader!.GetParent<Control>(), !searching);
        UiKit.SetVisible(_almostList!, !searching);
        UiKit.SetVisible(_moreLabel!, !searching);
        UiKit.SetVisible(_searchList!, searching);

        if (searching)
        {
            for (int i = 0; i < SearchScratch.Count; i++)
            {
                ProjectDef def = _catalog.Projects[SearchScratch[i]];
                UiProjectCard row = GetPooled(_rowPool, i, () => NewProjectCard(ProjectCardMode.Row), _searchList!);
                ProjectLook look = BuildLook(def, false);
                look.Sub = StateWord(StateOf(def.Index)) + " · " + UiText.FieldName(_catalog, def.Lineage, true);
                row.Bind(def, look);
                row.Visible = true;
                _navCards.Add(row);
            }
            HideFrom(_rowPool, SearchScratch.Count);
            UiKit.SetVisible(_noResultsLabel!, SearchScratch.Count == 0);
            if (SearchScratch.Count == 0) UiKit.SetText(_noResultsLabel!, $"No known project matches “{query}”.");
            return;
        }
        UiKit.SetVisible(_noResultsLabel!, false);

        int readyTotal = KeystoneScratch.Count;
        for (int theme = 0; theme < 3; theme++)
        {
            List<int> ids = ReadyScratch[theme];
            readyTotal += ids.Count;
            for (int i = 0; i < ids.Count; i++)
            {
                ProjectDef def = _catalog.Projects[ids[i]];
                UiProjectCard card = GetPooled(_cardPools[theme], i, () => NewProjectCard(ProjectCardMode.Card), _readyColumns[theme]!);
                card.Bind(def, BuildLook(def, true));
                card.Visible = true;
                _navCards.Add(card);
            }
            HideFrom(_cardPools[theme], ids.Count);
            UiKit.SetText(_columnCounts[theme]!, ids.Count > 0 ? ids.Count.ToString() : "");
            UiKit.SetVisible(_columnEmpty[theme]!, ids.Count == 0);
        }
        _keystoneRow!.Visible = KeystoneScratch.Count > 0;
        for (int i = 0; i < KeystoneScratch.Count; i++)
        {
            ProjectDef def = _catalog.Projects[KeystoneScratch[i]];
            UiProjectCard card = GetPooled(_cardPools[3], i, () => NewProjectCard(ProjectCardMode.Card), _keystoneRow);
            card.Bind(def, BuildLook(def, true));
            card.Visible = true;
            _navCards.Insert(i, card);
        }
        HideFrom(_cardPools[3], KeystoneScratch.Count);
        UiKit.SetText(_readyHeader!, $"READY NOW · {readyTotal}");

        int shown = _showAllAlmostRows ? AlmostScratch.Count : Math.Min(5, AlmostScratch.Count);
        for (int i = 0; i < shown; i++)
        {
            ProjectDef def = _catalog.Projects[AlmostScratch[i]];
            UiProjectCard row = GetPooled(_rowPool, i, () => NewProjectCard(ProjectCardMode.Row), _almostList!);
            ProjectLook look = BuildLook(def, false);
            MissingPrerequisite(def, out _, out bool hidden);
            look.HiddenPrerequisite = hidden;
            look.Sub = AlmostSub(def);
            row.Bind(def, look);
            row.Visible = true;
            _navCards.Add(row);
        }
        HideFrom(_rowPool, shown);
        UiKit.SetText(_almostHeader, $"ALMOST READY · {AlmostScratch.Count}");
        UiKit.SetVisible(_showAllAlmost!, AlmostScratch.Count > 5);
        UiKit.SetText(_showAllAlmost!, _showAllAlmostRows ? "Show fewer" : $"Show all ({AlmostScratch.Count})");
        UiKit.SetText(_moreLabel!, hiddenCount > 0 && _webThemeFilter < 0 ? $"+ {hiddenCount} more to discover in the {UiText.AgeName(frame.FocusEra)} age" : "");
    }

    private int CompareReady(int a, int b)
    {
        ProjectDef da = _catalog!.Projects[a], db = _catalog.Projects[b];
        bool aOnAgePath = (uint)a < (uint)_agePath.Length && _agePath[a];
        bool bOnAgePath = (uint)b < (uint)_agePath.Length && _agePath[b];
        if (aOnAgePath != bOnAgePath) return aOnAgePath ? -1 : 1;
        int c = da.Cost.ToDoubleForDisplay().CompareTo(db.Cost.ToDoubleForDisplay());
        return c != 0 ? c : string.CompareOrdinal(da.Name, db.Name);
    }

    private int CompareSearch(int a, int b)
    {
        int Group(int p) => StateOf(p) switch
        {
            ProjectState.Available or ProjectState.Slotted => 0, ProjectState.Glimpsed => 1, ProjectState.Completed => 2, _ => 3,
        };
        int c = Group(a).CompareTo(Group(b));
        return c != 0 ? c : string.CompareOrdinal(_catalog!.Projects[a].Name, _catalog.Projects[b].Name);
    }

    private bool ResearchMatches(ProjectDef def, string query)
    {
        string needle = query.ToLowerInvariant();
        bool Contains(string? text) => text is not null && text.Contains(needle, StringComparison.OrdinalIgnoreCase);
        if (Contains(def.Name) || Contains(def.Id) || Contains(def.Flavor) || Contains(def.Moment) || Contains(def.Type.ToString()) ||
            Contains(UiText.TypeNames[(int)def.Type]) || Contains(UiText.ThemeNames[(int)def.Theme])) return true;
        if (def.Lineage >= 0 && (Contains(UiText.FieldName(_catalog!, def.Lineage, true)) || Contains(UiText.FieldName(_catalog!, def.Lineage, false)))) return true;
        for (int i = 0; i < def.Effects.Length; i++)
        {
            EffectDef effect = def.Effects[i];
            if (Contains(effect.Text) || Contains(effect.Note) || Contains(effect.Action) || Contains(UiText.DescribeEffect(def, i))) return true;
        }
        return false;
    }

    // ---- Map view ----------------------------------------------------------------------------

    private ResearchRadialMap BuildMapView()
    {
        var map = new ResearchRadialMap
        {
            Visible = false,
            ProjectClicked = project => SelectResearchProject(project),
            ProjectActivated = project =>
            {
                SelectResearchProject(project);
                StartSelectedProject();
            },
            TooltipFor = MapTooltip,
            FirstMissing = project =>
            {
                if (_catalog is null || (uint)project >= (uint)_catalog.Count) return -1;
                MissingPrerequisite(_catalog.Projects[project], out int missing, out _);
                return missing;
            },
        };
        map.SetCatalog(_catalog!, lineage => UiText.FieldName(_catalog!, lineage, true));
        _mapLooks = new RadialNodeLook[_catalog!.Count];
        return map;
    }

    private void RefreshMapView(MatchFrame frame)
    {
        if (_mapView?.Layout is not ResearchRadialLayout layout || _catalog is null) return;
        string query = _webSearch?.Text.Trim() ?? "";
        bool searching = query.Length > 0;
        int era = frame.FocusEra;
        int civ = Math.Max(0, frame.FocusCiv);
        int keystone = _catalog.KeystoneLeaving(Math.Max(0, era));
        for (int p = 0; p < _mapLooks.Length; p++)
        {
            ProjectDef def = _catalog.Projects[p];
            ProjectState state = StateOf(p);
            ref RadialNodeLook node = ref _mapLooks[p];
            node = default;
            node.SlotRank = -1;
            node.QueueIndex = -1;
            if (!layout.Placed[p]) continue;
            bool revealed = IsRevealed(p);
            if (state == ProjectState.Hidden && !revealed)
            {
                node.Undiscovered = !def.IsKeystone && def.Era <= era;
                continue;
            }
            ProjectLook look = BuildLook(def, false);
            node.Visible = true;
            node.State = look.State;
            node.SlotRank = look.SlotRank;
            node.QueueIndex = look.QueueIndex;
            node.Progress = look.Progress;
            node.RequiredForNextAge = look.RequiredForNextAge;
            node.OptionalNextAgeRoute = look.OptionalNextAgeRoute;
            node.OnBreadthPath = look.OnBreadthPath;
            node.TurnsAge = look.TurnsAge;
            if (revealed) node.HiddenPrerequisite = true;
            else if (state == ProjectState.Glimpsed)
            {
                MissingPrerequisite(def, out _, out bool hidden);
                node.HiddenPrerequisite = hidden;
            }
            node.Filtered = _webThemeFilter >= 0 && (def.IsKeystone || (int)def.Theme != _webThemeFilter);
            node.Match = searching && ResearchMatches(def, query);
            if (p == keystone && (uint)civ < (uint)frame.CivCount)
            {
                node.Progress = Math.Min(1f, frame.CivBreadthDone[civ] / (float)Math.Max(1, def.BreadthMin));
                node.BreadthDone = frame.CivBreadthDone[civ];
                if (RevealsPath)
                    for (int r = 0; r < _agePathRequired.Length; r++)
                        if (_agePathRequired[r] && r != p && _catalog.Projects[r].Numeric) node.PathLeft++;
            }
        }

        string title = era >= 0 ? $"{UiText.AgeName(era).ToUpperInvariant()} AGE" : "";
        string sub = "";
        if (keystone >= 0 && (uint)civ < (uint)frame.CivCount && StateOf(keystone) is not (ProjectState.Completed or ProjectState.Dormant))
        {
            ProjectDef gate = _catalog.Projects[keystone];
            int done = Math.Min((int)frame.CivBreadthDone[civ], gate.BreadthMin);
            sub = $"{done} of {gate.BreadthMin} {UiText.AgeName(era)} projects known toward {gate.Name}";
        }
        _mapView.SetLooks(_mapLooks, _selectedResearchProject, era, searching, _webThemeFilter, title, sub);
    }

    private string MapTooltip(int project)
    {
        if (_catalog is null || (uint)project >= (uint)_catalog.Count) return "";
        ProjectDef def = _catalog.Projects[project];
        ProjectState state = StateOf(project);
        ProjectLook look = BuildLook(def, false);
        var tip = new StringBuilder();
        bool revealed = IsRevealed(project);
        tip.Append(def.Name).Append(" · ").Append(revealed ? $"On the path · needs {WhatItNeeds(def)}" : StateWord(look.State));
        if (state == ProjectState.Glimpsed) tip.Append(" — ").Append(AlmostSub(def));
        tip.Append('\n').Append(def.IsKeystone ? "Turning point" : UiText.FieldName(_catalog, def.Lineage, false))
            .Append(" · ").Append(UiText.AgeName(def.Era)).Append(" age");
        string role = AgeRoleText(_current!, look);
        if (role.Length > 0) tip.Append('\n').Append(role);
        if (state is ProjectState.Available && SlotOfProject(project) < 0) tip.Append("\nDouble-click or Enter to start");
        else if (state == ProjectState.Glimpsed && QueueIndexOf(project) < 0) tip.Append("\nDouble-click to queue it");
        if (_devVisible) tip.Append('\n').Append(def.Id);
        return tip.ToString();
    }

    /// <summary>Enter in the search box on the Map: select the best match and centre it.</summary>
    private void JumpToFirstMapMatch()
    {
        if (_webView != ViewMap || _catalog is null || _current is null || _mapView?.Layout is not ResearchRadialLayout layout) return;
        string query = _webSearch?.Text.Trim() ?? "";
        if (query.Length == 0) return;
        var matches = new List<int>();
        for (int p = 0; p < Math.Min(_current.FocusProjectCount, _catalog.Count); p++)
            if (layout.Placed[p] && IsKnownToPlayer(p) && ResearchMatches(_catalog.Projects[p], query)) matches.Add(p);
        if (matches.Count == 0) return;
        matches.Sort(CompareSearch);
        _mapCenterNext = true;
        _webSearch?.ReleaseFocus();
        SelectResearchProject(matches[0]);
        _mapView.GrabFocus();
    }

    // ---- slot strip, queue ---------------------------------------------------------------------

    private void UpdateStrip(MatchFrame frame)
    {
        int target = ResolveTargetSlot();
        for (int slot = 0; slot < 3; slot++)
        {
            StripChip chip = _strip[slot];
            int project = EffectiveSlotProject(slot);
            bool pending = _pendingProject[slot] != -2;
            if (project >= 0 && project < _catalog!.Count)
            {
                ProjectDef def = _catalog.Projects[project];
                UiKit.SetText(chip.Name, def.Name);
                chip.Bar.Fill = UiTokens.ThemeColor(def.Theme);
                chip.Bar.SetValue(pending ? 0 : frame.FocusSlotPermille[slot] / 1000f);
                UiKit.SetText(chip.Eta, pending ? "Pending" : FormatEta(slot, frame.FocusSlotPermille[slot]));
                chip.Badge.SetFill(new Color("#E2D3A6"));
            }
            else
            {
                UiKit.SetText(chip.Name, "+ Open");
                chip.Bar.SetValue(0, false);
                UiKit.SetText(chip.Eta, "");
                chip.Badge.SetFill(UiTokens.TextTertiary);
            }
            chip.Name.ThemeTypeVariation = project >= 0 ? "BodyStrong" : "BodyStrongAccent";
            chip.Root.TooltipText = project >= 0
                ? $"{UiText.SlotNames[slot]} · Drag {chip.Name.Text} to another slot to change its priority. Click to choose this slot."
                : $"{UiText.SlotNames[slot]} is open · Drop a project here to start it at this priority. Click to choose this slot.";
            UiKit.SetVisible(chip.Target, slot == target && chip.Root.Size.X > 260);
            UiKit.SetVisible(chip.Frame, slot == target);
        }
    }

    private void UpdateQueueChip(MatchFrame frame)
    {
        if (_queueChip is null) return;
        UiKit.SetText(_queueChip, frame.FocusQueueCount > 0 ? $"Queue {frame.FocusQueueCount}" : "Queue");
    }

    private void ToggleQueuePopover()
    {
        if (_queuePopover is null || _current is null || _catalog is null || _queueChip is null) return;
        if (_queuePopover.Visible) { CloseQueuePopover(); return; }
        CloseLeaningPopover();
        VBoxContainer body = UiKit.Column(UiTokens.Sp2);
        body.CustomMinimumSize = new Vector2(377, 0);
        body.AddChild(UiKit.Text("Waiting in line", "Heading"));
        if (_current.FocusQueueCount == 0)
            body.AddChild(UiKit.Text("Nothing queued. Queue a project and it starts by itself when a slot opens.", "BodySecondary", wrap: true));
        for (int i = 0; i < _current.FocusQueueCount; i++)
        {
            int queueIndex = i;
            int project = _current.FocusQueue[i];
            if ((uint)project >= (uint)_catalog.Count) continue;
            ProjectDef def = _catalog.Projects[project];
            var row = UiKit.Row(UiTokens.Sp2);
            row.AddChild(new UiIcon(UiDraw.ThemeGlyph(def.Theme), 16, UiTokens.ThemeColor(def.Theme)));
            row.AddChild(UiKit.Text($"{i + 1}. {def.Name}", "BodyStrong", ellipsis: true));
            row.AddChild(UiKit.Ghost("Remove", () => { DequeueAt(queueIndex); CloseQueuePopover(); }, null, UiGlyph.None, 30));
            body.AddChild(row);
        }
        foreach (Node child in _queuePopover.Body.GetChildren()) { _queuePopover.Body.RemoveChild(child); child.QueueFree(); }
        _queuePopover.Body.AddChild(body);
        Vector2 pos = _queueChip.GlobalPosition;
        _queuePopover.OpenRightAligned(pos.X + _queueChip.Size.X, pos.Y + _queueChip.Size.Y + 6, 377);
        _queueLayer = RegisterLayer("queue", false, () => { CloseQueuePopover(); return true; });
        _ui.Push(_queueLayer);
    }

    private void CloseQueuePopover()
    {
        _queuePopover?.Close();
        if (_queueLayer is not null) _ui.Remove(_queueLayer);
        _queueLayer = null;
    }

    private void DequeueAt(int index)
    {
        if (_session is null || _current is null) return;
        _session.Queue(PlayerCommands.Dequeue(_session.Tick, _current.FocusCiv, index));
    }

    private void ToggleLeaningPopover()
    {
        if (_leaningPopover is null || _current is null) return;
        if (_leaningPopover.Visible) { CloseLeaningPopover(); return; }
        CloseQueuePopover();
        for (int i = 0; i < 3; i++)
        {
            if (_leaningSliders[i] is HSlider slider) slider.Value = _current.FocusLeaning[i];
            if (_leaningValues[i] is Label label) label.Text = $"{_current.FocusLeaning[i]}%";
        }
        _leaningPopover.Open(new Vector2(UiTokens.Sp4 + 8, _logicalSize.Y - 330), 377);
        _leaningLayer = RegisterLayer("leaning", false, () => { CloseLeaningPopover(); return true; });
        _ui.Push(_leaningLayer);
    }

    private void CloseLeaningPopover()
    {
        _leaningPopover?.Close();
        if (_leaningLayer is not null) _ui.Remove(_leaningLayer);
        _leaningLayer = null;
    }

    private void ApplyLeanings()
    {
        if (_session is null || _current is null) return;
        var values = new int[3];
        int total = 0;
        for (int i = 0; i < 3; i++) { values[i] = (int)Math.Round(_leaningSliders[i]?.Value ?? 0); total += values[i]; }
        if (total == 0) { values[0] = 34; values[1] = 33; values[2] = 33; total = 100; }
        int assigned = 0;
        for (int i = 0; i < 3; i++) { values[i] = values[i] * 100 / total; assigned += values[i]; }
        for (int i = 0; assigned < 100; i = (i + 1) % 3, assigned++) values[i]++;
        for (int i = 0; i < 3; i++) _session.Queue(PlayerCommands.SetLeaning(_session.Tick, _current.FocusCiv, i, values[i]));
        ShowFooterNote("The elders' leaning is set.");
    }

    // ---- inspector ---------------------------------------------------------------------------

    private enum CtaKind { None, Start, Replace, ChooseSlot, Move, Queue, Disabled }

    private CtaKind _cta = CtaKind.None;
    private int _ctaSlot = -1;

    private int InspectorSignature(MatchFrame frame, ProjectDef? def)
    {
        var hash = new HashCode();
        hash.Add(_selectedResearchProject);
        hash.Add(_webTarget);
        hash.Add(_replaceRowOpen);
        hash.Add(_backProject);
        hash.Add(frame.FocusEra);
        hash.Add(_session?.Paused ?? false);
        for (int s = 0; s < 3; s++) { hash.Add(EffectiveSlotProject(s)); hash.Add(frame.FocusSlotPermille[s] / 20); }
        hash.Add(frame.FocusQueueCount);
        for (int q = 0; q < frame.FocusQueueCount; q++) hash.Add(frame.FocusQueue[q]);
        if (def is not null)
        {
            hash.Add(StateOf(def.Index));
            ProjectLook look = BuildLook(def, false);
            hash.Add(look.OnBreadthPath);
            hash.Add(look.RequiredForNextAge);
            hash.Add(look.OptionalNextAgeRoute);
            hash.Add(look.TurnsAge);
            hash.Add(IsRevealed(def.Index));
            foreach (int r in def.Requires) { hash.Add(StateOf(r)); hash.Add(IsRevealed(r)); }
            foreach (int r in def.RequiresAny) hash.Add(StateOf(r));
            if (def.Index < _dependents.Length) foreach (int d in _dependents[def.Index]) hash.Add(StateOf(d));
            hash.Add(frame.CivBreadthDone[Math.Max(0, frame.FocusCiv)]);
            hash.Add(ProjectPeopleContextLine(frame, def));
        }
        return hash.ToHashCode();
    }

    private void UpdateInspector(MatchFrame frame)
    {
        if (_catalog is null || _inspector is null) return;
        int project = _selectedResearchProject;
        bool known = (uint)project < (uint)frame.FocusProjectCount && IsKnownToPlayer(project);
        ProjectDef? def = known ? _catalog.Projects[project] : null;
        if (_dependents.Length == 0) EnsureDependents();
        int signature = InspectorSignature(frame, def);
        if (signature == _inspSignature) return;
        _inspSignature = signature;

        UiKit.SetVisible(_inspEmpty!, !known);
        foreach (Control? c in new Control?[] { _inspectorChips, _inspName, _inspState, _ageRoleNote, _ctaButton, _inspWhy, _inspEstimate, _orRow, _changesBlock, _peopleProjectContext, _seeBlock, _needsBlock, _leadsBlock })
            if (c is not null) UiKit.SetVisible(c, known);
        if (!known || def is null)
        {
            UiKit.SetVisible(_backLink!, false);
            UiKit.SetVisible(_replaceRow!, false);
            return;
        }

        ProjectState state = StateOf(project);
        int slotOf = SlotOfProject(project);
        int queueIndex = QueueIndexOf(project);
        int target = ResolveTargetSlot();
        bool canCommand = _session?.CanIssueCommands ?? false;

        UiKit.SetVisible(_backLink!, _backProject >= 0 && _backProject != project);
        if (_backLink!.Visible) _backLink.Text = $"← Back to {_catalog.Projects[_backProject].Name}";
        UiKit.SetText(_inspName!, def.Name);
        SetChip(_chipTheme!, UiText.ThemeNames[(int)def.Theme].ToUpperInvariant(), UiDraw.ThemeGlyph(def.Theme), UiTokens.ThemeText(def.Theme));
        SetChip(_chipAge!, UiText.AgeName(def.Era).ToUpperInvariant() + " AGE", UiGlyph.None, UiTokens.TextSecondary);
        SetChip(_chipType!, UiText.TypeNames[(int)def.Type].ToUpperInvariant(), UiGlyph.None, UiTokens.TextSecondary);

        // state line
        string stateText;
        string stateVariation = "BodyStrong";
        switch (state)
        {
            case ProjectState.Available when slotOf < 0:
                stateText = queueIndex >= 0 ? $"Ready to start · queued #{queueIndex + 1}" : "Ready to start";
                break;
            case ProjectState.Available:
            case ProjectState.Slotted:
                {
                    int s = Math.Max(0, slotOf);
                    int permille = _pendingProject[s] != -2 ? 0 : frame.FocusSlotPermille[s];
                    stateText = _pendingProject[s] != -2
                        ? $"In {UiText.SlotNames[s]} · starts when you resume"
                        : $"In {UiText.SlotNames[s]} · {permille / 10}% · {EtaPhrase(s, (short)permille)}";
                    break;
                }
            case ProjectState.Hidden:
                stateText = $"Not in reach yet — needs {WhatItNeeds(def)}";
                break;
            case ProjectState.Glimpsed:
                stateText = queueIndex >= 0 ? $"Queued · #{queueIndex + 1} in line" : $"Almost ready — needs {WhatItNeeds(def)}";
                break;
            case ProjectState.Completed:
                stateText = "Known";
                stateVariation = "BodyStrongSecondary";
                break;
            default:
                stateText = BlockedByMetal(frame, def) is { } metalName
                    ? $"Blocked — no settlement holds {metalName}, so its methods fall back. Still counts toward your age."
                    : "Blocked — its methods fall back until you meet its needs";
                break;
        }
        UiKit.SetText(_inspState!, stateText);
        UiKit.SetVariation(_inspState!, stateVariation);

        ProjectLook ageLook = BuildLook(def, false);
        string ageRole = AgeRoleText(frame, ageLook);
        UiKit.SetText(_ageRoleNote!, ageRole);
        UiKit.SetVisible(_ageRoleNote!, ageRole.Length > 0);

        // call to action
        UpdateCta(frame, def, state, slotOf, queueIndex, target, canCommand);

        // estimate line
        double estimate = state == ProjectState.Available && slotOf < 0 ? EstimateSeconds(def, Math.Max(0, target)) : -1;
        UiKit.SetText(_inspEstimate!, estimate >= 0
            ? $"~{UiText.Minutes(estimate)} as {UiText.SlotNames[Math.Max(0, target)]}. An estimate at today's research speed; practice and contact with neighbours can make it faster."
            : "");
        UiKit.SetVisible(_inspEstimate!, estimate >= 0);

        // what changes
        List<string> changes = UiText.DescribeEffects(def, 4);
        var text = new StringBuilder();
        foreach (string line in changes) text.Append("• ").Append(line).Append('\n');
        string changesText = text.ToString().TrimEnd('\n');
        UiKit.SetVisible(_changesBlock!, changesText.Length > 0);
        UiKit.SetText(_inspChanges!, changesText);
        string peopleContext = ProjectPeopleContextLine(frame, def);
        UiKit.SetVisible(_peopleProjectContext!, peopleContext.Length > 0);
        UiKit.SetText(_peopleProjectContextText!, peopleContext);
        UiKit.SetVisible(_seeBlock!, def.Moment.Length > 0 || def.Flavor.Length > 0);
        UiKit.SetText(_inspSeeText!, def.Moment);
        UiKit.SetText(_inspFlavor!, def.Flavor.Length > 0 ? $"“{def.Flavor}”" : "");

        BuildNeeds(frame, def, state);
        BuildLeads(def);
        if (_inspectorScroll is not null && signature != _lastInspectorScrollSignature) { }
    }

    private static string AgeRoleText(MatchFrame frame, ProjectLook look)
    {
        var note = new StringBuilder();
        if (look.OnBreadthPath)
        {
            int needed = Math.Max(1, (int)frame.CivBreadthNeeded[Math.Max(0, frame.FocusCiv)]);
            note.Append($"Counts toward {UiText.AgeName(frame.FocusEra)}'s {needed}-project requirement.");
        }
        if (look.RequiredForNextAge)
        {
            if (note.Length > 0) note.Append(' ');
            note.Append("Required to reach the next age.");
        }
        else if (look.OptionalNextAgeRoute)
        {
            if (note.Length > 0) note.Append(' ');
            note.Append("One possible prerequisite route to the next age.");
        }
        if (look.TurnsAge)
        {
            if (note.Length > 0) note.Append(' ');
            note.Append("Completes the age transition.");
        }
        return note.ToString();
    }

    private int _lastInspectorScrollSignature;

    private string EtaPhrase(int slot, short permille)
    {
        if (_session?.Paused == true) return "paused";
        double rate = _focusRatePermillePerSecond[slot];
        if (rate <= 0.01) return "measuring speed";
        return $"about {UiText.Minutes((1000 - permille) / rate)} left";
    }

    private string WhatItNeeds(ProjectDef def)
    {
        MissingPrerequisite(def, out int missing, out bool hidden);
        if (missing >= 0) return _catalog!.Projects[missing].Name;
        if (hidden) return "a project you haven't found yet";
        foreach (Gate gate in def.Gates) if (gate.Kind != GateKind.Seen) return UiText.GateText(_catalog!, gate).Replace("Needs ", "").ToLowerInvariant();
        return "something more";
    }

    private static void SetChip(PanelContainer chip, string text, UiGlyph glyph, Color tint)
    {
        var row = chip.GetChild<HBoxContainer>(0);
        foreach (Node child in row.GetChildren())
        {
            if (child is UiIcon icon) { icon.Glyph = glyph; icon.Tint = tint; icon.Visible = glyph != UiGlyph.None; }
            else if (child is Label label) { label.Text = text; label.AddThemeColorOverride("font_color", tint); }
        }
    }

    private void UpdateCta(MatchFrame frame, ProjectDef def, ProjectState state, int slotOf, int queueIndex, int target, bool canCommand)
    {
        var cta = _ctaButton!;
        string text;
        string why = "";
        bool disabled = false;
        bool primary = true;
        _cta = CtaKind.None;
        _ctaSlot = -1;
        string Name(int slot) => EffectiveSlotProject(slot) >= 0 ? _catalog!.Projects[EffectiveSlotProject(slot)].Name : "";

        switch (state)
        {
            case ProjectState.Available when slotOf < 0:
                if (target >= 0)
                {
                    _ctaSlot = target;
                    bool open = EffectiveSlotProject(target) < 0;
                    _cta = open ? CtaKind.Start : CtaKind.Replace;
                    text = open ? $"Start in {UiText.SlotNames[target]}" : $"Put in {UiText.SlotNames[target]} — replaces {Name(target)}";
                }
                else
                {
                    _cta = CtaKind.ChooseSlot;
                    text = "Choose a slot to replace…";
                }
                break;
            case ProjectState.Available:
            case ProjectState.Slotted:
                if (_webTarget >= 0 && _webTarget != slotOf)
                {
                    _cta = CtaKind.Move;
                    _ctaSlot = _webTarget;
                    text = $"Move to {UiText.SlotNames[_webTarget]}";
                }
                else
                {
                    _cta = CtaKind.Disabled;
                    disabled = true;
                    text = $"Already in {UiText.SlotNames[Math.Max(0, slotOf)]}";
                }
                break;
            case ProjectState.Glimpsed:
                primary = false;
                _cta = queueIndex >= 0 ? CtaKind.Disabled : CtaKind.Queue;
                disabled = queueIndex >= 0 || frame.FocusQueueCount >= 8;
                text = queueIndex >= 0 ? $"Queued · #{queueIndex + 1} in line" : "Queue — starts when it's ready";
                MissingPrerequisite(def, out int missing, out bool hidden);
                why = missing >= 0 ? $"Why can't I start this? It needs {_catalog!.Projects[missing].Name} first."
                    : hidden ? "Why can't I start this? One step before it is still undiscovered."
                    : $"Why can't I start this? {AlmostSub(def)}.";
                break;
            case ProjectState.Hidden:
                _cta = CtaKind.Disabled;
                disabled = true;
                primary = false;
                text = "Not in reach yet";
                MissingPrerequisite(def, out int pathMissing, out _);
                why = pathMissing >= 0 ? $"Why can't I start this? It needs {_catalog!.Projects[pathMissing].Name} first." : $"Why can't I start this? {AlmostSub(def)}.";
                break;
            case ProjectState.Completed:
                _cta = CtaKind.Disabled;
                disabled = true;
                primary = false;
                text = "Known";
                break;
            default:
                _cta = CtaKind.Disabled;
                disabled = true;
                primary = false;
                text = "Blocked";
                break;
        }
        if (!canCommand) { disabled = true; why = _session?.IsReplay == true ? "Replays can't take orders." : why; }
        UiKit.SetText(cta, text);
        cta.Disabled = disabled;
        UiKit.SetVariation(cta, primary ? "Primary" : "Secondary");
        cta.CustomMinimumSize = new Vector2(0, primary ? UiTokens.ControlL : UiTokens.ControlM);
        UiKit.SetText(_inspWhy!, why);
        UiKit.SetVisible(_inspWhy!, why.Length > 0);

        // replace row
        bool showReplace = _replaceRowOpen && _cta == CtaKind.ChooseSlot;
        UiKit.SetVisible(_replaceRow!, showReplace);
        if (showReplace)
            for (int slot = 0; slot < 3; slot++)
            {
                int p = EffectiveSlotProject(slot);
                int permille = _pendingProject[slot] != -2 || p < 0 ? 0 : frame.FocusSlotPermille[slot] / 10;
                _replaceChips[slot].Text = p >= 0 ? $"{slot + 1} {UiText.SlotNames[slot]} · {Name(slot)}" : $"{slot + 1} {UiText.SlotNames[slot]} · open";
                _replaceChips[slot].TooltipText = p >= 0 ? $"Replace {Name(slot)} ({permille}% done). It keeps its progress." : "Use this open slot";
            }

        // "Or" row
        int shown = 0;
        void Or(string label, Action action, string? tip = null)
        {
            if (shown >= 3) return;
            UiButton button = _orButtons[shown++];
            button.Text = label;
            button.TooltipText = tip ?? label;
            button.Visible = true;
            _orActions[shown - 1] = action;
        }
        foreach (UiButton button in _orButtons) button.Visible = false;
        if (canCommand)
        {
            if (state == ProjectState.Available && slotOf < 0)
            {
                for (int slot = 0; slot < 3; slot++)
                {
                    if (slot == _ctaSlot) continue;
                    int captured = slot;
                    int occupant = EffectiveSlotProject(slot);
                    Or(occupant >= 0 ? $"{slot + 1} {UiText.SlotNames[slot]} · replaces {Name(slot)}" : $"{slot + 1} {UiText.SlotNames[slot]}", () => AssignSelectedToSlot(captured));
                }
                if (queueIndex >= 0) Or("Remove from queue", RemoveSelectedFromQueue);
                else if (shown < 3 && frame.FocusQueueCount < 8) Or("Queue", QueueSelected, "Start it by itself when a slot opens");
            }
            else if (slotOf >= 0 && state is ProjectState.Available or ProjectState.Slotted)
            {
                Or("Stop — keeps progress", () => ClearSlot(slotOf));
            }
            else if (state == ProjectState.Glimpsed && queueIndex >= 0) Or("Remove from queue", RemoveSelectedFromQueue);
        }
        UiKit.SetVisible(_orRow!, shown > 0);
    }

    private readonly Action?[] _orActions = new Action?[3];

    private string ProjectPeopleContextLine(MatchFrame frame, ProjectDef def)
    {
        var context = new StringBuilder();
        string impact = MethodImpactLine(frame, def);
        if (impact.Length > 0) context.Append(impact);
        if (def.Lineage >= 0 && def.Lineage < frame.FocusKnowHowCount && _catalog is not null)
        {
            if (context.Length > 0) context.Append('\n');
            string field = UiText.FieldName(_catalog, def.Lineage, false);
            long stored = frame.FocusKnowHow[def.Lineage];
            context.Append(stored > 0
                ? $"Your people have banked {FormatFixed(stored)} Know-how in {field}; matching projects can spend it on up to half their cost."
                : $"No Know-how is stored in {field} yet. Practising related work builds it for matching projects.");
        }
        return context.ToString();
    }

    private static string MethodImpactLine(MatchFrame frame, ProjectDef def)
    {
        foreach (EffectDef effect in def.Effects)
        {
            if (effect.Kind != EffectKind.Method || !TryGetActivity(effect.Action ?? "", out FigureActivity activity)) continue;
            int rung = (int)effect.Int, people = 0, below = 0;
            for (int i = 0; i < frame.FigureCount; i++)
            {
                if (frame.FigureCiv[i] != frame.FocusCiv || (FigureActivity)frame.FigureActivity[i] != activity ||
                    (((FigureFlags)frame.FigureFlags[i]) & FigureFlags.FreeFolk) != 0) continue;
                people++;
                if (frame.FigureToolRung[i] < rung) below++;
            }
            string workers = WorkPeopleName(effect.Action ?? "");
            if (people == 0) return $"No one is assigned to {WorkPeopleAction(effect.Action ?? "")} this season.";
            string assigned = people == 1 ? "1 person is assigned" : $"{people} {workers} are assigned";
            return below > 0 ? $"{assigned} this season; {below} {(below == 1 ? "person still uses" : "still use")} older tools." : $"{assigned} this season.";
        }
        return "";
    }

    private static string WorkPeopleName(string action) => action switch
    {
        "forage" => "foragers", "hunt" => "hunters", "fish" => "fishers", "farm" => "farmers", "chop" => "woodcutters",
        "gather" => "gatherers", "craft" => "crafters", "build" => "builders", "preserve" => "food preservers",
        "tend_fire" => "fire tenders", "herd" => "herders", _ => "workers",
    };

    private static string WorkPeopleAction(string action) => action switch
    {
        "forage" => "foraging", "hunt" => "hunting", "fish" => "fishing", "farm" => "farming", "chop" => "woodcutting",
        "gather" => "gathering", "craft" => "crafting", "build" => "building", "preserve" => "food preserving",
        "tend_fire" => "tending the fire", "herd" => "herding", _ => "this work",
    };

    private static bool TryGetActivity(string action, out FigureActivity activity)
    {
        activity = action switch
        {
            "forage" => FigureActivity.Forage, "hunt" => FigureActivity.Hunt, "fish" => FigureActivity.Fish, "farm" => FigureActivity.Farm,
            "chop" => FigureActivity.Chop, "gather" => FigureActivity.Gather, "craft" => FigureActivity.Craft, "build" => FigureActivity.Build,
            "preserve" => FigureActivity.Preserve, "tend_fire" => FigureActivity.TendFire, "herd" => FigureActivity.Herd,
            _ => (FigureActivity)byte.MaxValue,
        };
        return (byte)activity != byte.MaxValue;
    }

    private void EnsureDependents()
    {
        if (_catalog is null) return;
        var lists = new List<int>[_catalog.Count];
        foreach (ProjectDef d in _catalog.Projects)
        {
            foreach (int r in d.Requires) (lists[r] ??= new List<int>()).Add(d.Index);
            foreach (int r in d.RequiresAny) (lists[r] ??= new List<int>()).Add(d.Index);
        }
        _dependents = new int[_catalog.Count][];
        for (int i = 0; i < lists.Length; i++) _dependents[i] = lists[i]?.ToArray() ?? Array.Empty<int>();
    }

    private static bool MetalAccessMet(MatchFrame frame, int civ, int bit)
        => (uint)civ < (uint)frame.CivMetalAccess.Length && (frame.CivMetalAccess[civ] & bit) != 0;

    /// <summary>The metal a Blocked project is missing, or null when its gate is something else.</summary>
    private string? BlockedByMetal(MatchFrame frame, ProjectDef def)
    {
        int civ = Math.Max(0, frame.FocusCiv);
        foreach (Gate gate in def.Gates)
            if (UiText.MetalGate(_catalog!, gate, out int bit, out string metal) && !MetalAccessMet(frame, civ, bit)) return metal;
        return null;
    }

    private void BuildNeeds(MatchFrame frame, ProjectDef def, ProjectState state)
    {
        int allUsed = 0, anyUsed = 0, hiddenRequired = 0;
        foreach (int r in def.Requires)
        {
            ProjectState s = StateOf(r);
            if (s == ProjectState.Hidden && !IsRevealed(r)) { hiddenRequired++; continue; }
            BindNeedRow(_needsPool, allUsed++, _needsList!, r, s);
        }
        HideFrom(_needsPool, allUsed);
        foreach (int r in def.RequiresAny)
        {
            ProjectState s = StateOf(r);
            if (s == ProjectState.Hidden) continue;
            BindNeedRow(_needsAnyPool, anyUsed++, _needsAnyList!, r, s);
        }
        HideFrom(_needsAnyPool, anyUsed);
        UiKit.SetVisible(_needsHeaderAll!, allUsed > 0);
        UiKit.SetVisible(_needsHeaderAny!, anyUsed > 0);
        UiKit.SetVisible(_needsList!, allUsed > 0);
        UiKit.SetVisible(_needsAnyList!, anyUsed > 0);

        var note = new StringBuilder();
        if (hiddenRequired > 0) note.Append(hiddenRequired == 1 ? "1 more you haven't discovered yet.\n" : $"{hiddenRequired} more you haven't discovered yet.\n");
        if (def.RequiresAny.Length > 0 && anyUsed == 0) note.Append("Another route you haven't discovered yet.\n");
        int focusCiv = Math.Max(0, frame.FocusCiv);
        foreach (Gate gate in def.Gates)
        {
            note.Append(gate.Kind == GateKind.Seen ? "✓ " : "• ").Append(UiText.GateText(_catalog!, gate)).Append('\n');
            if (UiText.MetalGate(_catalog!, gate, out int bit, out string metal))
                note.Append(UiText.MetalGateNote(metal, MetalAccessMet(frame, focusCiv, bit), state is ProjectState.Completed or ProjectState.Dormant)).Append('\n');
        }
        if (def.Requires.Length == 0 && def.RequiresAny.Length == 0 && def.Gates.Length == 0 && !def.IsKeystone)
            note.Append(state is ProjectState.Available or ProjectState.Slotted ? "Nothing — you can start it now." : "No earlier project is needed.");
        string noteText = note.ToString().Replace("✓ ", "").TrimEnd('\n');
        UiKit.SetText(_needsNote!, noteText);
        UiKit.SetVisible(_needsNote!, noteText.Length > 0);

        bool keystone = def.IsKeystone && def.BreadthEra >= 0;
        UiKit.SetVisible(_needsKeystone!, keystone);
        UiKit.SetVisible(_needsKeystoneBar!, keystone);
        if (keystone)
        {
            int civ = Math.Max(0, frame.FocusCiv);
            int done = frame.CivBreadthDone[civ], needed = Math.Max(1, def.BreadthMin);
            UiKit.SetText(_needsKeystone!, $"{Math.Min(done, needed)}/{needed} completed {UiText.AgeName(def.BreadthEra)} projects");
            _needsKeystoneBar!.SetValue(Math.Min(1f, done / (float)needed), false);
        }
    }

    private void BindNeedRow(List<UiProjectCard> pool, int index, Control parent, int project, ProjectState state)
    {
        ProjectDef def = _catalog!.Projects[project];
        UiProjectCard row = GetPooled(pool, index, () => NewProjectCard(ProjectCardMode.Row), parent);
        ProjectLook look = BuildLook(def, false);
        look.Selected = false;
        int slot = SlotOfProject(project);
        look.Sub = state switch
        {
            ProjectState.Completed => "known",
            ProjectState.Slotted => slot >= 0 && _pendingProject[slot] == -2 ? $"in {UiText.SlotNames[slot]}, {_current!.FocusSlotPermille[slot] / 10}%" : "in progress",
            ProjectState.Available => "ready",
            ProjectState.Glimpsed => "almost ready",
            ProjectState.Dormant => "blocked",
            ProjectState.Hidden => "not in reach yet",
            _ => "",
        };
        row.Bind(def, look);
        row.Visible = true;
    }

    private void BuildLeads(ProjectDef def)
    {
        int used = 0, more = 0;
        if (def.Index < _dependents.Length)
            foreach (int d in _dependents[def.Index])
            {
                ProjectState s = StateOf(d);
                if ((s == ProjectState.Hidden && !IsRevealed(d)) || !_catalog!.Projects[d].Numeric) continue;
                if (used >= 8) { more++; continue; }
                ProjectDef dependent = _catalog.Projects[d];
                UiProjectCard chip = GetPooled(_leadsPool, used, () => NewProjectCard(ProjectCardMode.Chip), _leadsList!);
                ProjectLook look = BuildLook(dependent, false);
                look.Selected = false;
                float width = Math.Clamp(UiTheme.Medium.GetStringSize(dependent.Name, HorizontalAlignment.Left, -1, UiTokens.FontBody).X + 84, 144, 233);
                chip.CustomMinimumSize = new Vector2(width, UiTokens.ControlS);
                chip.Bind(dependent, look);
                chip.Visible = true;
                used++;
            }
        HideFrom(_leadsPool, used);
        UiKit.SetVisible(_leadsBlock!, used > 0 || def.IsKeystone);
        if (used == 0 && !def.IsKeystone) _leadsBlock!.Visible = false;
        _ = more;
    }

    // ---- actions -----------------------------------------------------------------------------

    private void OnCtaPressed()
    {
        switch (_cta)
        {
            case CtaKind.Start:
            case CtaKind.Replace:
            case CtaKind.Move:
                AssignSelectedToSlot(_ctaSlot);
                break;
            case CtaKind.ChooseSlot:
                _replaceRowOpen = !_replaceRowOpen;
                _inspSignature = int.MinValue;
                RefreshResearchWebNow();
                break;
            case CtaKind.Queue:
                QueueSelected();
                break;
        }
    }

    private void StartSelectedProject()
    {
        int target = ResolveTargetSlot();
        if (_selectedResearchProject < 0) return;
        ProjectState state = StateOf(_selectedResearchProject);
        if (IsRevealed(_selectedResearchProject))
        {
            ShowFooterNote($"{_catalog!.Projects[_selectedResearchProject].Name} needs {WhatItNeeds(_catalog.Projects[_selectedResearchProject])} first.");
            return;
        }
        if (state == ProjectState.Glimpsed) { QueueSelected(); return; }
        if (target >= 0) AssignSelectedToSlot(target);
        else { _replaceRowOpen = true; _inspSignature = int.MinValue; RefreshResearchWebNow(); }
    }

    private void AssignSelectedToSlot(int slot) => AssignResearch(_selectedResearchProject, slot);

    private bool CanDropResearchSlot(int from, int to)
    {
        if (_session?.CanIssueCommands != true || _current is null || _catalog is null ||
            (uint)from >= 3u || (uint)to >= 3u || from == to) return false;
        int project = EffectiveSlotProject(from);
        return project >= 0 && StateOf(project) is ProjectState.Available or ProjectState.Slotted;
    }

    private void MoveResearchSlot(int from, int to)
    {
        if (!CanDropResearchSlot(from, to)) return;
        int project = EffectiveSlotProject(from);
        if (project >= 0) AssignResearch(project, to);
    }

    private void AssignResearch(int project, int slot)
    {
        if (_session is null || _current is null || _catalog is null || (uint)slot >= 3u || !_session.CanIssueCommands) return;
        ProjectState state = StateOf(project);
        if (state is not (ProjectState.Available or ProjectState.Slotted)) return;
        int previousSlot = SlotOfProject(project);
        int displaced = EffectiveSlotProject(slot);
        _session.Queue(PlayerCommands.SetSlot(_session.Tick, _current.FocusCiv, slot, project));
        if (_session.Paused)
        {
            MarkPending(slot, project);
            if (previousSlot >= 0 && previousSlot != slot) MarkPending(previousSlot, displaced >= 0 ? displaced : -1);
        }
        if (!_uiSettings.PlayedBefore)
        {
            _uiSettings.PlayedBefore = true;
            _uiSettings.Save();
        }
        bool wasHold = _openingHold || (_session.Paused && IsFreshUnassignedMatch());
        if (wasHold)
        {
            _openingHold = true;
            _openingHoldAssignmentPending = true;
            _session.SetPaused(true);
        }
        _selectedResearchProject = project;
        _webTarget = -1;
        _replaceRowOpen = false;
        string name = _catalog.Projects[project].Name;
        string note = previousSlot >= 0 && previousSlot != slot
            ? $"{name} moved to {UiText.SlotNames[slot]}."
            : displaced >= 0 && displaced != project
                ? $"{name} started in {UiText.SlotNames[slot]}. {_catalog.Projects[displaced].Name} keeps its progress and waits."
                : $"{name} started in {UiText.SlotNames[slot]}.";
        if (wasHold) note += " Time starts when you close the research menu.";
        else if (_session.Paused) note += " It starts when you resume.";
        ShowFooterNote(note);
        _webSignature = int.MinValue;
        _inspSignature = int.MinValue;
        RefreshResearchWebNow();
        UpdatePausedTreatment();
    }

    private void QueueSelected()
    {
        if (_session is null || _current is null || _selectedResearchProject < 0 || !_session.CanIssueCommands) return;
        _session.Queue(PlayerCommands.Enqueue(_session.Tick, _current.FocusCiv, _selectedResearchProject));
        ShowFooterNote($"{_catalog!.Projects[_selectedResearchProject].Name} added to the queue.");
    }

    private void RemoveSelectedFromQueue()
    {
        if (_session is null || _current is null || _selectedResearchProject < 0) return;
        int index = QueueIndexOf(_selectedResearchProject);
        if (index < 0) return;
        _session.Queue(PlayerCommands.Dequeue(_session.Tick, _current.FocusCiv, index));
        ShowFooterNote($"{_catalog!.Projects[_selectedResearchProject].Name} removed from the queue.");
    }

    private void UpdateNewBadge()
    {
        if (_newBadge is null || _newBadgeLabel is null) return;
        _newBadge.Visible = _newProjectCount > 0 && !_researchWebVisible;
        UiKit.SetText(_newBadgeLabel, $"{_newProjectCount} new");
    }

    private void OnResearchProjectCompleted(int project)
    {
        FlashSlotDone(project);
        if (!_researchWebVisible || _catalog is null) return;
        int open = FirstOpenSlot();
        // The slot opens when the frame arrives; point the notice at the first slot that will be free.
        int slot = Array.IndexOf(_lastSlotProject, project);
        if (slot < 0) slot = open;
        _noticeSlot = slot;
        string name = _catalog.Projects[project].Name;
        UiKit.SetText(_noticeLabel!, slot >= 0 ? $"{name} is done. {UiText.SlotNames[slot]} is open." : $"{name} is done.");
        _noticeButton!.Text = slot >= 0 ? $"Pick for {UiText.SlotNames[slot]}" : "";
        _noticeButton.Visible = slot >= 0 && !(_current?.FocusCouncil ?? false);
        _noticeBar!.Visible = true;
        _noticeTimer = 8.0f;
    }

    private void TickResearchWeb(float delta)
    {
        if (_footerNoteTimer > 0)
        {
            _footerNoteTimer -= delta;
            if (_footerNoteTimer <= 0 && _footerNote is not null)
            {
                _footerNote.Text = "Click a project to see it · Double-click or Enter to start · 1 2 3 choose a slot";
                _footerNote.ThemeTypeVariation = "CaptionTertiary";
            }
        }
        if (_noticeTimer > 0)
        {
            _noticeTimer -= delta;
            if (_noticeTimer <= 0 && _noticeBar is not null) _noticeBar.Visible = false;
        }
    }

    /// <summary>Keys that only mean something inside the web. Returns true when handled.</summary>
    private bool HandleWebKey(InputEventKey key)
    {
        if (!_researchWebVisible) return false;
        bool typing = GetViewport().GuiGetFocusOwner() is LineEdit;
        if (typing) return false;
        switch (key.Keycode)
        {
            case Key.Key1 or Key.Key2 or Key.Key3 or Key.Kp1 or Key.Kp2 or Key.Kp3:
                {
                    int slot = key.Keycode is Key.Key1 or Key.Kp1 ? 0 : key.Keycode is Key.Key2 or Key.Kp2 ? 1 : 2;
                    if (_selectedResearchProject >= 0) AssignSelectedToSlot(slot);
                    return true;
                }
            case Key.Enter or Key.KpEnter:
                if (GetViewport().GuiGetFocusOwner() is UiProjectCard or ResearchRadialMap or null) { OnCtaPressed(); return true; }
                return false;
            case Key.Minus or Key.KpSubtract when _webView == ViewMap:
                _mapView?.ZoomBy(1 / 1.3f);
                return true;
            case Key.Equal or Key.Plus or Key.KpAdd when _webView == ViewMap:
                _mapView?.ZoomBy(1.3f);
                return true;
            case Key.Home when _webView == ViewMap:
                _mapView?.FitVisible();
                return true;
            case Key.Slash:
                _webSearch?.GrabFocus();
                return true;
            case Key.Left: MoveWebSelection(-1, 0); return true;
            case Key.Right: MoveWebSelection(1, 0); return true;
            case Key.Up: MoveWebSelection(0, -1); return true;
            case Key.Down: MoveWebSelection(0, 1); return true;
        }
        return false;
    }

    private void MoveWebSelection(int dx, int dy)
    {
        if (_webView == ViewMap && _mapView is not null)
        {
            int neighbour = _mapView.Neighbour(_selectedResearchProject, dx, dy);
            if (neighbour >= 0 && neighbour != _selectedResearchProject) SelectResearchProject(neighbour);
            _mapView.GrabFocus();
            return;
        }
        if (_navCards.Count == 0) return;
        int current = _navCards.FindIndex(c => c.Project == _selectedResearchProject);
        if (current < 0) { SelectResearchProject(_navCards[0].Project); return; }
        UiProjectCard from = _navCards[current];
        int next = current;
        if (dy != 0) next = Math.Clamp(current + dy, 0, _navCards.Count - 1);
        else
        {
            // nearest card in the horizontal direction at a similar height
            float bestScore = float.MaxValue;
            Vector2 origin = from.GlobalPosition;
            for (int i = 0; i < _navCards.Count; i++)
            {
                if (i == current) continue;
                Vector2 pos = _navCards[i].GlobalPosition;
                float dxx = (pos.X - origin.X) * dx;
                if (dxx <= 4) continue;
                float score = dxx + Math.Abs(pos.Y - origin.Y) * 4;
                if (score < bestScore) { bestScore = score; next = i; }
            }
        }
        if (next != current)
        {
            SelectResearchProject(_navCards[next].Project);
            _navCards[next].GrabFocus();
        }
    }
}
