#nullable disable
using Godot;

public enum BuildingType
{
    Campfire, Shelter, Hut, Storehouse, Wall, WoodenHut, Workshop, Farm, Well, Road
}

/// <summary>
/// A completed, standing building in the world.
/// Spawned when a BuildOrder reaches 100% progress.
/// Has visual mesh, interaction radius, and functional effects on nearby NPCs.
/// </summary>
public partial class CompletedBuilding : Node3D
{
    public BuildingType Type      { get; set; }
    public string       TribeId   { get; set; }
    public float        Condition { get; set; } = 1f; // 0=ruin, 1=perfect

    private MeshInstance3D _mesh;
    private OmniLight3D    _light;
    private WorldObjectEntry _regEntry;

    public override void _Ready()
    {
        BuildVisuals();
        Register();
        SettlementManager.Instance?.RegisterBuilding(this);
        GD.Print($"[Building] {Type} completed at {GlobalPosition}");
    }

    public override void _ExitTree()
    {
        if (_regEntry != null) WorldObjectRegistry.Instance?.Unregister(_regEntry);
        SettlementManager.Instance?.UnregisterBuilding(this);
    }

    // ── Functional effects ────────────────────────────────────────────────
    /// <summary>How many NPCs can shelter here (reduces hunger/cold decay).</summary>
    public int ShelterCapacity => Type switch {
        BuildingType.Shelter    => 3,
        BuildingType.Hut        => 6,
        BuildingType.WoodenHut  => 5,
        BuildingType.Storehouse => 0,
        BuildingType.Workshop   => 0,
        _                       => 0
    };

    /// <summary>Food storage bonus this building provides.</summary>
    public float StorageBonus => Type switch {
        BuildingType.Storehouse => 50f,
        BuildingType.Hut        => 10f,
        _                       => 0f
    };

    /// <summary>Radius within which NPCs benefit.</summary>
    public float InfluenceRadius => Type switch {
        BuildingType.Campfire   => 6f,
        BuildingType.Hut        => 8f,
        BuildingType.WoodenHut  => 8f,
        BuildingType.Shelter    => 5f,
        BuildingType.Storehouse => 12f,
        BuildingType.Workshop   => 10f,
        BuildingType.Farm       => 15f,
        BuildingType.Well       => 10f,
        BuildingType.Wall       => 3f,
        _                       => 5f
    };

    // ── Visuals ───────────────────────────────────────────────────────────
    private void BuildVisuals()
    {
        _mesh = new MeshInstance3D();
        var mat = new StandardMaterial3D();
        mat.AlbedoColor = GetColor();

        _mesh.Mesh = Type switch {
            BuildingType.Campfire   => MakeCylinder(0.4f, 0.6f),
            BuildingType.Shelter    => MakeBox(2f, 1.5f, 2f),
            BuildingType.Hut        => MakeBox(3f, 2.5f, 3f),
            BuildingType.WoodenHut  => MakeBox(3.5f, 3f, 3.5f),
            BuildingType.Storehouse => MakeBox(4f, 2.5f, 4f),
            BuildingType.Workshop   => MakeBox(3f, 2.5f, 4f),
            BuildingType.Farm       => MakeBox(6f, 0.1f, 6f),
            BuildingType.Well       => MakeCylinder(0.6f, 1.2f),
            BuildingType.Wall       => MakeBox(1f, 2f, 4f),
            BuildingType.Road       => MakeBox(2f, 0.05f, 4f),
            _ => MakeBox(2f, 2f, 2f)
        };
        _mesh.Mesh.SurfaceSetMaterial(0, mat);
        _mesh.Position = new Vector3(0, GetHeight() * 0.5f, 0);
        AddChild(_mesh);

        // Roof for huts
        if (Type is BuildingType.Hut or BuildingType.WoodenHut or BuildingType.Shelter)
        {
            var roof = new MeshInstance3D();
            var roofMat = new StandardMaterial3D();
            roofMat.AlbedoColor = new Color(0.45f, 0.25f, 0.1f);
            var prism = new PrismMesh();
            float w = Type == BuildingType.Hut ? 3.4f : (Type == BuildingType.WoodenHut ? 4f : 2.4f);
            prism.Size = new Vector3(w, 1.5f, w);
            prism.SurfaceSetMaterial(0, roofMat);
            roof.Mesh = prism;
            roof.Position = new Vector3(0, GetHeight() + 0.5f, 0);
            AddChild(roof);
        }

        // Fire glow for campfire
        if (Type == BuildingType.Campfire)
        {
            _light = new OmniLight3D();
            _light.LightColor  = new Color(1f, 0.55f, 0.1f);
            _light.OmniRange   = 8f;
            _light.LightEnergy = 1.5f;
            _light.Position    = new Vector3(0, 1f, 0);
            AddChild(_light);
        }

        // Door marker for huts
        if (Type is BuildingType.Hut or BuildingType.WoodenHut)
        {
            var door = new MeshInstance3D();
            var dMat = new StandardMaterial3D();
            dMat.AlbedoColor = new Color(0.3f, 0.18f, 0.08f);
            var db = new BoxMesh(); db.Size = new Vector3(0.8f, 1.6f, 0.1f);
            db.SurfaceSetMaterial(0, dMat);
            door.Mesh = db;
            float half = Type == BuildingType.Hut ? 1.5f : 1.75f;
            door.Position = new Vector3(0, 0.8f, half);
            AddChild(door);
        }

        // Name label
        var lbl = new Label3D();
        lbl.Text     = GetDisplayName();
        lbl.FontSize = 24;
        lbl.Modulate = new Color(1f, 1f, 1f, 0.7f);
        lbl.Position = new Vector3(0, GetHeight() + 2f, 0);
        lbl.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
        AddChild(lbl);
    }

