using CivSim.Sim.World;
using Godot;

namespace CivSim.Game.Looks;

/// <summary>The current low-poly diorama look. Painted and pixel looks can implement the same boundary later.</summary>
public sealed class LowPoly3DLook : IGameLook
{
    public string Name => "Low-poly 3D";
    public Shader TerrainShader { get; } = ResourceLoader.Load<Shader>("res://Shaders/terrain.gdshader")
        ?? throw new System.InvalidOperationException("Missing low-poly terrain shader.");
    public Shader WaterShader { get; } = ResourceLoader.Load<Shader>("res://Shaders/water.gdshader")
        ?? throw new System.InvalidOperationException("Missing low-poly water shader.");

    public Color TerrainColor(byte biome) => (Biome)biome switch
    {
        Biome.Ocean => new Color("#244655"), Biome.Coast => new Color("#b9aa82"),
        Biome.Ice => new Color("#dce6e8"), Biome.Tundra => new Color("#9ea67c"),
        Biome.Taiga => new Color("#5b7447"), Biome.TemperateForest => new Color("#5f8137"),
        Biome.OpenWoodland => new Color("#7d9842"), Biome.GrasslandSteppe => new Color("#8ba346"),
        Biome.Savanna => new Color("#a6994c"), Biome.TropicalForest => new Color("#557f36"),
        Biome.Wetland => new Color("#6c8a58"), Biome.Desert => new Color("#dcc289"),
        Biome.Mountain => new Color("#7d7466"), _ => new Color("#8f877c"),
    };

    public Color CliffColor(byte biome) => (Biome)biome switch
    {
        Biome.Ocean or Biome.Coast => new Color("#a89268"),
        Biome.Mountain or Biome.Peak => new Color("#6f675e"),
        Biome.Desert => new Color("#b8976a"),
        Biome.Ice => new Color("#b8c8cf"),
        _ => new Color("#765b42"),
    };

    public Color StructureTint(int kind, Color civ)
    {
        Color material = (kind switch
        {
            0 or 7 or 9 or 10 or 11 => new Color("#a78960"),
            1 or 2 or 3 => new Color("#ad8c5f"),
            12 => new Color("#806344"),
            15 => new Color("#729044"),
            16 => new Color("#89794b"),
            17 => new Color("#765338"),
            _ => new Color("#946e4b"),
        }).SrgbToLinear();
        return material.Lerp(civ, kind == 0 ? 0.15f : 0.34f);
    }

    public StandardMaterial3D LitVertexMaterial() => new()
    {
        AlbedoColor = Colors.White,
        VertexColorUseAsAlbedo = true,
        Roughness = 0.92f,
        Metallic = 0.0f,
    };

    public StandardMaterial3D UnshadedVertexMaterial() => new()
    {
        AlbedoColor = Colors.White,
        VertexColorUseAsAlbedo = true,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
    };

    public Mesh CreateFigureBody() => new CapsuleMesh { Radius = 0.075f, Height = 0.32f, RadialSegments = 7, Rings = 2 };
    public Mesh CreateSphere() => new SphereMesh { Radius = 1, Height = 2, RadialSegments = 7, Rings = 3 };
    public Mesh CreateBox(Vector3 size) => new BoxMesh { Size = size };
    public Mesh CreateCylinder(float top, float bottom, float height, int segments = 7)
        => new CylinderMesh { TopRadius = top, BottomRadius = bottom, Height = height, RadialSegments = segments, Rings = 1 };

    public Mesh CreateStructure(int kind) => kind switch
    {
        0 => CreateCylinder(0.12f, 0.14f, 0.07f, 8),
        1 or 2 => CreateCylinder(0.015f, 0.22f, 0.34f, 7),
        3 => new SphereMesh { Radius = 0.26f, Height = 0.25f, RadialSegments = 7, Rings = 2 },
        4 or 5 or 6 => CreateBox(new Vector3(0.38f, 0.30f, 0.34f)),
        7 or 9 => CreateCylinder(0.14f, 0.17f, 0.23f, 7),
        8 or 10 or 11 => CreateCylinder(0.12f, 0.16f, 0.20f, 7),
        12 => CreateCylinder(0.035f, 0.045f, 0.38f, 5),
        13 => CreateCylinder(0.10f, 0.16f, 0.58f, 6),
        14 => CreateBox(new Vector3(0.48f, 0.06f, 0.10f)),
        15 => CreateBox(new Vector3(0.55f, 0.025f, 0.42f)),
        16 => CreateCylinder(0.22f, 0.27f, 0.08f, 7),
        17 => CreateCylinder(0.035f, 0.05f, 0.32f, 5),
        _ => CreateBox(new Vector3(0.26f, 0.20f, 0.26f)),
    };
}
