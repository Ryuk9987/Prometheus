#nullable disable
using Godot;
using System.Collections.Generic;

public partial class AnimalManager : Node
{
    public static AnimalManager Instance { get; private set; }

    private readonly List<Animal> _animals = new();

    // Mesh colors per animal type
    private static readonly Dictionary<AnimalType, Color> Colors = new()
    {
        { AnimalType.Deer,   new Color(0.8f, 0.65f, 0.3f) },
        { AnimalType.Boar,   new Color(0.45f, 0.3f, 0.2f) },
        { AnimalType.Rabbit, new Color(0.9f, 0.85f, 0.8f) },
    };

    private static readonly Dictionary<AnimalType, float[]> Stats = new()
    {
        // { health, foodValue, fleeRadius, speed }
        { AnimalType.Deer,   new[] { 3f,  5f, 10f, 4.5f } },
        { AnimalType.Boar,   new[] { 5f,  8f,  6f, 3.5f } },
        { AnimalType.Rabbit, new[] { 1f,  2f, 12f, 6.0f } },
    };

    public IReadOnlyList<Animal> Animals => _animals;

    public override void _Ready()
    {
        Instance = this;
        SpawnInitialAnimals();
    }

    private void SpawnInitialAnimals()
    {
        var rng = new RandomNumberGenerator();
        rng.Randomize();
        SpawnGroup(rng, AnimalType.Deer,   8);
        SpawnGroup(rng, AnimalType.Boar,   4);
        SpawnGroup(rng, AnimalType.Rabbit, 12);
    }

    private void SpawnGroup(RandomNumberGenerator rng, AnimalType type, int count)
    {
        float[] s = Stats[type];
        for (int i = 0; i < count; i++)
        {
            var a = new Animal();
            a.Type       = type;
            a.Health     = s[0];
            a.FoodValue  = s[1];
            a.FleeRadius = s[2];
            a.MoveSpeed  = s[3];

            float x = rng.RandfRange(-42f, 42f);
            float z = rng.RandfRange(-42f, 42f);
            a.Position = new Vector3(x, 0.3f, z);

            // Visual
            var mesh = new MeshInstance3D();
            var mat  = new StandardMaterial3D();
            mat.AlbedoColor = Colors[type];
            float r = type == AnimalType.Rabbit ? 0.2f : type == AnimalType.Deer ? 0.35f : 0.4f;
            float h = r * 2.2f;
            var capsule = new CapsuleMesh();
            capsule.Radius = r; capsule.Height = h;
            capsule.SurfaceSetMaterial(0, mat);
            mesh.Mesh = capsule;
            mesh.Position = new Vector3(0, r, 0);
            a.AddChild(mesh);

            AddChild(a);
        }
        GD.Print($"[AnimalManager] Spawned {count} {type}.");
    }

    public void Register(Animal a)   => _animals.Add(a);
    public void Unregister(Animal a) => _animals.Remove(a);

    /// <summary>Find nearest animal within range.</summary>
    public Animal FindNearest(Vector3 from, float maxRange = 30f)
    {
        Animal best = null;
        float bestDist = maxRange;
        foreach (var a in _animals)
        {
            if (a.IsDead) continue;
            float d = from.DistanceTo(a.GlobalPosition);
            if (d < bestDist) { bestDist = d; best = a; }
        }
        return best;
    }
}
