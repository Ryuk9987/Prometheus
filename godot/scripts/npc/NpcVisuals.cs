#nullable disable
using Godot;

/// <summary>
/// NpcVisuals — replaces the capsule with a stylized low-poly humanoid.
///
/// Body parts (all BoxMesh/SphereMesh, low vertex count):
///   Head       — slightly rounded box, skin color
///   Torso      — tall box, tribe color
///   Hips       — wide short box
///   Upper arms — thin cylinders
///   Lower arms — thinner cylinders
///   Hands      — tiny spheres
///   Upper legs — cylinders
///   Lower legs — thinner cylinders
///   Feet       — flat boxes
///
/// Role accent: small colored shape on torso:
///   Leader   — gold crown spike on head
///   Hunter   — dark cape (box behind torso)
///   Builder  — tool belt (thin box at waist)
///   Healer   — cross mark (two thin boxes on torso)
///   Gatherer — leaf mark (small sphere on back)
/// </summary>
public partial class NpcVisuals : Node3D
{
    private NpcEntity _owner;
    private Node3D    _bodyRoot;
    private MeshInstance3D _torso;
    private MeshInstance3D _head;
    private StandardMaterial3D _skinMat;
    private StandardMaterial3D _tribeMat;
    private StandardMaterial3D _accentMat;

    // Colors
    private Color _tribeColor  = new Color(0.3f, 0.55f, 0.8f);
    private Color _skinColor   = new Color(0.85f, 0.72f, 0.58f);
    private Color _clothColor  = new Color(0.25f, 0.35f, 0.45f);
    private Color _hairColor   = new Color(0.2f,  0.15f, 0.1f);

    // ── Animation state ───────────────────────────────────────────────────
    private Node3D _leftUpperArm, _rightUpperArm;
    private Node3D _leftUpperLeg, _rightUpperLeg;
    private Node3D _leftLowerLeg, _rightLowerLeg;
    private Node3D _headNode;

    private double _animTime   = 0;
    private Vector3 _lastPos   = Vector3.Zero;
    private float _walkSpeed   = 0f;
    private const float Scale  = 0.52f; // world scale

    public override void _Ready()
    {
        _owner = GetParent<NpcEntity>();

        var oldMesh = _owner.GetNodeOrNull<MeshInstance3D>("MeshInstance3D");
        oldMesh?.QueueFree();

        _bodyRoot = new Node3D();
        _bodyRoot.Name = "BodyRoot";
        _bodyRoot.Scale = Vector3.One * Scale;
        AddChild(_bodyRoot);

        _lastPos = _owner.GlobalPosition;
        _animTime = GD.RandRange(0.0, Mathf.Tau); // stagger animations
        BuildHumanoid();
    }

    public override void _Process(double delta)
    {
        if (_owner == null) return;

        // Tribe color live-update
        var tribe = TribeManager.Instance?.GetTribe(_owner);
        var newTribeColor = tribe?.Color ?? new Color(0.3f, 0.55f, 0.8f);
        if (!newTribeColor.IsEqualApprox(_tribeColor))
        {
            _tribeColor = newTribeColor;
            if (_tribeMat != null) _tribeMat.AlbedoColor = _tribeColor;
        }

        UpdateRoleAccent();
        Animate(delta);
    }

    // ── Animation ─────────────────────────────────────────────────────────
    private void Animate(double delta)
    {
        _animTime += delta;

        // Measure movement speed
        var newPos  = _owner.GlobalPosition;
        _walkSpeed  = Mathf.Lerp(_walkSpeed,
            (float)(newPos - _lastPos).Length() / (float)delta, 0.2f);
        _lastPos    = newPos;

        bool moving = _walkSpeed > 0.05f;

        if (moving)
            AnimateWalk();
        else
            AnimateIdle();
    }