    private void Register()
    {
        string icon = Type switch {
            BuildingType.Campfire   => "🔥",
            BuildingType.Shelter    => "🏚",
            BuildingType.Hut        => "🏠",
            BuildingType.WoodenHut  => "🪵",
            BuildingType.Storehouse => "🏛",
            BuildingType.Workshop   => "🔧",
            BuildingType.Farm       => "🌾",
            BuildingType.Well       => "💧",
            BuildingType.Wall       => "🧱",
            BuildingType.Road       => "🛤",
            _ => "🏗"
        };
        _regEntry = new WorldObjectEntry(this, WorldObjectKind.Structure, GetDisplayName(), icon);
        WorldObjectRegistry.Instance?.Register(_regEntry);
    }

    public string GetDisplayName() => Type switch {
        BuildingType.Campfire   => "Lagerfeuer",
        BuildingType.Shelter    => "Unterkunft",
        BuildingType.Hut        => "Hütte",
        BuildingType.WoodenHut  => "Holzhütte",
        BuildingType.Storehouse => "Vorratslager",
        BuildingType.Workshop   => "Werkstatt",
        BuildingType.Farm       => "Ackerfeld",
        BuildingType.Well       => "Brunnen",
        BuildingType.Wall       => "Mauer",
        BuildingType.Road       => "Weg",
        _ => Type.ToString()
    };

    private Color GetColor() => Type switch {
        BuildingType.Campfire   => new Color(0.8f, 0.3f, 0.1f),
        BuildingType.Shelter    => new Color(0.55f, 0.4f, 0.25f),
        BuildingType.Hut        => new Color(0.6f, 0.45f, 0.28f),
        BuildingType.WoodenHut  => new Color(0.5f, 0.35f, 0.2f),
        BuildingType.Storehouse => new Color(0.5f, 0.5f, 0.55f),
        BuildingType.Workshop   => new Color(0.4f, 0.4f, 0.45f),
        BuildingType.Farm       => new Color(0.4f, 0.55f, 0.2f),
        BuildingType.Well       => new Color(0.45f, 0.45f, 0.5f),
        BuildingType.Wall       => new Color(0.5f, 0.5f, 0.5f),
        BuildingType.Road       => new Color(0.55f, 0.5f, 0.4f),
        _ => new Color(0.5f, 0.5f, 0.5f)
    };

    private float GetHeight() => Type switch {
        BuildingType.Campfire   => 0.6f,
        BuildingType.Hut        => 2.5f,
        BuildingType.WoodenHut  => 3f,
        BuildingType.Shelter    => 1.5f,
        BuildingType.Storehouse => 2.5f,
        BuildingType.Workshop   => 2.5f,
        BuildingType.Farm       => 0.1f,
        BuildingType.Well       => 1.2f,
        BuildingType.Wall       => 2f,
        BuildingType.Road       => 0.05f,
        _ => 2f
    };

    private static BoxMesh MakeBox(float x, float y, float z)
    {
        var m = new BoxMesh(); m.Size = new Vector3(x, y, z); return m;
    }
    private static CylinderMesh MakeCylinder(float r, float h)
    {
        var m = new CylinderMesh(); m.TopRadius = r; m.BottomRadius = r; m.Height = h; return m;
    }
}
