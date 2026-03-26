#nullable disable
using Godot;
using System.Collections.Generic;

public enum TabletMode { Draw, Blueprint }

/// <summary>
/// The physical Oracle Tablet in the world.
/// NPCs with sufficient belief walk here to receive knowledge.
/// The player draws or selects blueprints on the tablet surface.
/// </summary>
public partial class OracleTablet : Node3D
{
    [Export] public float NpcReadRadius   { get; set; } = 6f;
    [Export] public float GlowIntensity   { get; set; } = 2.0f;

    public TabletMode   Mode              { get; private set; } = TabletMode.Blueprint;
    public string       ActiveBlueprintId { get; private set; } = "";
    public bool         HasContent        { get; private set; } = false;

    private SubViewport      _viewport;
    private MeshInstance3D   _screen;
    private OmniLight3D      _glow;
    private TabletCanvas     _canvas;

    // NPCs currently reading the tablet
    private readonly HashSet<NpcEntity> _readers = new();
    private double _readTimer = 0;
    private const double ReadInterval = 3.0;

    [Signal] public delegate void KnowledgeTransferredEventHandler(string npcName, string ideaId, float depth);

    public static OracleTablet Instance { get; private set; }

    public override void _Ready()
    {
        Instance = this;
        _viewport = GetNode<SubViewport>("SubViewport");
        _screen   = GetNode<MeshInstance3D>("Screen");
        _glow     = GetNode<OmniLight3D>("Glow");
        _canvas   = _viewport.GetNode<TabletCanvas>("TabletCanvas");

        // Apply viewport texture to screen mesh
        var mat = new StandardMaterial3D();
        mat.AlbedoTexture    = _viewport.GetTexture();
        mat.EmissionEnabled  = true;
        mat.EmissionTexture  = _viewport.GetTexture();
        mat.EmissionEnergyMultiplier = 0.8f;
        _screen.SetSurfaceOverrideMaterial(0, mat);

        GD.Print("[OracleTablet] Ready. Press F to interact.");
    }

    public override void _Process(double delta)
    {
        // Pulse glow
        float pulse = 1f + 0.3f * Mathf.Sin((float)Time.GetTicksMsec() * 0.002f);
        _glow.LightEnergy = GlowIntensity * pulse;

        // Periodically transfer knowledge to nearby believers
        _readTimer += delta;
        if (_readTimer >= ReadInterval && HasContent)
        {
            _readTimer = 0;
            TransferToNearbyNpcs();
        }
    }

    // Called by TabletUI when a blueprint is selected
    public void SetBlueprint(string ideaId)
    {
        ActiveBlueprintId = ideaId;
        Mode = TabletMode.Blueprint;
        HasContent = true;
        _canvas.ShowBlueprint(ideaId);
        GD.Print($"[OracleTablet] Blueprint set: {ideaId}");
    }

    // Called by TabletUI when player switches to draw mode
    public void SetDrawMode()
    {
        Mode = TabletMode.Draw;
        HasContent = true;
        _canvas.SetDrawMode();
    }

    public void ClearTablet()
    {
        HasContent = false;
        ActiveBlueprintId = "";
        _canvas.Clear();
    }

    private void TransferToNearbyNpcs()
    {
        if (GameManager.Instance == null) return;

        foreach (var npc in GameManager.Instance.AllNpcs)
        {
            float dist = GlobalPosition.DistanceTo(npc.GlobalPosition);
            if (dist > NpcReadRadius) continue;
            if (!npc.Belief.CanHearOracle) continue;

            AttemptTransfer(npc);
        }
    }

    private void AttemptTransfer(NpcEntity npc)
    {
        string ideaId = Mode == TabletMode.Blueprint ? ActiveBlueprintId : "unknown_drawing";
        if (string.IsNullOrEmpty(ideaId)) return;

        // Understanding = belief × curiosity × (1 + existing knowledge bonus)
        float existingBonus = npc.Knowledge.Knows(ideaId) ? 0.3f : 0f;
        float understanding = npc.Belief.Belief
                            * npc.Personality.Curiosity
                            * (1f + existingBonus);

        var rng = new RandomNumberGenerator();
        rng.Randomize();
        float depth      = Mathf.Clamp(understanding * rng.RandfRange(0.3f, 0.7f), 0f, 1f);
        float confidence = npc.Belief.Belief * 0.7f;

        if (depth < 0.05f) return; // too little understanding

        npc.Knowledge.Learn(ideaId, depth, confidence, "oracle_tablet");
        npc.Belief.Reinforce(0.05f);

        GD.Print($"[OracleTablet] {npc.NpcName} learned '{ideaId}' depth:{depth:F2}");
        EmitSignal(SignalName.KnowledgeTransferred, npc.NpcName, ideaId, depth);
    }
}
