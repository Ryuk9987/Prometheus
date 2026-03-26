#nullable disable
using Godot;

/// <summary>
/// Low-poly humanoid NPC visuals with proper articulated skeleton.
///
/// Hierarchy (all in BodyRoot local space, feet at Y=0):
///   BodyRoot (scale = targetHeight / modelHeight)
///     Torso
///     Head
///     LShoulder  → LUpperArm → LElbow → LLowerArm → LHand
///     RShoulder  → RUpperArm → RElbow → RLowerArm → RHand
///     LHip       → LUpperLeg → LKnee  → LLowerLeg → LFoot
///     RHip       → RUpperLeg → RKnee  → RLowerLeg → RFoot
///
/// The model is designed 1.9 units tall, then uniformly scaled per NPC.
/// NpcEntity spawns at Y=0.5, so BodyRoot is offset -0.5 → feet at world Y=0.
/// </summary>
public partial class NpcVisuals : Node3D
{
    // ── Model constants (unscaled, feet at Y=0) ──────────────────────────
    private const float ModelHeight = 1.9f;

    private const float FootY       = 0.00f;
    private const float AnkleY      = 0.13f;
    private const float KneeY       = 0.52f;
    private const float HipY        = 0.90f;
    private const float WaistY      = 1.00f;
    private const float ShoulderY   = 1.45f;
    private const float NeckY       = 1.55f;
    private const float HeadCenterY = 1.70f;
    private const float HipW        = 0.13f;  // hip half-width
    private const float ShoulderW   = 0.22f;  // shoulder half-width

    // ── Animation pivots ─────────────────────────────────────────────────
    private Node3D _lHip, _rHip;       // upper leg pivot
    private Node3D _lKnee, _rKnee;     // lower leg pivot
    private Node3D _lShoulder, _rShoulder; // upper arm pivot
    private Node3D _lElbow, _rElbow;   // lower arm pivot
    private Node3D _bodyRoot;
    private Node3D _accentNode;

    // ── Runtime ──────────────────────────────────────────────────────────
    private NpcEntity _owner;
    private double    _animTime  = 0;
    private float     _walkSpeed = 0f;
    private Vector3   _lastPos   = Vector3.Zero;

    // ── Materials (cached for color update) ──────────────────────────────
    private StandardMaterial3D _tribeMat;
    private StandardMaterial3D _skinMat;
    private Color               _tribeColor = new Color(0.3f, 0.55f, 0.8f);

    // ── Gender & Height ───────────────────────────────────────────────────
    private bool  _female;
    private float _targetHeight;

    public override void _Ready()
    {
        _owner = GetParent<NpcEntity>();

        var rng = new RandomNumberGenerator();
        rng.Seed = (ulong)(_owner.NpcName.GetHashCode() + 12345);

        // Gender: female names often end in a/e/i; rough heuristic
        string n = _owner.NpcName.ToLower();
        _female = n.EndsWith("a") || n.EndsWith("e") || n.EndsWith("i") || n.EndsWith("ra");

        // Height: normal distribution around mean
        float mean  = _female ? 1.65f : 1.80f;
        float sigma = 0.08f;
        _targetHeight = Mathf.Clamp(mean + (float)rng.RandfRange(-sigma*2, sigma*2), 1.5f, 2.0f);

        _animTime = rng.RandfRange(0f, Mathf.Tau);
        _lastPos  = _owner.GlobalPosition;

        // Remove old capsule mesh if present
        _owner.GetNodeOrNull<MeshInstance3D>("MeshInstance3D")?.QueueFree();

        _bodyRoot = new Node3D();
        _bodyRoot.Name  = "BodyRoot";
        _bodyRoot.Scale = Vector3.One * (_targetHeight / ModelHeight);
        // NpcEntity spawns at Y=0 → BodyRoot at origin, feet touch ground
        _bodyRoot.Position = Vector3.Zero;
        AddChild(_bodyRoot);

        BuildBody();
        BuildAccent();

        // Raise name label above head
        var lbl = _owner.GetNodeOrNull<Label3D>("Label3D");
        if (lbl != null) lbl.Position = new Vector3(0, _targetHeight + 0.35f, 0);
    }

