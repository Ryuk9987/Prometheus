#nullable disable
using Godot;
using System.Collections.Generic;

public enum TabletMode { Draw, Blueprint }

/// <summary>
/// The physical Oracle Tablet in the world.
/// In Draw mode: NPCs interpret the drawing based on their own context.
/// In Blueprint mode: predefined idea is delivered directly.
/// </summary>
public partial class OracleTablet : Node3D
{
    [Export] public float NpcReadRadius { get; set; } = 6f;
    [Export] public float GlowIntensity { get; set; } = 2.0f;

    public TabletMode Mode              { get; private set; } = TabletMode.Blueprint;
    public string     ActiveBlueprintId { get; private set; } = "";
    public bool       HasContent        { get; private set; } = false;

    // Current drawing for NPC interpretation
    private List<DrawnStroke> _currentDrawing;

    private SubViewport    _viewport;
    private MeshInstance3D _screen;
    private OmniLight3D    _glow;
    private TabletCanvas   _canvas;

    private double _readTimer = 0;
    private const double ReadInterval = 4.0;

    [Signal] public delegate void KnowledgeTransferredEventHandler(string npcName, string ideaId, float depth);
    [Signal] public delegate void InterpretationEventHandler(string npcName, string ideaLabel, string reasoning);

    public static OracleTablet Instance { get; private set; }

    public override void _Ready()
    {
        Instance  = this;
        _viewport = GetNode<SubViewport>("SubViewport");
        _screen   = GetNode<MeshInstance3D>("Screen");
        _glow     = GetNode<OmniLight3D>("Glow");
        _canvas   = _viewport.GetNode<TabletCanvas>("TabletCanvas");

        var mat = new StandardMaterial3D();
        mat.AlbedoTexture            = _viewport.GetTexture();
        mat.EmissionEnabled          = true;
        mat.EmissionTexture          = _viewport.GetTexture();
        mat.EmissionEnergyMultiplier = 0.8f;
        _screen.SetSurfaceOverrideMaterial(0, mat);
    }

    public override void _Process(double delta)
    {
        float pulse = 1f + 0.3f * Mathf.Sin((float)Time.GetTicksMsec() * 0.002f);
        _glow.LightEnergy = GlowIntensity * pulse;

        _readTimer += delta;
        if (_readTimer >= ReadInterval && HasContent)
        {
            _readTimer = 0;
            TransferToNearbyNpcs();
        }
    }

    // ── Called from BlueprintEditor ──────────────────────────────────────────

    public void SetBlueprint(string ideaId)
    {
        ActiveBlueprintId = ideaId;
        Mode       = TabletMode.Blueprint;
        HasContent = true;
        _currentDrawing = null;
        _canvas.ShowBlueprint(ideaId);
        GD.Print($"[OracleTablet] Blueprint: {ideaId}");
    }

    public void SetDrawing(List<DrawnStroke> strokes)
    {
        _currentDrawing   = strokes;
        Mode              = TabletMode.Draw;
        HasContent        = strokes != null && strokes.Count > 0;
        ActiveBlueprintId = "";
        GD.Print($"[OracleTablet] Drawing submitted: {strokes?.Count ?? 0} strokes.");
    }

    public void SetDrawMode()
    {
        Mode = TabletMode.Draw;
        _canvas.SetDrawMode();
    }

    public void ClearTablet()
    {
        HasContent        = false;
        ActiveBlueprintId = "";
        _currentDrawing   = null;
        _canvas.Clear();
    }

    // ── Knowledge transfer ───────────────────────────────────────────────────

    private void TransferToNearbyNpcs()
    {
        if (GameManager.Instance == null) return;

        foreach (var npc in GameManager.Instance.AllNpcs)
        {
            if (GlobalPosition.DistanceTo(npc.GlobalPosition) > NpcReadRadius) continue;
            if (!npc.Belief.CanHearOracle) continue;

            if (Mode == TabletMode.Blueprint)
                TransferBlueprint(npc);
            else if (Mode == TabletMode.Draw && _currentDrawing != null)
                TransferDrawing(npc);
        }
    }

    private void TransferBlueprint(NpcEntity npc)
    {
        if (string.IsNullOrEmpty(ActiveBlueprintId)) return;
        float understanding = npc.Belief.Belief * npc.Personality.Curiosity;
        var rng = new RandomNumberGenerator(); rng.Randomize();
        float depth = Mathf.Clamp(understanding * rng.RandfRange(0.3f, 0.7f), 0f, 1f);
        if (depth < 0.05f) return;
        npc.Knowledge.Learn(ActiveBlueprintId, depth, npc.Belief.Belief * 0.7f, "oracle_tablet");
        npc.Belief.Reinforce(0.05f);
        EmitSignal(SignalName.KnowledgeTransferred, npc.NpcName, ActiveBlueprintId, depth);
    }

    private void TransferDrawing(NpcEntity npc)
    {
        var features = DrawingAnalyzer.Analyze(_currentDrawing);
        var result   = InterpretationEngine.Interpret(features, npc);

        if (result.IdeaId == "unknown") return;

        npc.Knowledge.Learn(result.IdeaId, result.Depth, result.Confidence, "oracle_drawing");
        npc.Belief.Reinforce(0.08f);

        GD.Print($"[OracleTablet] {npc.NpcName} interpreted drawing as '{result.IdeaLabel}' " +
                 $"depth:{result.Depth:F2} — {result.Reasoning}");

        EmitSignal(SignalName.KnowledgeTransferred, npc.NpcName, result.IdeaId, result.Depth);
        EmitSignal(SignalName.Interpretation, npc.NpcName, result.IdeaLabel, result.Reasoning);
    }
}
