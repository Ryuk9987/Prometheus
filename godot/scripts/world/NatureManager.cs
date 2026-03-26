#nullable disable
using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Manages all NatureObjects. Spawns initial world, provides queries.
/// </summary>
public partial class NatureManager : Node3D
{
    public static NatureManager Instance { get; private set; }
    private readonly List<NatureObject> _objects = new();

    [Export] public float WorldHalfSize { get; set; } = 44f;

    public IReadOnlyList<NatureObject> Objects => _objects;

    public override void _Ready()
    {
        Instance = this;
        SpawnWorld();
    }

    private void SpawnWorld()
    {
        var rng = new RandomNumberGenerator(); rng.Randomize();

        // ── Trees ──────────────────────────────────────────────────────
        SpawnGroup(rng, NatureObjectType.TreeOak,   18, clump: true);
        SpawnGroup(rng, NatureObjectType.TreePine,  14, clump: true);
        SpawnGroup(rng, NatureObjectType.TreeBirch, 10, clump: false);

        // ── Bushes ─────────────────────────────────────────────────────
        SpawnGroup(rng, NatureObjectType.BushBerry,  12, clump: false);
        SpawnGroup(rng, NatureObjectType.BushPoison,  6, clump: false);

        // ── Mushrooms ──────────────────────────────────────────────────
        SpawnGroup(rng, NatureObjectType.MushroomEdible, 10, clump: true);
        SpawnGroup(rng, NatureObjectType.MushroomPoison,  6, clump: true);

        // ── Rocks ──────────────────────────────────────────────────────
        SpawnGroup(rng, NatureObjectType.RockSmall,  15, clump: false);
        SpawnGroup(rng, NatureObjectType.RockLarge,   6, clump: false);

        // ── Water ──────────────────────────────────────────────────────
        SpawnGroup(rng, NatureObjectType.WaterSource, 5, clump: false);

        GD.Print($"[NatureManager] World populated with {_objects.Count} nature objects.");
    }

    private void SpawnGroup(RandomNumberGenerator rng, NatureObjectType type,
        int count, bool clump)
    {
        // Clumped = pick anchor then scatter nearby; scattered = random
        float cx = clump ? rng.RandfRange(-WorldHalfSize, WorldHalfSize) : 0;
        float cz = clump ? rng.RandfRange(-WorldHalfSize, WorldHalfSize) : 0;
        float spread = clump ? 8f : WorldHalfSize;

        for (int i = 0; i < count; i++)
        {
            var obj = new NatureObject();
            obj.ObjType    = type;
            obj.State      = NatureObject.GrowthState.Mature;
            obj.GrowthAge  = rng.RandfRange(0.6f, 1.0f);
            obj.RegrowTime = type switch {
                NatureObjectType.TreeOak   => 120f,
                NatureObjectType.TreePine  => 100f,
                NatureObjectType.TreeBirch => 80f,
                NatureObjectType.BushBerry or NatureObjectType.BushPoison => 30f,
                NatureObjectType.MushroomEdible or NatureObjectType.MushroomPoison => 20f,
                _ => 60f
            };

            float x = cx + rng.RandfRange(-spread, spread);
            float z = cz + rng.RandfRange(-spread, spread);
            x = Mathf.Clamp(x, -WorldHalfSize, WorldHalfSize);
            z = Mathf.Clamp(z, -WorldHalfSize, WorldHalfSize);
            obj.Position = new Vector3(x, 0, z);
            AddChild(obj);
        }
    }

    public void Register(NatureObject o)   => _objects.Add(o);
    public void Unregister(NatureObject o) => _objects.Remove(o);

    /// <summary>Find nearest harvestable/fellable object of matching types.</summary>
    public NatureObject FindNearest(Vector3 from, NatureObjectType[] types, float maxRange = 50f)
    {
        NatureObject best = null; float bestD = maxRange;
        foreach (var o in _objects)
        {
            if (!types.Contains(o.ObjType)) continue;
            if (!o.IsHarvestable && !o.IsFellable) continue;
            if (o.State == NatureObject.GrowthState.Stump) continue;
            float d = from.DistanceTo(o.GlobalPosition);
            if (d < bestD) { bestD = d; best = o; }
        }
        return best;
    }
}