    private void AnimateWalk()
    {
        float t  = (float)_animTime * 2.5f * Mathf.Tau;
        float sw = Mathf.Sin(t);
        float swDeg = sw * 35f;

        SetRot(_leftUpperLeg,   swDeg,  0f);
        SetRot(_rightUpperLeg, -swDeg,  0f);
        SetRot(_leftLowerLeg,   Mathf.Max(0f, sw) * 18f, 0f);
        SetRot(_rightLowerLeg,  Mathf.Max(0f,-sw) * 18f, 0f);
        SetRot(_leftUpperArm,  -swDeg * 0.7f,  20f);
        SetRot(_rightUpperArm,  swDeg * 0.7f, -20f);

        if (_bodyRoot != null)
            _bodyRoot.Position = new Vector3(0, Mathf.Abs(sw) * 0.012f, 0);
    }

    private void AnimateIdle()
    {
        float t      = (float)_animTime * 0.8f * Mathf.Tau;
        float breath = Mathf.Sin(t) * 0.005f;
        if (_bodyRoot != null) _bodyRoot.Position = new Vector3(0, breath, 0);

        LerpRot(_leftUpperArm,  0f,  20f, 0.06f);
        LerpRot(_rightUpperArm, 0f, -20f, 0.06f);
        LerpRot(_leftUpperLeg,  0f,  0f,  0.06f);
        LerpRot(_rightUpperLeg, 0f,  0f,  0.06f);
        LerpRot(_leftLowerLeg,  0f,  0f,  0.06f);
        LerpRot(_rightLowerLeg, 0f,  0f,  0.06f);
    }

    private static void SetRot(Node3D node, float xDeg, float zDeg)
    {
        if (node == null) return;
        node.Rotation = new Vector3(Mathf.DegToRad(xDeg), node.Rotation.Y, Mathf.DegToRad(zDeg));
    }

    private static void LerpRot(Node3D node, float xDeg, float zDeg, float t)
    {
        if (node == null) return;
        var target = new Vector3(Mathf.DegToRad(xDeg), node.Rotation.Y, Mathf.DegToRad(zDeg));
        node.Rotation = node.Rotation.Lerp(target, t);
    }

    // ── Build humanoid ────────────────────────────────────────────────────
    private void BuildHumanoid()
    {
        _skinMat  = Mat(_skinColor);
        _tribeMat = Mat(_tribeColor);

        // ── Head
        _head = Part(MakeBox(0.28f, 0.30f, 0.26f), _skinMat,
                     new Vector3(0, 1.62f, 0));
        // Eyes (tiny dark boxes)
        Part(MakeBox(0.06f, 0.04f, 0.04f), Mat(new Color(0.1f,0.1f,0.12f)),
             new Vector3(-0.08f, 1.64f, 0.13f));
        Part(MakeBox(0.06f, 0.04f, 0.04f), Mat(new Color(0.1f,0.1f,0.12f)),
             new Vector3( 0.08f, 1.64f, 0.13f));
        // Hair
        Part(MakeBox(0.3f, 0.08f, 0.28f), Mat(_hairColor),
             new Vector3(0, 1.79f, 0));

        // ── Neck
        Part(MakeCylinder(0.06f, 0.12f), _skinMat, new Vector3(0, 1.5f, 0));

        // ── Torso
        _torso = Part(MakeBox(0.38f, 0.45f, 0.22f), _tribeMat,
                      new Vector3(0, 1.22f, 0));

        // ── Hips / Pelvis
        var hipMat = Mat(Darken(_tribeColor, 0.3f));
        Part(MakeBox(0.36f, 0.18f, 0.22f), hipMat, new Vector3(0, 0.95f, 0));

        // ── Arms (pivot at shoulder for animation)
        var armMat  = Mat(Darken(_tribeColor, 0.15f));
        var handMat = _skinMat;

        // ── Arms (pivot at shoulder)
        Pivot2(-0.26f, 1.44f, 0, armMat, MakeCylinder(0.07f, 0.28f),
               new Vector3(0, -0.14f, 0), rotZ: 20f, out _leftUpperArm);
        Part(MakeCylinder(0.06f, 0.26f), armMat,    new Vector3(-0.30f, 0.97f, 0), rotZ: 10f);
        Part(MakeBox(0.10f, 0.07f, 0.07f), handMat, new Vector3(-0.33f, 0.82f, 0));

        Pivot2( 0.26f, 1.44f, 0, armMat, MakeCylinder(0.07f, 0.28f),
                new Vector3(0, -0.14f, 0), rotZ: -20f, out _rightUpperArm);
        Part(MakeCylinder(0.06f, 0.26f), armMat,    new Vector3( 0.30f, 0.97f, 0), rotZ: -10f);
        Part(MakeBox(0.10f, 0.07f, 0.07f), handMat, new Vector3( 0.33f, 0.82f, 0));

        // ── Legs (pivot at hip)
        var legMat  = Mat(new Color(0.2f, 0.22f, 0.28f));
        var bootMat = Mat(new Color(0.25f, 0.18f, 0.12f));

        Pivot2(-0.12f, 0.86f, 0, legMat, MakeCylinder(0.09f, 0.34f),
               new Vector3(0, -0.17f, 0), out _leftUpperLeg);
        Pivot2(-0.12f, 0.50f, 0, legMat, MakeCylinder(0.08f, 0.30f),
               new Vector3(0, -0.15f, 0), out _leftLowerLeg);
        Part(MakeBox(0.13f, 0.08f, 0.18f), bootMat, new Vector3(-0.12f, 0.09f, 0.03f));

        Pivot2( 0.12f, 0.86f, 0, legMat, MakeCylinder(0.09f, 0.34f),
                new Vector3(0, -0.17f, 0), out _rightUpperLeg);
        Pivot2( 0.12f, 0.50f, 0, legMat, MakeCylinder(0.08f, 0.30f),
                new Vector3(0, -0.15f, 0), out _rightLowerLeg);
        Part(MakeBox(0.13f, 0.08f, 0.18f), bootMat, new Vector3( 0.12f, 0.09f, 0.03f));

        // Name label — float above head
        var nameLabel = _owner.GetNodeOrNull<Label3D>("Label3D");
        if (nameLabel != null) nameLabel.Position = new Vector3(0, 2.1f, 0);

        AddRoleAccent();
    }

