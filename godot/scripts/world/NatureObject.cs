#nullable disable
using Godot;
using System.Collections.Generic;

public enum NatureObjectType
{
    TreeOak, TreePine, TreeBirch,
    BushBerry, BushPoison,
    MushroomEdible, MushroomPoison,
    RockSmall, RockLarge,
    WaterSource,
}

/// <summary>
/// A realistic natural world object.
/// Trees must be felled → yield multiple resource types.
/// Bushes/mushrooms can be harvested repeatedly.
/// Regrows over time. State: Growing → Mature → Harvested → Regrowth
/// </summary>
public partial class NatureObject : Node3D
{
    public enum GrowthState { Sprout, Growing, Mature, Harvested, Stump }

    [Export] public NatureObjectType ObjType    { get; set; } = NatureObjectType.TreeOak;
    [Export] public GrowthState      State      { get; set; } = GrowthState.Mature;
    [Export] public float            GrowthAge  { get; set; } = 1f;   // 0..1
    [Export] public float            RegrowTime { get; set; } = 60f;  // world ticks

    // Visual
    private MeshInstance3D _mesh;
    private Label3D        _label;

    // Regrowth timer
    private float _regrowTimer = 0;

    // Registry entry
    private WorldObjectEntry _regEntry;

    // ── Yield tables ───────────────────────────────────────────────────────
    public static readonly Dictionary<NatureObjectType, List<(ResourceType res, float amount, string label)>> Yields = new()
    {
        [NatureObjectType.TreeOak] = new()
        {
            (ResourceType.LogHardwood, 3f,  "Hartholzstämme"),
            (ResourceType.Branch,      5f,  "Äste"),
            (ResourceType.Bark,        2f,  "Eichenrinde"),
            (ResourceType.Leaf,        4f,  "Blätter"),
        },
        [NatureObjectType.TreePine] = new()
        {
            (ResourceType.LogSoftwood, 3f,  "Weichholzstämme"),
            (ResourceType.Branch,      4f,  "Äste"),
            (ResourceType.Resin,       1f,  "Harz"),
            (ResourceType.Leaf,        3f,  "Nadeln"),
        },
        [NatureObjectType.TreeBirch] = new()
        {
            (ResourceType.LogSoftwood, 2f,  "Birkenholz"),
            (ResourceType.Branch,      3f,  "Äste"),
            (ResourceType.Bark,        3f,  "Birkenrinde"),
        },
        [NatureObjectType.BushBerry] = new()
        {
            (ResourceType.BerryEdible, 4f,  "Essbare Beeren"),
            (ResourceType.Leaf,        1f,  "Blätter"),
        },
        [NatureObjectType.BushPoison] = new()
        {
            (ResourceType.BerryPoison, 3f,  "Giftige Beeren"),
            (ResourceType.Leaf,        1f,  "Blätter"),
        },
        [NatureObjectType.MushroomEdible] = new()
        {
            (ResourceType.MushroomEdible, 2f, "Speisepilze"),
        },
        [NatureObjectType.MushroomPoison] = new()
        {
            (ResourceType.MushroomPoison, 2f, "Giftpilze"),
        },
        [NatureObjectType.RockSmall] = new()
        {
            (ResourceType.Stone, 3f, "Stein"),
        },
        [NatureObjectType.RockLarge] = new()
        {
            (ResourceType.Stone, 8f, "Stein"),
            (ResourceType.Bone,  0f, null), // no bone but reuse slot
        },
        [NatureObjectType.WaterSource] = new()
        {
            (ResourceType.Water, 999f, "Wasser"),
        },
    };

    public bool IsFellable  => ObjType is NatureObjectType.TreeOak
                                       or NatureObjectType.TreePine
                                       or NatureObjectType.TreeBirch;
    public bool IsHarvestable => State == GrowthState.Mature
                              || State == GrowthState.Growing;
    public bool IsTree       => IsFellable;

    public override void _Ready()
    {
        BuildVisuals();
        RegisterSelf();
        NatureManager.Instance?.Register(this);

        if (GameManager.Instance != null)
            GameManager.Instance.Connect(GameManager.SignalName.WorldTick,
                Callable.From<double>(OnWorldTick));
    }

    // ── Interaction ────────────────────────────────────────────────────────

    /// <summary>Fell a tree — yields all products at once, leaves stump.</summary>
    public List<(ResourceType, float, string)> Fell()
    {
        if (!IsFellable || State == GrowthState.Stump) return new();
        State = GrowthState.Stump;
        _regrowTimer = 0;
        UpdateVisuals();
        GD.Print($"[Nature] Tree felled: {ObjType}");
        return GetYields();
    }

