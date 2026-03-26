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

    public override void _Ready()
    {
        _owner = GetParent<NpcEntity>();

        // Remove default capsule if exists
        var oldMesh = _owner.GetNodeOrNull<MeshInstance3D>("MeshInstance3D");
        oldMesh?.QueueFree();
        var oldCapsule = _owner.GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
        // Keep collision shape

        _bodyRoot = new Node3D();
        _bodyRoot.Name = "BodyRoot";
        AddChild(_bodyRoot);

        BuildHumanoid();
    }

    public override void _Process(double delta)
    {
        // Update tribe color + role accent periodically
        if (_owner == null) return;

        var tribe = TribeManager.Instance?.GetTribe(_owner);
        var newTribeColor = tribe?.Color ?? new Color(0.3f, 0.55f, 0.8f);

        if (!newTribeColor.IsEqualApprox(_tribeColor))
        {
            _tribeColor = newTribeColor;
            if (_tribeMat != null) _tribeMat.AlbedoColor = _tribeColor;
        }

        UpdateRoleAccent();
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

        // ── Arms
        var armMat = Mat(Darken(_tribeColor, 0.15f));
        var handMat = _skinMat;
        // Left arm
        Part(MakeCylinder(0.07f, 0.28f), armMat,  new Vector3(-0.27f, 1.27f, 0), rotZ:  20f);
        Part(MakeCylinder(0.06f, 0.26f), armMat,  new Vector3(-0.32f, 0.97f, 0), rotZ:  10f);
        Part(MakeBox(0.10f, 0.07f, 0.07f), handMat, new Vector3(-0.35f, 0.82f, 0));
        // Right arm
        Part(MakeCylinder(0.07f, 0.28f), armMat,  new Vector3( 0.27f, 1.27f, 0), rotZ: -20f);
        Part(MakeCylinder(0.06f, 0.26f), armMat,  new Vector3( 0.32f, 0.97f, 0), rotZ: -10f);
        Part(MakeBox(0.10f, 0.07f, 0.07f), handMat, new Vector3( 0.35f, 0.82f, 0));

        // ── Legs
        var legMat  = Mat(new Color(0.2f, 0.22f, 0.28f));
        var bootMat = Mat(new Color(0.25f, 0.18f, 0.12f));
        // Left leg
        Part(MakeCylinder(0.09f, 0.34f), legMat,  new Vector3(-0.12f, 0.68f, 0));
        Part(MakeCylinder(0.08f, 0.30f), legMat,  new Vector3(-0.12f, 0.31f, 0));
        Part(MakeBox(0.12f, 0.08f, 0.18f), bootMat, new Vector3(-0.12f, 0.09f, 0.03f));
        // Right leg
        Part(MakeCylinder(0.09f, 0.34f), legMat,  new Vector3( 0.12f, 0.68f, 0));
        Part(MakeCylinder(0.08f, 0.30f), legMat,  new Vector3( 0.12f, 0.31f, 0));
        Part(MakeBox(0.12f, 0.08f, 0.18f), bootMat, new Vector3( 0.12f, 0.09f, 0.03f));

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