    // ── Role accent ───────────────────────────────────────────────────────
    private Node3D _accentNode;

    private void AddRoleAccent()
    {
        _accentNode?.QueueFree();
        _accentNode = new Node3D();
        _accentNode.Name = "RoleAccent";
        _bodyRoot.AddChild(_accentNode);
        BuildAccentForRole(_owner.SocialRole);
    }

    private void UpdateRoleAccent()
    {
        // Only rebuild if role changed
        if (_accentNode?.Name == "RoleAccent_" + _owner.SocialRole.ToString()) return;
        _accentNode?.QueueFree();
        _accentNode = new Node3D();
        _accentNode.Name = "RoleAccent_" + _owner.SocialRole.ToString();
        _bodyRoot.AddChild(_accentNode);
        BuildAccentForRole(_owner.SocialRole);
    }

    private void BuildAccentForRole(SocialRole role)
    {
        switch (role)
        {
            case SocialRole.Leader:
                // Gold crown: 3 spikes on head
                var goldMat = Mat(new Color(1f, 0.82f, 0.1f));
                AccentPart(MakeCylinder(0.04f, 0.15f), goldMat, new Vector3(-0.09f, 1.93f,  0));
                AccentPart(MakeCylinder(0.04f, 0.2f),  goldMat, new Vector3(0,      1.97f,  0));
                AccentPart(MakeCylinder(0.04f, 0.15f), goldMat, new Vector3( 0.09f, 1.93f,  0));
                // Crown band
                AccentPart(MakeBox(0.32f, 0.06f, 0.30f), goldMat, new Vector3(0, 1.82f, 0));
                break;

            case SocialRole.Hunter:
                // Dark quiver on back
                AccentPart(MakeBox(0.07f, 0.26f, 0.07f), Mat(new Color(0.35f,0.22f,0.1f)),
                    new Vector3(0, 1.25f, -0.15f));
                // Small bow
                AccentPart(MakeBox(0.02f, 0.35f, 0.02f), Mat(new Color(0.45f,0.3f,0.15f)),
                    new Vector3(-0.24f, 1.1f, 0.02f), rotZ: 15f);
                break;

            case SocialRole.Builder:
                // Tool belt
                AccentPart(MakeBox(0.42f, 0.06f, 0.24f), Mat(new Color(0.5f,0.35f,0.15f)),
                    new Vector3(0, 1.01f, 0));
                // Hammer on belt
                AccentPart(MakeBox(0.08f, 0.14f, 0.05f), Mat(new Color(0.55f,0.5f,0.45f)),
                    new Vector3(0.18f, 0.98f, 0.12f));
                break;

            case SocialRole.Healer:
                // White cross on torso
                var wMat = Mat(new Color(0.9f, 0.9f, 0.9f));
                AccentPart(MakeBox(0.06f, 0.25f, 0.03f), wMat, new Vector3(0, 1.22f, 0.12f));
                AccentPart(MakeBox(0.22f, 0.06f, 0.03f), wMat, new Vector3(0, 1.25f, 0.12f));
                break;

            case SocialRole.Gatherer:
                // Leaf/bag on back
                AccentPart(MakeBox(0.18f, 0.22f, 0.1f), Mat(new Color(0.4f,0.6f,0.25f)),
                    new Vector3(0, 1.18f, -0.16f));
                break;

            case SocialRole.Farmer:
                // Straw hat
                AccentPart(MakeBox(0.38f, 0.05f, 0.36f), Mat(new Color(0.85f,0.72f,0.3f)),
                    new Vector3(0, 1.83f, 0));
                AccentPart(MakeCylinder(0.15f, 0.1f), Mat(new Color(0.8f,0.68f,0.28f)),
                    new Vector3(0, 1.86f, 0));
                break;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    private MeshInstance3D Pivot2(float px, float py, float pz,
        StandardMaterial3D mat, Mesh mesh, Vector3 meshOffset,
        float rotX = 0, float rotZ = 0, out Node3D pivot_out)
    {
        pivot_out = new Node3D();
        pivot_out.Position = new Vector3(px, py, pz);
        if (rotZ != 0) pivot_out.RotateZ(Mathf.DegToRad(rotZ));
        _bodyRoot.AddChild(pivot_out);
        var mi = new MeshInstance3D();
        mi.Mesh = mesh; mi.Position = meshOffset;
        mesh.SurfaceSetMaterial(0, mat);
        pivot_out.AddChild(mi);
        return mi;
    }

    private MeshInstance3D Part(Mesh mesh, StandardMaterial3D mat,
        Vector3 pos, float rotX = 0, float rotZ = 0)
    {
        var mi = new MeshInstance3D();
        mi.Mesh     = mesh;
        mi.Position = pos;
        if (rotX != 0) mi.RotateX(Mathf.DegToRad(rotX));
        if (rotZ != 0) mi.RotateZ(Mathf.DegToRad(rotZ));
        mesh.SurfaceSetMaterial(0, mat);
        _bodyRoot.AddChild(mi);
        return mi;
    }

    private void AccentPart(Mesh mesh, StandardMaterial3D mat,
        Vector3 pos, float rotX = 0, float rotZ = 0)
    {
        var mi = new MeshInstance3D();
        mi.Mesh     = mesh;
        mi.Position = pos;
        if (rotX != 0) mi.RotateX(Mathf.DegToRad(rotX));
        if (rotZ != 0) mi.RotateZ(Mathf.DegToRad(rotZ));
        mesh.SurfaceSetMaterial(0, mat);
        _accentNode.AddChild(mi);
    }

    private static BoxMesh MakeBox(float x, float y, float z)
    {
        var m = new BoxMesh();
        m.Size = new Vector3(x, y, z);
        return m;
    }

    private static CylinderMesh MakeCylinder(float r, float h)
    {
        var m = new CylinderMesh();
        m.TopRadius = r * 0.85f;
        m.BottomRadius = r;
        m.Height = h;
        m.RadialSegments = 5; // Low poly — pentagon
        return m;
    }

    private static StandardMaterial3D Mat(Color c)
    {
        var m = new StandardMaterial3D();
        m.AlbedoColor = c;
        m.ShadingMode = BaseMaterial3D.ShadingModeEnum.PerVertex; // flat shading
        return m;
    }

    private static Color Darken(Color c, float amount)
        => new Color(c.R - amount, c.G - amount, c.B - amount, c.A);
}