    /// <summary>Harvest non-tree (bush/mushroom) — partial yield, regrows.</summary>
    public List<(ResourceType, float, string)> Harvest()
    {
        if (IsTree) return new();
        if (State == GrowthState.Harvested) return new();
        State = GrowthState.Harvested;
        _regrowTimer = 0;
        UpdateVisuals();
        return GetYields();
    }

    private List<(ResourceType, float, string)> GetYields()
    {
        if (!Yields.TryGetValue(ObjType, out var list)) return new();
        var result = new List<(ResourceType, float, string)>();
        float maturity = Mathf.Clamp(GrowthAge, 0.3f, 1f);
        foreach (var (res, amt, lbl) in list)
        {
            if (lbl == null) continue;
            result.Add((res, amt * maturity, lbl));
        }
        return result;
    }

    // ── World tick ────────────────────────────────────────────────────────
    private void OnWorldTick(double delta)
    {
        switch (State)
        {
            case GrowthState.Sprout:
            case GrowthState.Growing:
                GrowthAge = Mathf.Min(GrowthAge + 0.02f, 1f);
                if (GrowthAge >= 1f) { State = GrowthState.Mature; UpdateVisuals(); }
                break;

            case GrowthState.Harvested:
                _regrowTimer += (float)delta;
                if (_regrowTimer >= RegrowTime * 0.3f)
                { State = GrowthState.Growing; GrowthAge = 0.3f; UpdateVisuals(); }
                break;

            case GrowthState.Stump:
                _regrowTimer += (float)delta;
                if (_regrowTimer >= RegrowTime)
                { State = GrowthState.Sprout; GrowthAge = 0.05f; UpdateVisuals(); }
                break;
        }
    }

    // ── Visuals ───────────────────────────────────────────────────────────
    private void BuildVisuals()
    {
        _mesh = new MeshInstance3D();
        var mat = new StandardMaterial3D();
        mat.AlbedoColor = GetColor();

        Mesh mesh = ObjType switch {
            NatureObjectType.TreeOak or NatureObjectType.TreeBirch => MakeCylinder(0.2f, GetHeight()),
            NatureObjectType.TreePine   => MakeCylinder(0.15f, GetHeight()),
            NatureObjectType.BushBerry  => MakeSphere(0.45f),
            NatureObjectType.BushPoison => MakeSphere(0.4f),
            NatureObjectType.MushroomEdible or NatureObjectType.MushroomPoison => MakeSphere(0.2f),
            NatureObjectType.RockSmall  => MakeSphere(0.35f),
            NatureObjectType.RockLarge  => MakeSphere(0.6f),
            NatureObjectType.WaterSource => MakeCylinder(0.8f, 0.1f),
            _ => MakeSphere(0.3f)
        };
        mesh.SurfaceSetMaterial(0, mat);
        _mesh.Mesh = mesh;
        _mesh.Position = new Vector3(0, GetHeight() * 0.5f, 0);
        AddChild(_mesh);

        // Treetop sphere for trees
        if (IsTree && State != GrowthState.Stump)
        {
            var crown = new MeshInstance3D();
            var cMat = new StandardMaterial3D();
            cMat.AlbedoColor = ObjType == NatureObjectType.TreePine
                ? new Color(0.15f, 0.45f, 0.2f) : new Color(0.2f, 0.6f, 0.15f);
            var cs = new SphereMesh(); cs.Radius = GetCrownRadius(); cs.Height = GetCrownRadius()*2f;
            cs.SurfaceSetMaterial(0, cMat);
            crown.Mesh = cs;
            crown.Position = new Vector3(0, GetHeight(), 0);
            AddChild(crown);
        }
    }

    private void UpdateVisuals()
    {
        // Rebuild visuals on state change
        foreach (Node c in GetChildren()) c.QueueFree();
        BuildVisuals();
        if (_regEntry != null)
        {
            WorldObjectRegistry.Instance?.Unregister(_regEntry);
            RegisterSelf();
        }
    }