    // ── Body construction ─────────────────────────────────────────────────
    private void BuildBody()
    {
        _skinMat  = Mat(_female ? new Color(0.9f, 0.78f, 0.65f) : new Color(0.82f, 0.68f, 0.55f));
        _tribeMat = Mat(_tribeColor);
        var darkMat  = Mat(Darken(_tribeColor, 0.25f));
        var hairMat  = Mat(_female ? new Color(0.55f, 0.35f, 0.2f) : new Color(0.2f, 0.15f, 0.1f));
        var legMat   = Mat(new Color(0.18f, 0.20f, 0.26f));
        var bootMat  = Mat(new Color(0.22f, 0.16f, 0.10f));

        float w = _female ? 0.85f : 1.0f; // width factor for feminine proportions

        // ── Torso
        Add(MakeBox(0.36f*w, 0.45f, 0.20f), _tribeMat, 0, WaistY + 0.22f);
        // Hips/belt
        Add(MakeBox(0.34f*w, 0.18f, 0.20f), darkMat, 0, WaistY + 0.02f);

        // ── Head
        Add(MakeBox(0.26f*w, 0.28f, 0.24f), _skinMat, 0, HeadCenterY);
        // Hair
        Add(MakeBox(0.28f*w, 0.08f, 0.26f), hairMat, 0, HeadCenterY + 0.17f);
        if (_female) // longer hair sides
        {
            Add(MakeBox(0.06f, 0.18f, 0.22f), hairMat, -0.14f*w, HeadCenterY + 0.08f);
            Add(MakeBox(0.06f, 0.18f, 0.22f), hairMat,  0.14f*w, HeadCenterY + 0.08f);
        }
        // Eyes
        Add(MakeBox(0.055f, 0.04f, 0.02f), Mat(new Color(0.1f,0.1f,0.12f)), -0.075f*w, HeadCenterY+0.03f, 0.12f);
        Add(MakeBox(0.055f, 0.04f, 0.02f), Mat(new Color(0.1f,0.1f,0.12f)),  0.075f*w, HeadCenterY+0.03f, 0.12f);
        // Neck
        Add(MakeCyl(0.055f, NeckY - HeadCenterY + 0.15f), _skinMat, 0, NeckY);

        // ── Arms: shoulder pivot → upper arm → elbow pivot → lower arm → hand
        _lShoulder = Pivot(-ShoulderW*w, ShoulderY, 0);
        AddTo(_lShoulder, MakeCyl(0.065f, 0.27f), darkMat, 0, -0.135f);
        _lElbow = PivotChild(_lShoulder, 0, -0.28f, 0);
        AddTo(_lElbow, MakeCyl(0.055f, 0.24f), darkMat, 0, -0.12f);
        AddTo(_lElbow, MakeBox(0.09f, 0.065f, 0.065f), _skinMat, 0, -0.27f);

        _rShoulder = Pivot( ShoulderW*w, ShoulderY, 0);
        AddTo(_rShoulder, MakeCyl(0.065f, 0.27f), darkMat, 0, -0.135f);
        _rElbow = PivotChild(_rShoulder, 0, -0.28f, 0);
        AddTo(_rElbow, MakeCyl(0.055f, 0.24f), darkMat, 0, -0.12f);
        AddTo(_rElbow, MakeBox(0.09f, 0.065f, 0.065f), _skinMat, 0, -0.27f);

        // Initial arm angle: left arm splays left (-Z), right splays right (+Z)
        // Character faces -Z, so left = -X direction needs Z=-22 to tilt outward
        _lShoulder.RotationDegrees = new Vector3(0, 0, -22);
        _rShoulder.RotationDegrees = new Vector3(0, 0,  22);

        // ── Legs: hip pivot → upper leg → knee pivot → lower leg → foot
        _lHip = Pivot(-HipW*w, HipY, 0);
        AddTo(_lHip, MakeCyl(0.085f, 0.37f), legMat, 0, -0.185f);
        _lKnee = PivotChild(_lHip, 0, -0.375f, 0);
        AddTo(_lKnee, MakeCyl(0.075f, 0.33f), legMat, 0, -0.165f);
        AddTo(_lKnee, MakeBox(0.115f, 0.075f, 0.17f), bootMat, 0, -0.365f, 0.02f);

        _rHip = Pivot( HipW*w, HipY, 0);
        AddTo(_rHip, MakeCyl(0.085f, 0.37f), legMat, 0, -0.185f);
        _rKnee = PivotChild(_rHip, 0, -0.375f, 0);
        AddTo(_rKnee, MakeCyl(0.075f, 0.33f), legMat, 0, -0.165f);
        AddTo(_rKnee, MakeBox(0.115f, 0.075f, 0.17f), bootMat, 0, -0.365f, 0.02f);
    }

