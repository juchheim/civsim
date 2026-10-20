using Godot;

namespace CivSim.Game.Looks;

/// <summary>
/// The Godot-only visual boundary. A look owns shaders, terrain palette, actor materials and primitive meshes;
/// it never receives a MatchModel and cannot change simulation state.
/// </summary>
public interface IGameLook
{
    string Name { get; }
    Shader TerrainShader { get; }
    Shader WaterShader { get; }
    Color TerrainColor(byte biome);
    Color CliffColor(byte biome);
    Color StructureTint(int kind, Color civilization);
    StandardMaterial3D LitVertexMaterial();
    StandardMaterial3D UnshadedVertexMaterial();
    Mesh CreateFigureBody();
    Mesh CreateSphere();
    Mesh CreateBox(Vector3 size);
    Mesh CreateCylinder(float top, float bottom, float height, int segments = 7);
    Mesh CreateStructure(int kind);
}