    private void RegisterSelf()
    {
        string icon = ObjType switch {
            NatureObjectType.TreeOak    => "🌳",
            NatureObjectType.TreePine   => "🌲",
            NatureObjectType.TreeBirch  => "🌿",
            NatureObjectType.BushBerry  => "🫐",
            NatureObjectType.BushPoison => "⚠🫐",
            NatureObjectType.MushroomEdible => "🍄",
            NatureObjectType.MushroomPoison => "☠🍄",
            NatureObjectType.RockSmall  => "🪨",
            NatureObjectType.RockLarge  => "⛰",
            NatureObjectType.WaterSource => "💧",
            _ => "🌿"
        };
        string label = GetDisplayName();
        _regEntry = new WorldObjectEntry(this, WorldObjectKind.Resource, label, icon);
        WorldObjectRegistry.Instance?.Register(_regEntry);
    }

    public string GetDisplayName() => ObjType switch {
        NatureObjectType.TreeOak    => State == GrowthState.Stump ? "Eichenstumpf" : "Eiche",
        NatureObjectType.TreePine   => State == GrowthState.Stump ? "Kiefernstumpf" : "Kiefer",
        NatureObjectType.TreeBirch  => State == GrowthState.Stump ? "Birkenstumpf" : "Birke",
        NatureObjectType.BushBerry  => "Beerenstrauch (essbar)",
        NatureObjectType.BushPoison => "Beerenstrauch (giftig) ⚠",
        NatureObjectType.MushroomEdible => "Speisepilz",
        NatureObjectType.MushroomPoison => "Giftpilz ⚠",
        NatureObjectType.RockSmall  => "Stein",
        NatureObjectType.RockLarge  => "Felsblock",
        NatureObjectType.WaterSource => "Wasserquelle",
        _ => ObjType.ToString()
    };

    private Color GetColor() => ObjType switch {
        NatureObjectType.TreeOak    => new Color(0.45f, 0.28f, 0.1f),
        NatureObjectType.TreePine   => new Color(0.35f, 0.22f, 0.08f),
        NatureObjectType.TreeBirch  => new Color(0.85f, 0.82f, 0.75f),
        NatureObjectType.BushBerry  => new Color(0.2f,  0.55f, 0.15f),
        NatureObjectType.BushPoison => new Color(0.25f, 0.4f,  0.1f),
        NatureObjectType.MushroomEdible => new Color(0.8f,  0.5f,  0.2f),
        NatureObjectType.MushroomPoison => new Color(0.6f,  0.1f,  0.6f),
        NatureObjectType.RockSmall  => new Color(0.5f,  0.5f,  0.5f),
        NatureObjectType.RockLarge  => new Color(0.45f, 0.45f, 0.48f),
        NatureObjectType.WaterSource => new Color(0.2f,  0.5f,  0.9f),
        _ => new Color(0.5f, 0.5f, 0.5f)
    };

    private float GetHeight() => ObjType switch {
        NatureObjectType.TreeOak   => 3f + GrowthAge * 2f,
        NatureObjectType.TreePine  => 4f + GrowthAge * 3f,
        NatureObjectType.TreeBirch => 2.5f + GrowthAge * 1.5f,
        NatureObjectType.BushBerry or NatureObjectType.BushPoison => 0.6f,
        NatureObjectType.MushroomEdible or NatureObjectType.MushroomPoison => 0.25f,
        NatureObjectType.RockSmall => 0.4f,
        NatureObjectType.RockLarge => 0.8f,
        NatureObjectType.WaterSource => 0.05f,
        NatureObjectType.Stump      => 0.3f,
        _ => 0.3f
    };

    private float GetCrownRadius() => ObjType switch {
        NatureObjectType.TreePine  => 0.8f + GrowthAge * 0.5f,
        _ => 1.2f + GrowthAge * 0.8f
    };

    private static CylinderMesh MakeCylinder(float r, float h)
    {
        var m = new CylinderMesh();
        m.TopRadius = r * 0.7f; m.BottomRadius = r; m.Height = h; return m;
    }
    private static SphereMesh MakeSphere(float r)
    {
        var m = new SphereMesh(); m.Radius = r; m.Height = r*2f; return m;
    }

    /// <summary>Quick check if any NPC nearby has tools to fell this.</summary>
    public bool Knowledge_Required_Check()
    {
        if (!IsFellable) return true;
        if (GameManager.Instance == null) return false;
        foreach (var npc in GameManager.Instance.AllNpcs)
            if (npc.Knowledge.Knows("tools") || npc.Knowledge.Knows("axe"))
                return true;
        return false;
    }

    public override void _ExitTree()
    {
        NatureManager.Instance?.Unregister(this);
        if (_regEntry != null) WorldObjectRegistry.Instance?.Unregister(_regEntry);
    }
}