    // ── Role accent ───────────────────────────────────────────────────────
    private void BuildAccent()
    {
        _accentNode?.QueueFree();
        _accentNode      = new Node3D();
        _accentNode.Name = "Accent_" + _owner.SocialRole;
        _bodyRoot.AddChild(_accentNode);

        switch (_owner.SocialRole)
        {
            case SocialRole.Leader:
                var gold = Mat(new Color(1f, 0.82f, 0.12f));
                AddTo(_accentNode, MakeBox(0.30f, 0.06f, 0.28f), gold, 0, HeadCenterY + 0.17f);
                AddTo(_accentNode, MakeCyl(0.04f, 0.16f), gold, -0.09f, HeadCenterY + 0.29f);
                AddTo(_accentNode, MakeCyl(0.04f, 0.20f), gold,  0.00f, HeadCenterY + 0.31f);
                AddTo(_accentNode, MakeCyl(0.04f, 0.16f), gold,  0.09f, HeadCenterY + 0.29f);
                break;
            case SocialRole.Hunter:
                AddTo(_accentNode, MakeBox(0.065f, 0.24f, 0.065f), Mat(new Color(0.35f,0.22f,0.1f)), 0, WaistY + 0.40f, -0.14f);
                AddTo(_accentNode, MakeBox(0.018f, 0.32f, 0.018f), Mat(new Color(0.45f,0.3f,0.15f)), -0.22f, WaistY + 0.30f, 0);
                break;
            case SocialRole.Builder:
                AddTo(_accentNode, MakeBox(0.40f, 0.055f, 0.22f), Mat(new Color(0.5f,0.35f,0.15f)), 0, WaistY + 0.04f);
                AddTo(_accentNode, MakeBox(0.075f, 0.13f, 0.045f), Mat(new Color(0.55f,0.5f,0.45f)), 0.16f, WaistY + 0.02f, 0.12f);
                break;
            case SocialRole.Healer:
                var wh = Mat(new Color(0.9f, 0.9f, 0.9f));
                AddTo(_accentNode, MakeBox(0.055f, 0.22f, 0.025f), wh, 0, WaistY + 0.28f, 0.11f);
                AddTo(_accentNode, MakeBox(0.20f,  0.055f, 0.025f), wh, 0, WaistY + 0.32f, 0.11f);
                break;
            case SocialRole.Gatherer:
                AddTo(_accentNode, MakeBox(0.17f, 0.20f, 0.09f), Mat(new Color(0.38f,0.58f,0.22f)), 0, WaistY + 0.28f, -0.14f);
                break;
            case SocialRole.Farmer:
                var straw = Mat(new Color(0.85f, 0.72f, 0.3f));
                AddTo(_accentNode, MakeBox(0.36f, 0.045f, 0.34f), straw, 0, HeadCenterY + 0.20f);
                AddTo(_accentNode, MakeCyl(0.13f, 0.10f), straw, 0, HeadCenterY + 0.22f);
                break;
        }
    }

    // ── Animation ─────────────────────────────────────────────────────────
    public override void _Process(double delta)
    {
        if (_owner == null || _bodyRoot == null) return;

        // Tribe color live-update
        var tribe = TribeManager.Instance?.GetTribe(_owner);
        var tc    = tribe?.Color ?? new Color(0.3f, 0.55f, 0.8f);
        if (!tc.IsEqualApprox(_tribeColor))
        {
            _tribeColor = tc;
            if (_tribeMat != null) _tribeMat.AlbedoColor = tc;
        }

        // Rebuild accent if role changed
        if (_accentNode != null && _accentNode.Name != "Accent_" + _owner.SocialRole)
            BuildAccent();

        // Speed & direction
        _animTime   += delta;
        var curPos   = _owner.GlobalPosition;
        var movement = curPos - _lastPos;
        float moved  = (float)movement.Length();
        float speed  = (float)delta > 0.0001 ? moved / (float)delta : 0f;
        _walkSpeed   = Mathf.Lerp(_walkSpeed, speed, 0.3f);

        // Face movement direction
        var flatDir = new Vector3(movement.X, 0, movement.Z);
        if (flatDir.LengthSquared() > 0.00001f)
        {
            // Model front = +Z, so face movement direction directly
            float target = Mathf.Atan2(flatDir.X, flatDir.Z);
            _owner.Rotation = new Vector3(0, Mathf.LerpAngle(_owner.Rotation.Y, target, 0.2f), 0);
        }
        _lastPos = curPos;

        if (_walkSpeed > 0.25f) Walk(); else Idle();
    }

