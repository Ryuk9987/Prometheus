#nullable disable
using Godot;

/// <summary>
/// A campfire built by NPCs.
/// Stages: pile (branches collected) → lit (fire started) → burning (active).
/// Provides warmth and cooking capability. Needs fuel to keep burning.
/// </summary>
public partial class Campfire : Node3D
{
    public enum Stage { Pile, Lit, Burning, Extinguished }

    public Stage   CurrentStage  { get; private set; } = Stage.Pile;
    public float   Fuel          { get; private set; } = 0f;
    public float   MaxFuel       { get; set; } = 10f;
    public bool    IsBurning     => CurrentStage == Stage.Burning || CurrentStage == Stage.Lit;
    /// <summary>Set before AddChild — determines if stone ring is built around fire.</summary>
    public bool    WithStoneRing { get; set; } = false;

    private MeshInstance3D _branchMesh;
    private OmniLight3D    _fireLight;
    private double         _burnTimer = 0;
    private const double   BurnInterval = 2.0; // fuel consumed per tick

    public static CampfireManager Manager => CampfireManager.Instance;

    public override void _Ready()
    {
        BuildVisuals();
        CampfireManager.Instance?.Register(this);
        GD.Print($"[Campfire] Built at {GlobalPosition}");
    }

    public void AddFuel(float amount)
    {
        Fuel = Mathf.Min(Fuel + amount, MaxFuel);
        if (CurrentStage == Stage.Pile && Fuel >= 3f)
            CurrentStage = Stage.Lit;
        UpdateVisuals();
    }

    public void Light()
    {
        if (Fuel < 1f) return;
        CurrentStage = Stage.Burning;
        _fireLight.Visible = true;
        UpdateVisuals();
        GD.Print("[Campfire] 🔥 Lit!");
    }

    public override void _Process(double delta)
    {
        if (!IsBurning) return;
        _burnTimer += delta;
        if (_burnTimer >= BurnInterval)
        {
            _burnTimer = 0;
            Fuel -= 0.5f;
            if (Fuel <= 0f)
            {
                CurrentStage = Stage.Extinguished;
                _fireLight.Visible = false;
                UpdateVisuals();
                GD.Print("[Campfire] Fire extinguished.");
            }
            else
            {
                // Flicker
                _fireLight.LightEnergy = 1.5f + GD.Randf() * 1.0f;
            }
        }
    }

    private void BuildVisuals()
    {
        // Branch pile
        _branchMesh = new MeshInstance3D();
        var mat = new StandardMaterial3D();
        mat.AlbedoColor = new Color(0.45f, 0.28f, 0.1f);
        var cyl = new CylinderMesh();
        cyl.TopRadius = 0.4f; cyl.BottomRadius = 0.5f; cyl.Height = 0.25f;
        cyl.SurfaceSetMaterial(0, mat);
        _branchMesh.Mesh = cyl;
        _branchMesh.Position = new Vector3(0, 0.12f, 0);
        AddChild(_branchMesh);

        // Stone ring — only if player included stones in blueprint
        if (WithStoneRing)
        {
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.Pi / 3f;
                var stone = new MeshInstance3D();
                var sm = new StandardMaterial3D();
                sm.AlbedoColor = new Color(0.5f, 0.5f, 0.55f);
                var sp = new SphereMesh(); sp.Radius = 0.12f; sp.Height = 0.22f;
                sp.SurfaceSetMaterial(0, sm);
                stone.Mesh = sp;
                stone.Position = new Vector3(Mathf.Cos(a)*0.52f, 0.06f, Mathf.Sin(a)*0.52f);
                AddChild(stone);
            }
        }

        // Fire light (off until lit)
        _fireLight = new OmniLight3D();
        _fireLight.LightColor  = new Color(1f, 0.5f, 0.1f);
        _fireLight.LightEnergy = 2f;
        _fireLight.OmniRange   = 10f;
        _fireLight.Position    = new Vector3(0, 0.5f, 0);
        _fireLight.Visible     = false;
        AddChild(_fireLight);
    }

    private void UpdateVisuals()
    {
        if (_branchMesh == null) return;
        var mat = _branchMesh.GetActiveMaterial(0) as StandardMaterial3D;
        if (mat == null) return;
        mat.AlbedoColor = CurrentStage switch
        {
            Stage.Pile          => new Color(0.45f, 0.28f, 0.1f),
            Stage.Lit           => new Color(0.6f,  0.35f, 0.1f),
            Stage.Burning       => new Color(0.8f,  0.4f,  0.05f),
            Stage.Extinguished  => new Color(0.25f, 0.25f, 0.25f),
            _                   => mat.AlbedoColor
        };
    }

    public override void _ExitTree()
    {
        CampfireManager.Instance?.Unregister(this);
    }
}
