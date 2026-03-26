#nullable disable
using Godot;

public enum ResourceType
{
    Food,    // Beeren, Früchte, Pilze
    Water,   // Fluss, Quelle
    Wood,    // Bäume
    Stone    // Felsen
}

/// <summary>
/// A harvestable world resource. NPCs seek these to survive.
/// Respawns after a cooldown.
/// </summary>
public partial class ResourceNode : Node3D
{
    [Export] public ResourceType Type       { get; set; } = ResourceType.Food;
    [Export] public float        Amount     { get; set; } = 10f;
    [Export] public float        MaxAmount  { get; set; } = 10f;
    [Export] public float        RespawnRate { get; set; } = 0.5f; // per world tick

    private MeshInstance3D _mesh;
    private Label3D        _label;

    public bool IsEmpty => Amount <= 0f;

    public override void _Ready()
    {
        _mesh  = GetNodeOrNull<MeshInstance3D>("MeshInstance3D");
        _label = GetNodeOrNull<Label3D>("Label3D");
        UpdateVisual();
        ResourceManager.Instance?.Register(this);
    }

    /// <summary>Harvest some amount. Returns how much was actually taken.</summary>
    public float Harvest(float requested = 1f)
    {
        float taken = Mathf.Min(requested, Amount);
        Amount -= taken;
        UpdateVisual();
        return taken;
    }

    public void OnWorldTick(double delta)
    {
        if (Amount < MaxAmount)
        {
            Amount = Mathf.Min(Amount + RespawnRate * (float)delta, MaxAmount);
            UpdateVisual();
        }
    }

    private void UpdateVisual()
    {
        if (_mesh == null) return;
        float t = Amount / MaxAmount;
        _mesh.Visible = !IsEmpty;

        if (_label != null)
            _label.Text = IsEmpty ? "" : $"{Amount:F0}";
    }

    public override void _ExitTree()
    {
        ResourceManager.Instance?.Unregister(this);
    }
}
