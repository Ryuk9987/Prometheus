#nullable disable
using Godot;


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

    private WorldObjectEntry _registryEntry;

    public override void _Ready()
    {
        _mesh  = GetNodeOrNull<MeshInstance3D>("MeshInstance3D");
        _label = GetNodeOrNull<Label3D>("Label3D");
        UpdateVisual();
        ResourceManager.Instance?.Register(this);

        string icon = Type switch {
            ResourceType.Food  => "🍎", ResourceType.Water => "💧",
            ResourceType.Wood  => "🪵", ResourceType.Stone => "⬟", _ => "•"
        };
        string name = Type switch {
            ResourceType.Food  => "Nahrungsquelle", ResourceType.Water => "Wasserquelle",
            ResourceType.Wood  => "Baum/Holz",      ResourceType.Stone => "Stein",      _ => Type.ToString()
        };
        _registryEntry = new WorldObjectEntry(this, WorldObjectKind.Resource, name, icon);
        WorldObjectRegistry.Instance?.Register(_registryEntry);
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
        if (_registryEntry != null) WorldObjectRegistry.Instance?.Unregister(_registryEntry);
    }
}