    private void Walk()
    {
        float t   = (float)_animTime * 2.4f * Mathf.Tau;
        float sw  = Mathf.Sin(t);
        float leg = sw * 32f;    // upper leg degrees
        float kn  = Mathf.Max(0f, -sw) * 22f; // knee bend (only backward leg bends)

        // Legs
        if (_lHip  != null) _lHip.RotationDegrees  = new Vector3( leg, 0, 0);
        if (_rHip  != null) _rHip.RotationDegrees   = new Vector3(-leg, 0, 0);
        if (_lKnee != null) _lKnee.RotationDegrees  = new Vector3(Mathf.Max(0f,  sw)*22f, 0, 0);
        if (_rKnee != null) _rKnee.RotationDegrees  = new Vector3(Mathf.Max(0f, -sw)*22f, 0, 0);

        // Arms (opposite swing)
        float arm = -sw * 22f;
        if (_lShoulder != null) _lShoulder.RotationDegrees = new Vector3( arm, 0, -22f);
        if (_rShoulder != null) _rShoulder.RotationDegrees = new Vector3(-arm, 0,  22f);

        _bodyRoot.Position = new Vector3(0, Mathf.Abs(sw) * 0.012f, 0);
    }

    private void Idle()
    {
        float t      = (float)_animTime * 0.75f * Mathf.Tau;
        float breath = Mathf.Sin(t) * 0.004f;

        _bodyRoot.Position = new Vector3(0, breath, 0);

        // Smooth return to rest
        LerpDeg(_lHip,       Vector3.Zero, 0.07f);
        LerpDeg(_rHip,       Vector3.Zero, 0.07f);
        LerpDeg(_lKnee,      Vector3.Zero, 0.07f);
        LerpDeg(_rKnee,      Vector3.Zero, 0.07f);
        LerpDeg(_lShoulder,  new Vector3(0, 0, -22f), 0.07f);
        LerpDeg(_rShoulder,  new Vector3(0, 0,  22f), 0.07f);
        LerpDeg(_lElbow,     Vector3.Zero, 0.07f);
        LerpDeg(_rElbow,     Vector3.Zero, 0.07f);
    }

    private static void LerpDeg(Node3D n, Vector3 targetDeg, float t)
    {
        if (n == null) return;
        n.RotationDegrees = n.RotationDegrees.Lerp(targetDeg, t);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    /// <summary>Add a mesh directly to BodyRoot at absolute local position.</summary>
    private void Add(Mesh mesh, StandardMaterial3D mat,
        float x, float y, float z = 0)
    {
        var mi = new MeshInstance3D();
        mi.Mesh = mesh;
        mi.Position = new Vector3(x, y, z);
        mesh.SurfaceSetMaterial(0, mat);
        _bodyRoot.AddChild(mi);
    }

    /// <summary>Add a mesh to a specific parent node at LOCAL offset.</summary>
    private void AddTo(Node3D parent, Mesh mesh, StandardMaterial3D mat,
        float x, float y, float z = 0)
    {
        var mi = new MeshInstance3D();
        mi.Mesh = mesh;
        mi.Position = new Vector3(x, y, z);
        mesh.SurfaceSetMaterial(0, mat);
        parent.AddChild(mi);
    }

    /// <summary>Create a pivot Node3D child of BodyRoot.</summary>
    private Node3D Pivot(float x, float y, float z)
    {
        var p = new Node3D();
        p.Position = new Vector3(x, y, z);
        _bodyRoot.AddChild(p);
        return p;
    }

    /// <summary>Create a pivot Node3D child of another Node3D.</summary>
    private static Node3D PivotChild(Node3D parent, float x, float y, float z)
    {
        var p = new Node3D();
        p.Position = new Vector3(x, y, z);
        parent.AddChild(p);
        return p;
    }

    private static BoxMesh MakeBox(float x, float y, float z)
    {
        var m = new BoxMesh();
        m.Size = new Vector3(x, y, z);
        return m;
    }

    private static CylinderMesh MakeCyl(float r, float h)
    {
        var m = new CylinderMesh();
        m.TopRadius    = r * 0.85f;
        m.BottomRadius = r;
        m.Height       = h;
        m.RadialSegments = 5;
        return m;
    }

    private static StandardMaterial3D Mat(Color c)
    {
        var m = new StandardMaterial3D();
        m.AlbedoColor = c;
        m.ShadingMode = BaseMaterial3D.ShadingModeEnum.PerVertex;
        return m;
    }

    private static Color Darken(Color c, float a)
        => new Color(Mathf.Max(0,c.R-a), Mathf.Max(0,c.G-a), Mathf.Max(0,c.B-a));
}
