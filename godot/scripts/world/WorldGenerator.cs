#nullable disable
using Godot;

/// <summary>
/// Procedurally scatters resource nodes across the terrain at startup.
/// Creates a living, harvestable world.
/// </summary>
public partial class WorldGenerator : Node3D
{
    [Export] public int   FoodCount  { get; set; } = 25;
    [Export] public int   WaterCount { get; set; } = 8;
    [Export] public int   WoodCount  { get; set; } = 30;
    [Export] public int   StoneCount { get; set; } = 15;
    [Export] public float WorldSize  { get; set; } = 45f; // half-extent of terrain

    public override void _Ready()
    {
        var rng = new RandomNumberGenerator();
        rng.Randomize();

        SpawnResources(rng, ResourceType.Food,  FoodCount,  0.4f, new Color(0.2f, 0.8f, 0.2f));  // green
        SpawnResources(rng, ResourceType.Water, WaterCount, 0.2f, new Color(0.2f, 0.5f, 1.0f));  // blue
        SpawnResources(rng, ResourceType.Wood,  WoodCount,  0.5f, new Color(0.4f, 0.25f, 0.1f)); // brown
        SpawnResources(rng, ResourceType.Stone, StoneCount, 0.3f, new Color(0.6f, 0.6f, 0.6f));  // grey

        GD.Print($"[WorldGenerator] World populated: {FoodCount} food, {WaterCount} water, {WoodCount} wood, {StoneCount} stone.");
    }

    private void SpawnResources(RandomNumberGenerator rng, ResourceType type, int count, float scale, Color color)
    {
        for (int i = 0; i < count; i++)
        {
            var node = new ResourceNode();
            node.Type      = type;
            node.MaxAmount = type == ResourceType.Food  ? 8f
                           : type == ResourceType.Water ? 50f
                           : type == ResourceType.Wood  ? 15f
                           : 20f; // stone
            node.Amount    = node.MaxAmount;
            node.RespawnRate = type == ResourceType.Stone ? 0.05f
                             : type == ResourceType.Wood  ? 0.1f
                             : 0.5f;

            float x = rng.RandfRange(-WorldSize, WorldSize);
            float z = rng.RandfRange(-WorldSize, WorldSize);
            node.Position = new Vector3(x, 0.0f, z);

            // Visual mesh
            var mesh = new MeshInstance3D();
            var mat  = new StandardMaterial3D();
            mat.AlbedoColor = color;

            Mesh shape = type switch
            {
                ResourceType.Food  => CreateSphereMesh(scale * 0.4f, mat),
                ResourceType.Water => CreateCylinderMesh(scale * 0.8f, 0.15f, mat),
                ResourceType.Wood  => CreateCylinderMesh(scale * 0.3f, 1.2f, mat),
                ResourceType.Stone => CreateSphereMesh(scale * 0.6f, mat),
                _                  => CreateSphereMesh(0.3f, mat)
            };

            mesh.Mesh = shape;
            mesh.Position = new Vector3(0, scale * 0.5f, 0);
            node.AddChild(mesh);

            // Small label
            var label = new Label3D();
            label.Text      = "";
            label.FontSize  = 20;
            label.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
            label.Position  = new Vector3(0, scale + 0.6f, 0);
            node.AddChild(label);

            AddChild(node);
        }
    }

    private static SphereMesh CreateSphereMesh(float radius, StandardMaterial3D mat)
    {
        var m = new SphereMesh();
        m.Radius = radius;
        m.Height = radius * 2f;
        m.SurfaceSetMaterial(0, mat);
        return m;
    }

    private static CylinderMesh CreateCylinderMesh(float radius, float height, StandardMaterial3D mat)
    {
        var m = new CylinderMesh();
        m.TopRadius    = radius;
        m.BottomRadius = radius;
        m.Height       = height;
        m.SurfaceSetMaterial(0, mat);
        return m;
    }
}
