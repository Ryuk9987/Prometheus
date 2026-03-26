#nullable disable
using Godot;
using System.Collections.Generic;

public enum BuildOrderStatus { Pending, InProgress, Done }

/// <summary>
/// A build order placed by the player in the world.
/// Shows a ghost outline. NPCs with relevant knowledge come and build it.
/// </summary>
public partial class BuildOrder : Node3D
{
    public string           KnowledgeId  { get; set; }
    public BuildOrderStatus Status       { get; set; } = BuildOrderStatus.Pending;
    public float            Progress     { get; set; } = 0f;
    public float            Required     { get; set; } = 5f;
    public string           TribeId      { get; set; } = "";
    public bool             IsAutonomous { get; set; } = false;

    private MeshInstance3D  _ghost;
    private Label3D         _label;
    private OmniLight3D     _light;

    public static BuildOrderManager Manager => BuildOrderManager.Instance;

    private WorldObjectEntry _registryEntry;

    public override void _Ready()
    {
        BuildVisuals();
        BuildOrderManager.Instance?.Register(this);
        var def = KnowledgeCatalog.Get(KnowledgeId);
        _registryEntry = new WorldObjectEntry(this, WorldObjectKind.BuildOrder,
            $"Bauauftrag: {def?.DisplayName ?? KnowledgeId}", def?.Icon ?? "🏗");
        WorldObjectRegistry.Instance?.Register(_registryEntry);
    }

    public void Work(float amount)
    {
        Status   = BuildOrderStatus.InProgress;
        Progress = Mathf.Min(Progress + amount, Required);
        UpdateVisuals();

        if (Progress >= Required)
        {
            Status = BuildOrderStatus.Done;
            Complete();
        }
    }

    private void Complete()
    {
        GD.Print($"[BuildOrder] {KnowledgeId} completed at {GlobalPosition}!");
        BuildOrderManager.Instance?.Unregister(this);

        // Spawn actual building visual (placeholder — artist pass later)
        var def = KnowledgeCatalog.Get(KnowledgeId);
        // Notify SettlementManager to spawn the real CompletedBuilding
        SettlementManager.Instance?.OnBuildOrderCompleted(this);
        if (_registryEntry != null) WorldObjectRegistry.Instance?.Unregister(_registryEntry);
        QueueFree();
    }

    private void BuildVisuals()
    {
        var def = KnowledgeCatalog.Get(KnowledgeId);
        string icon = def?.Icon ?? "?";
        string name = def?.DisplayName ?? KnowledgeId;

        // Ghost mesh
        _ghost = new MeshInstance3D();
        var mat = new StandardMaterial3D();
        mat.AlbedoColor    = new Color(0.4f, 0.7f, 1f, 0.3f);
        mat.Transparency   = BaseMaterial3D.TransparencyEnum.Alpha;
        mat.CullMode       = BaseMaterial3D.CullModeEnum.Disabled;
        var box = new BoxMesh(); box.Size = new Vector3(2f, 2f, 2f);
        box.SurfaceSetMaterial(0, mat);
        _ghost.Mesh = box;
        _ghost.Position = new Vector3(0, 1f, 0);
        AddChild(_ghost);

        // Label
        _label = new Label3D();
        _label.Text      = $"{icon} {name}\n[Warte auf Bauarbeiter]";
        _label.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
        _label.FontSize  = 28;
        _label.Position  = new Vector3(0, 3f, 0);
        AddChild(_label);

        // Marker light
        _light = new OmniLight3D();
        _light.LightColor  = new Color(0.4f, 0.7f, 1f);
        _light.LightEnergy = 1.5f;
        _light.OmniRange   = 6f;
        _light.Position    = new Vector3(0, 2f, 0);
        AddChild(_light);
    }

    private void UpdateVisuals()
    {
        float t = Progress / Required;
        if (_label != null)
        {
            var def = KnowledgeCatalog.Get(KnowledgeId);
            _label.Text = $"{def?.Icon} {def?.DisplayName}\n[{(int)(t*100)}%]";
        }
        if (_ghost != null)
        {
            var mat = _ghost.GetActiveMaterial(0) as StandardMaterial3D;
            if (mat != null) mat.AlbedoColor = new Color(0.4f + t*0.4f, 0.7f, 1f - t*0.5f, 0.3f + t*0.4f);
        }
        if (_light != null)
            _light.LightColor = new Color(0.4f + t*0.6f, 0.7f, 1f - t*0.7f);
    }

    private static BoxMesh MakeBox(Vector3 size, StandardMaterial3D mat)
    {
        var m = new BoxMesh(); m.Size = size;
        m.SurfaceSetMaterial(0, mat);
        return m;
    }

    public override void _ExitTree()
    {
        BuildOrderManager.Instance?.Unregister(this);
        if (_registryEntry != null) WorldObjectRegistry.Instance?.Unregister(_registryEntry);
    }
}
