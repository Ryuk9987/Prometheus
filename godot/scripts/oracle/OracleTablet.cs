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

    private double _readTimer   = 0;
    private const double ReadInterval = 4.0;

    // Flash effect state
    private bool   _flashing       = false;
    private double _flashTimer     = 0;
    private const double FlashDuration = 3.0;  // seconds of intense glow
    private const float  FlashPeak     = 12f;  // peak light energy
    private const float  AttractRadius = 25f;  // NPCs within this range get attracted

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
        if (_flashing)
        {
            _flashTimer += delta;
            float t = (float)(_flashTimer / FlashDuration);

            // Sharp spike at start, then fade to normal glow
            float flashCurve = Mathf.Exp(-t * 3f);                   // exponential decay
            float pulse      = 1f + 0.3f * Mathf.Sin((float)Time.GetTicksMsec() * 0.008f);
            _glow.LightEnergy = Mathf.Lerp(GlowIntensity * pulse, FlashPeak, flashCurve);

            // Color shifts white-hot → back to blue
            _glow.LightColor = _glow.LightColor.Lerp(new Color(0.4f, 0.7f, 1f), t * 0.05f);

            if (_flashTimer >= FlashDuration)
            {
                _flashing         = false;
                _glow.LightColor  = new Color(0.4f, 0.7f, 1f);
                _glow.LightEnergy = GlowIntensity;
                _glow.OmniRange   = 8f; // back to normal
            }
        }
        else
        {
            float pulse = 1f + 0.3f * Mathf.Sin((float)Time.GetTicksMsec() * 0.002f);
            _glow.LightEnergy = GlowIntensity * pulse;
        }

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
        TriggerFlash();
        GD.Print($"[OracleTablet] Blueprint: {ideaId}");
    }

    public void SetDrawing(List<DrawnStroke> strokes)
    {
        if (strokes == null || strokes.Count == 0) return;
        _currentDrawing   = strokes;
        Mode              = TabletMode.Draw;
        HasContent        = true;
        ActiveBlueprintId = "";
        TriggerFlash();
        GD.Print($"[OracleTablet] Drawing submitted: {strokes.Count} strokes.");
    }

    // Current composition (stamps + strokes)
    private List<PlacedStamp> _currentStamps;
    public  Composition       LastComposition { get; private set; }

    public void SetComposition(List<PlacedStamp> stamps, List<DrawnStroke> strokes)
    {
        bool hasContent = (stamps != null && stamps.Count > 0) ||
                          (strokes != null && strokes.Count > 0);
        if (!hasContent) return;

        _currentStamps  = stamps;
        _currentDrawing = strokes;
        Mode            = TabletMode.Draw;
        HasContent      = true;
        ActiveBlueprintId = "";
        TriggerFlash();

        var comp = CompositionAnalyzer.Analyze(stamps ?? new List<PlacedStamp>(), strokes);
        LastComposition = comp;
        GD.Print($"[OracleTablet] Composition: '{comp.Description}' → idea: {comp.PrimaryIdea} | stamps: {string.Join(",", comp.StampCounts.Keys)}");
        // Immediate transfer to NPCs already nearby
        CallDeferred(MethodName.TransferToNearbyNpcs);
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

    private static string MapToCatalogId(string ideaId, string label)
    {
        // Map composition descriptions to specific catalog IDs
        if (label.Contains("Steinkranz"))              return "campfire_stone";
        if (label.Contains("Lagerfeuer"))              return "campfire";
        if (label.Contains("Haus") || label.Contains("Unterkunft")) return "shelter";
        if (label.Contains("Hütte"))                   return "hut";
        if (label.Contains("Bogen"))                   return "bow";
        if (label.Contains("Speer"))                   return "hunting";
        if (label.Contains("Feld"))                    return "agriculture";
        if (label.Contains("Mauer"))                   return "wall";
        if (label.Contains("Schrift"))                 return "writing";
        if (label.Contains("Sternkarte"))              return "astronomy";
        if (label.Contains("Schmied"))                 return "metalwork";
        if (label.Contains("Heilmittel") || label.Contains("Heiltrank")) return "medicine";
        return ideaId; // fallback to base idea id
    }

    // ── Flash + Attract ──────────────────────────────────────────────────────

    private void TriggerFlash()
    {
        _flashing   = true;
        _flashTimer = 0;
        _glow.LightColor  = new Color(1f, 0.95f, 0.8f); // warm white flash
        _glow.OmniRange   = 20f;                          // light expands
        AttractNearbyNpcs();
        GD.Print("[OracleTablet] ✨ Flash triggered — NPCs attracted.");
    }

    private void AttractNearbyNpcs()
    {
        if (GameManager.Instance == null) return;
        int attracted = 0;
        foreach (var npc in GameManager.Instance.AllNpcs)
        {
            float dist = GlobalPosition.DistanceTo(npc.GlobalPosition);
            if (dist > AttractRadius) continue;

            // Force tablet seek — override cooldown
            npc.TabletSeek.ForceSeek();
            attracted++;
        }
        GD.Print($"[OracleTablet] {attracted} NPCs attracted to tablet.");
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
        // Analyze composition (stamps take priority over freehand)
        InterpretationResult result;

        if (_currentStamps != null && _currentStamps.Count > 0)
        {
            var comp = CompositionAnalyzer.Analyze(_currentStamps, _currentDrawing);
            if (comp.PrimaryIdea != "unknown")
            {
                // Composition match — understanding via belief + curiosity
                var rng = new RandomNumberGenerator(); rng.Randomize();
                float depth = Mathf.Clamp(
                    npc.Belief.Belief * npc.Personality.Curiosity * rng.RandfRange(0.4f, 0.8f), 0f, 1f);

                result = new InterpretationResult {
                    IdeaId    = comp.PrimaryIdea,
                    IdeaLabel = comp.Description,
                    Depth     = depth,
                    Confidence = npc.Belief.Belief * 0.8f,
                    Reasoning  = $"Ich sehe '{comp.Description}' — ich versuche das nachzubauen."
                };
            }
            else
            {
                var features = DrawingAnalyzer.Analyze(_currentDrawing ?? new List<DrawnStroke>());
                result = InterpretationEngine.Interpret(features, npc);
            }
        }
        else
        {
            var features = DrawingAnalyzer.Analyze(_currentDrawing ?? new List<DrawnStroke>());
            result = InterpretationEngine.Interpret(features, npc);
        }

        if (result.IdeaId == "unknown") return;

        // Map composition description to specific catalog entry
        string catalogId = MapToCatalogId(result.IdeaId, result.IdeaLabel);
        npc.Knowledge.Learn(catalogId, result.Depth, result.Confidence, "oracle_tablet");
        npc.Belief.Reinforce(0.08f);

        GD.Print($"[OracleTablet] {npc.NpcName}: '{result.IdeaLabel}' ({catalogId}) depth:{result.Depth:F2}");
        EmitSignal(SignalName.KnowledgeTransferred, npc.NpcName, catalogId, result.Depth);
        EmitSignal(SignalName.Interpretation, npc.NpcName, result.IdeaLabel, result.Reasoning);
    }
}
