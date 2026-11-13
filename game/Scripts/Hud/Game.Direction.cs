using System;
using CivSim.Game.Ui;
using CivSim.Sim.Core;
using CivSim.Sim.View;
using Godot;

namespace CivSim.Game;

/// <summary>The civilization-wide direction control, kept separate from Posture and individual orders.</summary>
public partial class Game
{
    private void CreateCivilizationDirectionModal()
    {
        _directionModal = MakeModal("direction", 987, 610, "Civilization direction",
            "Your people favor this direction when choosing what to do next. Your direct orders take priority.",
            () => { CloseCivilizationDirectionModal(); return true; });
        _directionModal.Actions.AddChild(UiKit.Ghost("Close", CloseCivilizationDirectionModal, null, UiGlyph.None, UiTokens.ControlM, true));
    }

    private void ToggleCivilizationDirection()
    {
        if (_directionModal is null || _current is null) return;
        if (_directionModal.Open) { CloseCivilizationDirectionModal(); return; }
        RefreshCivilizationDirectionModal(_current);
        OpenModal(_directionModal);
    }

    private void CloseCivilizationDirectionModal()
    {
        if (_directionModal is { Open: true } modal) CloseModal(modal);
    }

    private void RefreshCivilizationDirectionUi(MatchFrame frame)
    {
        if (_btnDirection is not null)
        {
            CivilizationDirection shown = PendingCivilizationDirection(frame);
            bool pending = shown != frame.ViewerDirection;
            _btnDirection.Disabled = frame.ViewerCiv < 0 || _session?.CanIssueCommands != true;
            _btnDirection.TooltipText = pending
                ? $"Civilization direction: {CivilizationGuidance.Name(shown)} is queued. Click to change it."
                : $"Civilization direction: {CivilizationGuidance.Name(shown)}. Click to guide future autonomous decisions.";
        }
        if (_directionModal is { Open: true }) RefreshCivilizationDirectionModal(frame);
    }

    private CivilizationDirection PendingCivilizationDirection(MatchFrame frame)
    {
        CivilizationDirection direction = frame.ViewerDirection;
        if (_session is null || frame.ViewerCiv < 0) return direction;
        foreach (Command command in _session.PendingCommands)
            if (command.Civ == frame.ViewerCiv && command.Kind == CommandKind.SetCivilizationDirection &&
                command.A is >= 0 and <= (int)CivilizationDirection.Discovery)
                direction = (CivilizationDirection)command.A;
        return direction;
    }

    private void RefreshCivilizationDirectionModal(MatchFrame frame)
    {
        if (_directionModal is null || _session is null) return;
        foreach (Node child in _directionModal.Body.GetChildren())
        {
            _directionModal.Body.RemoveChild(child);
            child.QueueFree();
        }

        CivilizationDirection shown = PendingCivilizationDirection(frame);
        bool pending = shown != frame.ViewerDirection;
        var status = UiKit.Text(pending
                ? $"{CivilizationGuidance.Name(shown)} is queued and will apply on the next simulation tick."
                : $"Current direction: {CivilizationGuidance.Name(shown)}.",
            pending ? "BodyStrongAccent" : "BodyStrong");
        _directionModal.Body.AddChild(status);
        _directionModal.Body.AddChild(UiKit.Text(
            "A non-Balanced direction lets the Civ Mind make discretionary diplomatic and military decisions for you; direct orders still take priority. Research guidance applies when Auto-fill is on. Survival work and foundation rules come first, settlement focuses keep priority, and your Posture is unchanged.",
            "Caption", wrap: true));

        var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        foreach (CivilizationDirection direction in Enum.GetValues<CivilizationDirection>())
        {
            bool selected = direction == shown;
            string marker = selected ? pending ? "QUEUED · " : "CURRENT · " : string.Empty;
            string text = $"{marker}{CivilizationGuidance.Name(direction)}\n{CivilizationGuidance.Description(direction)}\n" +
                $"Research: {CivilizationGuidance.ResearchSummary(direction)}\n" +
                $"Build: {CivilizationGuidance.BuildingSummary(direction)}\n" +
                $"Politics: {CivilizationGuidance.PoliticsSummary(direction)}";
            UiButton card = selected
                ? UiKit.Primary(text, () => ChooseCivilizationDirection(direction), height: 120)
                : UiKit.Secondary(text, () => ChooseCivilizationDirection(direction), height: 120);
            card.Alignment = HorizontalAlignment.Left;
            card.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            card.CustomMinimumSize = new Vector2(0, 120);
            card.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            card.FocusMode = Control.FocusModeEnum.All;
            card.TooltipText = CivilizationGuidance.Description(direction);
            card.Disabled = frame.ViewerCiv < 0 || !_session.CanIssueCommands || selected && !pending;
            grid.AddChild(card);
        }
        _directionModal.Body.AddChild(grid);
        if (_session.IsReplay)
            status.TooltipText = "This replay is read-only.";
    }

    private void ChooseCivilizationDirection(CivilizationDirection direction)
    {
        if (_session?.CanIssueCommands != true || _current is null || _current.ViewerCiv < 0) return;
        QueueCommand(PlayerCommands.SetCivilizationDirection(_session.Tick, _current.ViewerCiv, direction));
        if (_current is not null) RefreshCivilizationDirectionModal(_current);
    }
}
