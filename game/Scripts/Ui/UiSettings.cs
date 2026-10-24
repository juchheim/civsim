using Godot;

namespace CivSim.Game.Ui;

/// <summary>Player UI preferences, kept in user://settings.cfg. Reading and writing never throws into the game.</summary>
public sealed class UiSettings
{
    private const string Section = "ui";
    private const string Path = "user://settings.cfg";

    public static UiSettings Current { get; private set; } = new();

    public float UiScale = 1.0f;
    public bool ReduceMotion;
    public bool ShowHints = true;
    public bool ShowTips = true;
    public bool PauseInWeb;
    public bool PauseOnDanger;
    public bool PlayedBefore;
    public bool HintsPanned, HintsZoomed, HintsTurned;
    public bool TrackpadSeen;
    /// <summary>The rival level chosen for the next new match. New players start on Settler, the gentlest.</summary>
    public CivSim.Sim.Ai.Difficulty Difficulty = CivSim.Sim.Ai.Difficulty.Settler;

    public static UiSettings Load()
    {
        var settings = new UiSettings();
        try
        {
            var file = new ConfigFile();
            if (file.Load(Path) == Error.Ok)
            {
                settings.UiScale = Mathf.Clamp((float)file.GetValue(Section, "ui_scale", 1.0f).AsDouble(), 1.0f, 1.3f);
                settings.ReduceMotion = file.GetValue(Section, "reduce_motion", false).AsBool();
                settings.ShowHints = file.GetValue(Section, "show_hints", true).AsBool();
                settings.ShowTips = file.GetValue(Section, "show_tips", true).AsBool();
                settings.PauseInWeb = file.GetValue(Section, "pause_in_web", false).AsBool();
                settings.PauseOnDanger = file.GetValue(Section, "pause_on_danger", false).AsBool();
                settings.PlayedBefore = file.GetValue(Section, "played_before", false).AsBool();
                settings.HintsPanned = file.GetValue(Section, "hint_panned", false).AsBool();
                settings.HintsZoomed = file.GetValue(Section, "hint_zoomed", false).AsBool();
                settings.HintsTurned = file.GetValue(Section, "hint_turned", false).AsBool();
                settings.TrackpadSeen = file.GetValue(Section, "trackpad_seen", false).AsBool();
                int level = file.GetValue(Section, "difficulty", (int)CivSim.Sim.Ai.Difficulty.Settler).AsInt32();
                settings.Difficulty = System.Enum.IsDefined(typeof(CivSim.Sim.Ai.Difficulty), (byte)level)
                    ? (CivSim.Sim.Ai.Difficulty)level : CivSim.Sim.Ai.Difficulty.Settler;
            }
        }
        catch (System.Exception ex) { GD.PushWarning($"Could not read UI settings: {ex.Message}"); }
        Current = settings;
        return settings;
    }

    public void Save()
    {
        try
        {
            var file = new ConfigFile();
            file.SetValue(Section, "ui_scale", UiScale);
            file.SetValue(Section, "reduce_motion", ReduceMotion);
            file.SetValue(Section, "show_hints", ShowHints);
            file.SetValue(Section, "show_tips", ShowTips);
            file.SetValue(Section, "pause_in_web", PauseInWeb);
            file.SetValue(Section, "pause_on_danger", PauseOnDanger);
            file.SetValue(Section, "played_before", PlayedBefore);
            file.SetValue(Section, "hint_panned", HintsPanned);
            file.SetValue(Section, "hint_zoomed", HintsZoomed);
            file.SetValue(Section, "hint_turned", HintsTurned);
            file.SetValue(Section, "trackpad_seen", TrackpadSeen);
            file.SetValue(Section, "difficulty", (int)Difficulty);
            file.Save(Path);
        }
        catch (System.Exception ex) { GD.PushWarning($"Could not save UI settings: {ex.Message}"); }
    }
}
