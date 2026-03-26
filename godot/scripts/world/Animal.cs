#nullable disable
using Godot;

public enum AnimalType { Deer, Boar, Rabbit }

/// <summary>
/// Simple prey animal. Wanders, flees when NPCs get too close.
/// Can be hunted by coordinated NPCs.
/// </summary>
public partial class Animal : Node3D
{
    [Export] public AnimalType Type       { get; set; } = AnimalType.Deer;
    [Export] public float      Health     { get; set; } = 3f;
    [Export] public float      FleeRadius { get; set; } = 8f;
    [Export] public float      MoveSpeed  { get; set; } = 4f;
    [Export] public float      FoodValue  { get; set; } = 5f;

    private RandomNumberGenerator _rng = new();
    private Vector3 _wanderTarget;
    private double  _wanderTimer = 0;
    private bool    _fleeing     = false;

    public bool IsDead => Health <= 0f;

    private WorldObjectEntry _registryEntry;

    public override void _Ready()
    {
        _rng.Randomize();
        _wanderTarget = GlobalPosition;
        AnimalManager.Instance?.Register(this);
        string icon = Type switch { AnimalType.Deer => "🦌", AnimalType.Boar => "🐗", _ => "🐇" };
        _registryEntry = new WorldObjectEntry(this, WorldObjectKind.Animal, Type.ToString(), icon);
        WorldObjectRegistry.Instance?.Register(_registryEntry);
    }

    public override void _Process(double delta)
    {
        if (IsDead) return;

        // Check for nearby NPCs — flee if too close
        Vector3 fleeDir = Vector3.Zero;
        _fleeing = false;

        if (GameManager.Instance != null)
        {
            foreach (var npc in GameManager.Instance.AllNpcs)
            {
                var d = GlobalPosition - npc.GlobalPosition;
                if (d.Length() < FleeRadius)
                {
                    fleeDir += d.Normalized() / Mathf.Max(d.Length(), 0.1f);
                    _fleeing = true;
                }
            }
        }

        if (_fleeing)
        {
            fleeDir = fleeDir.Normalized();
            fleeDir.Y = 0;
            GlobalPosition += fleeDir * MoveSpeed * 1.5f * (float)delta;
            return;
        }

        // Wander
        var dir = _wanderTarget - GlobalPosition;
        dir.Y = 0;
        if (dir.Length() > 0.5f)
            GlobalPosition += dir.Normalized() * MoveSpeed * (float)delta;

        _wanderTimer += delta;
        if (_wanderTimer > _rng.RandfRange(3f, 7f))
        {
            _wanderTimer = 0;
            _wanderTarget = GlobalPosition + new Vector3(
                _rng.RandfRange(-20f, 20f), 0, _rng.RandfRange(-20f, 20f));
        }
    }

    /// <summary>NPC strikes the animal. Returns true if killed.</summary>
    public bool Strike(float damage = 1f)
    {
        Health -= damage;
        if (Health <= 0f)
        {
            Die();
            return true;
        }
        return false;
    }

    private void Die()
    {
        // Spawn food resource at death location
        var food = new ResourceNode();
        food.Type      = ResourceType.Food;
        food.Amount    = FoodValue;
        food.MaxAmount = FoodValue;
        food.RespawnRate = 0f; // carcass doesn't respawn
        food.Position  = GlobalPosition;

        var mesh = new MeshInstance3D();
        var mat  = new StandardMaterial3D();
        mat.AlbedoColor = new Color(0.8f, 0.3f, 0.1f);
        var sphere = new SphereMesh();
        sphere.Radius = 0.3f;
        sphere.Height = 0.6f;
        sphere.SurfaceSetMaterial(0, mat);
        mesh.Mesh = sphere;
        food.AddChild(mesh);

        GetParent().AddChild(food);
        GD.Print($"[Animal] {Type} killed — dropped {FoodValue} food.");
        AnimalManager.Instance?.Unregister(this);
        QueueFree();
    }

    public override void _ExitTree()
    {
        AnimalManager.Instance?.Unregister(this);
        if (_registryEntry != null) WorldObjectRegistry.Instance?.Unregister(_registryEntry);
    }
}
