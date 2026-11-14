using Godot;

namespace CivSim.Game.Ui;

/// <summary>One age's skin: its accent tint and, loaded lazily, the three raster pieces (docs/MVP/34-era-ui-skins.md).</summary>
public sealed class UiSkinSet
{
    public string Slug { get; }
    public string Name { get; }
    /// <summary>Colour of the hairline border and the base for flourishes; matches the tint the images were drawn in.</summary>
    public Color Tint { get; }
    /// <summary>Evens out how visible each texture is (some patterns carry more ink than others at the same alpha).</summary>
    public float Strength { get; }
    private Texture2D? _tex, _corner, _crest;
    private bool _loaded;

    public UiSkinSet(string slug, string name, string tint, float strength = 1f)
    {
        Slug = slug;
        Name = name;
        Tint = new Color(tint);
        Strength = strength;
    }

    /// <summary>Seamless 256 px tile; its alpha channel carries the pattern.</summary>
    public Texture2D? Tex { get { Load(); return _tex; } }
    /// <summary>Top-left corner flourish, 96 px square; the renderer flips it for the other corners.</summary>
    public Texture2D? Corner { get { Load(); return _corner; } }
    /// <summary>Symmetric 384×24 ornament for the top edge of sheets.</summary>
    public Texture2D? Crest { get { Load(); return _crest; } }

    private void Load()
    {
        if (_loaded) return;
        _loaded = true;
        _tex = Read("panel_tex");
        _corner = Read("corner");
        _crest = Read("crest");
    }

    private Texture2D? Read(string file)
    {
        string path = $"res://assets/ui/skins/{Slug}/{file}.png";
        return ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null;
    }
}

/// <summary>The ten age skins, indexed by the sim's age id (content/registries/eras.yaml).</summary>
public static class UiSkins
{
    public static readonly UiSkinSet[] All =
    {
        new("dawn", "Dawn", "#C8A06A"),
        new("hunters", "Hunters", "#B7855A"),
        new("roots", "Roots", "#8FAE6A", 0.8f),
        new("bronze", "Bronze", "#CD8F46", 0.8f),
        new("iron", "Iron", "#8FA1B3"),
        new("faith", "Faith & Fief", "#D1A94E", 1.2f),
        new("discovery", "Discovery", "#D8B972", 1.2f),
        new("industry", "Industry", "#C57A3E"),
        new("modern", "Modern", "#8DB1CF", 1.2f),
        new("information", "Information", "#4FD6C2"),
    };

    public static UiSkinSet For(int era) => All[System.Math.Clamp(era, 0, All.Length - 1)];
}
