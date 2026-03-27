#nullable disable
using Godot;

/// <summary>
/// AnimalVisuals — procedural low-poly animal meshes with walk/idle animations.
///
/// All geometry built in code (no .tscn assets needed).
/// Each animal has:
///   - A body hierarchy with articulated legs
///   - Idle animation (breathing / ear twitch)
///   - Walk animation (leg swing)
///   - Flee animation (faster leg swing + head down)
///
/// Attach as child of Animal node.
/// </summary>
public partial class AnimalVisuals : Node3D
{
    private Animal    _owner;
    private double    _animTimer = 0;

    // Articulated limb nodes
    private Node3D _body;
    private Node3D _head;
    private Node3D _neck;
    private Node3D _tail;
    private Node3D _legFL, _legFR, _legBL, _legBR; // Front-Left/Right, Back-Left/Right
    private Node3D _earL, _earR;   // Rabbit only
    private Node3D _antlerL, _antlerR; // Deer only

    // Shadow circle
    private MeshInstance3D _shadow;

    public override void _Ready()
    {
        _owner = GetParent<Animal>();
        BuildAnimal();
    }

    public override void _Process(double delta)
    {
        _animTimer += delta;
        AnimateAnimal(delta);
    }

    // ═══════════════════════════════════════════════════════════════════
    // BUILD
    // ═══════════════════════════════════════════════════════════════════
    private void BuildAnimal()
    {
        switch (_owner.Type)
        {
            case AnimalType.Deer:   BuildDeer();   break;
            case AnimalType.Boar:   BuildBoar();   break;
            case AnimalType.Rabbit: BuildRabbit(); break;
        }
        BuildShadow();
    }

    // ── DEER ─────────────────────────────────────────────────────────────
    private void BuildDeer()
    {
        var bodyColor = new Color(0.60f, 0.42f, 0.22f);  // warm brown
        var bellyColor= new Color(0.80f, 0.68f, 0.50f);  // lighter belly
        var darkColor = new Color(0.35f, 0.22f, 0.10f);  // dark legs

        // Body
        _body = MakeNode("Body", Vector3.Zero);
        AddChild(_body);
        AddMesh(_body, MakeBox(0.45f, 0.30f, 0.80f), bodyColor, new Vector3(0, 0.55f, 0));

        // Belly patch
        AddMesh(_body, MakeBox(0.30f, 0.05f, 0.60f), bellyColor, new Vector3(0, 0.40f, 0));

        // Neck
        _neck = MakeNode("Neck", new Vector3(0, 0.65f, 0.35f));
        _body.AddChild(_neck);
        AddMesh(_neck, MakeBox(0.14f, 0.35f, 0.14f), bodyColor, new Vector3(0, 0.17f, 0));
        _neck.RotationDegrees = new Vector3(-30f, 0, 0);

        // Head
        _head = MakeNode("Head", new Vector3(0, 0.28f, 0.05f));
        _neck.AddChild(_head);
        AddMesh(_head, MakeBox(0.20f, 0.18f, 0.28f), bodyColor, new Vector3(0, 0, 0));
        // Snout
        AddMesh(_head, MakeBox(0.10f, 0.10f, 0.18f), bellyColor, new Vector3(0, -0.03f, 0.18f));
        // Eyes
        AddMesh(_head, MakeSphere(0.03f), new Color(0.1f,0.1f,0.1f), new Vector3( 0.09f, 0.04f, 0.10f));
        AddMesh(_head, MakeSphere(0.03f), new Color(0.1f,0.1f,0.1f), new Vector3(-0.09f, 0.04f, 0.10f));

        // Antlers (simple Y-shape branches)
        _antlerL = MakeNode("AntlerL", new Vector3( 0.07f, 0.09f, 0.02f));
        _head.AddChild(_antlerL);
        AddMesh(_antlerL, MakeBox(0.03f, 0.20f, 0.03f), darkColor, new Vector3(0, 0.10f, 0));
        AddMesh(_antlerL, MakeBox(0.12f, 0.03f, 0.03f), darkColor, new Vector3(0, 0.18f, 0));

        _antlerR = MakeNode("AntlerR", new Vector3(-0.07f, 0.09f, 0.02f));
        _head.AddChild(_antlerR);
        AddMesh(_antlerR, MakeBox(0.03f, 0.20f, 0.03f), darkColor, new Vector3(0, 0.10f, 0));
        AddMesh(_antlerR, MakeBox(0.12f, 0.03f, 0.03f), darkColor, new Vector3(0, 0.18f, 0));

        // Tail (white)
        _tail = MakeNode("Tail", new Vector3(0, 0.60f, -0.40f));
        _body.AddChild(_tail);
        AddMesh(_tail, MakeSphere(0.07f), new Color(0.95f,0.95f,0.90f), Vector3.Zero);

        // Legs (4)
        _legFL = MakeLeg(_body, new Vector3( 0.18f, 0.40f,  0.25f), darkColor, 0.55f);
        _legFR = MakeLeg(_body, new Vector3(-0.18f, 0.40f,  0.25f), darkColor, 0.55f);
        _legBL = MakeLeg(_body, new Vector3( 0.18f, 0.40f, -0.25f), darkColor, 0.55f);
        _legBR = MakeLeg(_body, new Vector3(-0.18f, 0.40f, -0.25f), darkColor, 0.55f);
    }

    // ── BOAR ──────────────────────────────────────────────────────────────
    private void BuildBoar()
    {
        var bodyColor = new Color(0.35f, 0.28f, 0.22f);  // dark grey-brown
        var bristleColor = new Color(0.22f, 0.18f, 0.14f);
        var tuskColor = new Color(0.92f, 0.88f, 0.75f);

        _body = MakeNode("Body", Vector3.Zero);
        AddChild(_body);
        // Stocky barrel body
        AddMesh(_body, MakeBox(0.50f, 0.38f, 0.70f), bodyColor, new Vector3(0, 0.38f, 0));
        // Bristle ridge along back
        AddMesh(_body, MakeBox(0.12f, 0.10f, 0.65f), bristleColor, new Vector3(0, 0.73f, 0));

        // Head (large, low)
        _head = MakeNode("Head", new Vector3(0, 0.42f, 0.40f));
        _body.AddChild(_head);
        AddMesh(_head, MakeBox(0.35f, 0.28f, 0.38f), bodyColor, Vector3.Zero);
        // Snout disc
        AddMesh(_head, MakeCylinder(0.10f, 0.08f), bodyColor, new Vector3(0, -0.04f, 0.20f));
        // Nostrils
        AddMesh(_head, MakeSphere(0.03f), bristleColor, new Vector3( 0.05f, -0.03f, 0.24f));
        AddMesh(_head, MakeSphere(0.03f), bristleColor, new Vector3(-0.05f, -0.03f, 0.24f));
        // Small eyes
        AddMesh(_head, MakeSphere(0.025f), new Color(0.05f,0.05f,0.05f), new Vector3( 0.14f, 0.05f, 0.14f));
        AddMesh(_head, MakeSphere(0.025f), new Color(0.05f,0.05f,0.05f), new Vector3(-0.14f, 0.05f, 0.14f));
        // Tusks
        AddMesh(_head, MakeBox(0.04f, 0.04f, 0.12f), tuskColor, new Vector3( 0.12f,-0.08f, 0.22f));
        AddMesh(_head, MakeBox(0.04f, 0.04f, 0.12f), tuskColor, new Vector3(-0.12f,-0.08f, 0.22f));
        // Small ears
        AddMesh(_head, MakeBox(0.07f, 0.10f, 0.04f), bristleColor, new Vector3( 0.16f, 0.14f, 0));
        AddMesh(_head, MakeBox(0.07f, 0.10f, 0.04f), bristleColor, new Vector3(-0.16f, 0.14f, 0));

        // Short tail
        _tail = MakeNode("Tail", new Vector3(0, 0.50f, -0.35f));
        _body.AddChild(_tail);
        AddMesh(_tail, MakeBox(0.04f, 0.10f, 0.04f), bristleColor, new Vector3(0, 0.05f, 0));

        // Short stocky legs
        _legFL = MakeLeg(_body, new Vector3( 0.18f, 0.38f,  0.22f), bristleColor, 0.38f);
        _legFR = MakeLeg(_body, new Vector3(-0.18f, 0.38f,  0.22f), bristleColor, 0.38f);
        _legBL = MakeLeg(_body, new Vector3( 0.18f, 0.38f, -0.22f), bristleColor, 0.38f);
        _legBR = MakeLeg(_body, new Vector3(-0.18f, 0.38f, -0.22f), bristleColor, 0.38f);
    }

    // ── RABBIT ────────────────────────────────────────────────────────────
    private void BuildRabbit()
    {
        var furColor  = new Color(0.80f, 0.72f, 0.60f); // sandy beige
        var bellyColor= new Color(0.95f, 0.92f, 0.85f);
        var darkColor = new Color(0.45f, 0.35f, 0.25f);

        _body = MakeNode("Body", Vector3.Zero);
        AddChild(_body);
        // Round body
        AddMesh(_body, MakeSphere(0.18f), furColor, new Vector3(0, 0.22f, 0));
        // Belly
        AddMesh(_body, MakeSphere(0.12f), bellyColor, new Vector3(0, 0.20f, 0.05f));

        // Head
        _head = MakeNode("Head", new Vector3(0, 0.32f, 0.14f));
        _body.AddChild(_head);
        AddMesh(_head, MakeSphere(0.13f), furColor, Vector3.Zero);
        // Cheeks
        AddMesh(_head, MakeSphere(0.07f), bellyColor, new Vector3( 0.06f,-0.03f, 0.07f));
        AddMesh(_head, MakeSphere(0.07f), bellyColor, new Vector3(-0.06f,-0.03f, 0.07f));
        // Nose
        AddMesh(_head, MakeSphere(0.025f), new Color(0.9f,0.5f,0.55f), new Vector3(0,-0.03f, 0.12f));
        // Eyes
        AddMesh(_head, MakeSphere(0.03f), new Color(0.6f,0.1f,0.1f), new Vector3( 0.07f, 0.04f, 0.09f));
        AddMesh(_head, MakeSphere(0.03f), new Color(0.6f,0.1f,0.1f), new Vector3(-0.07f, 0.04f, 0.09f));

        // Long ears
        _earL = MakeNode("EarL", new Vector3( 0.05f, 0.10f, 0));
        _head.AddChild(_earL);
        AddMesh(_earL, MakeBox(0.05f, 0.22f, 0.03f), furColor,  new Vector3(0, 0.11f, 0));
        AddMesh(_earL, MakeBox(0.025f,0.18f, 0.015f), new Color(0.9f,0.5f,0.55f), new Vector3(0, 0.11f, 0.008f));

        _earR = MakeNode("EarR", new Vector3(-0.05f, 0.10f, 0));
        _head.AddChild(_earR);
        AddMesh(_earR, MakeBox(0.05f, 0.22f, 0.03f), furColor,  new Vector3(0, 0.11f, 0));
        AddMesh(_earR, MakeBox(0.025f,0.18f, 0.015f), new Color(0.9f,0.5f,0.55f), new Vector3(0, 0.11f, 0.008f));

        // Fluffy tail
        _tail = MakeNode("Tail", new Vector3(0, 0.25f, -0.16f));
        _body.AddChild(_tail);
        AddMesh(_tail, MakeSphere(0.07f), bellyColor, Vector3.Zero);

        // Small legs
        _legFL = MakeLeg(_body, new Vector3( 0.09f, 0.22f,  0.10f), darkColor, 0.20f);
        _legFR = MakeLeg(_body, new Vector3(-0.09f, 0.22f,  0.10f), darkColor, 0.20f);
        _legBL = MakeLeg(_body, new Vector3( 0.09f, 0.22f, -0.08f), darkColor, 0.24f); // longer back legs
        _legBR = MakeLeg(_body, new Vector3(-0.09f, 0.22f, -0.08f), darkColor, 0.24f);
    }

    // ── Shadow ────────────────────────────────────────────────────────────
    private void BuildShadow()
    {
        float r = _owner.Type switch { AnimalType.Deer => 0.35f, AnimalType.Boar => 0.30f, _ => 0.15f };
        _shadow = new MeshInstance3D();
        var mat = new StandardMaterial3D();
        mat.AlbedoColor = new Color(0, 0, 0, 0.30f);
        mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        var cyl = new CylinderMesh();
        cyl.TopRadius = r; cyl.BottomRadius = r; cyl.Height = 0.01f;
        cyl.SurfaceSetMaterial(0, mat);
        _shadow.Mesh = cyl;
        _shadow.Position = new Vector3(0, 0.01f, 0);
        AddChild(_shadow);
    }

    // ═══════════════════════════════════════════════════════════════════
    // ANIMATION
    // ═══════════════════════════════════════════════════════════════════
    private void AnimateAnimal(double delta)
    {
        if (_owner.IsDead) return;

        bool fleeing = _owner.IsFleeing; // need to expose this
        float speed  = fleeing ? 3.0f : 1.0f;
        float t      = (float)_animTimer;

        switch (_owner.Type)
        {
            case AnimalType.Deer:   AnimateDeer(t, speed, fleeing);   break;
            case AnimalType.Boar:   AnimateBoar(t, speed, fleeing);   break;
            case AnimalType.Rabbit: AnimateRabbit(t, speed, fleeing); break;
        }

        // Face movement direction
        if (_owner.Velocity.LengthSquared() > 0.01f)
        {
            var vel = _owner.Velocity; vel.Y = 0;
            if (vel.LengthSquared() > 0.001f)
            {
                float targetYaw = Mathf.Atan2(vel.X, vel.Z);
                float curYaw    = Rotation.Y;
                Rotation = new Vector3(0, Mathf.LerpAngle(curYaw, targetYaw, 0.15f), 0);
            }
        }
    }

    private void AnimateDeer(float t, float speed, bool fleeing)
    {
        // Leg swing
        float swing = Mathf.Sin(t * speed * 4f) * 20f;
        if (_legFL != null) _legFL.RotationDegrees = new Vector3( swing, 0, 0);
        if (_legFR != null) _legFR.RotationDegrees = new Vector3(-swing, 0, 0);
        if (_legBL != null) _legBL.RotationDegrees = new Vector3(-swing, 0, 0);
        if (_legBR != null) _legBR.RotationDegrees = new Vector3( swing, 0, 0);

        // Body bob
        float bob = Mathf.Sin(t * speed * 4f) * 0.02f;
        if (_body != null) _body.Position = new Vector3(0, bob, 0);

        // Head bob (grazing when idle)
        if (!fleeing && _neck != null)
        {
            float graze = Mathf.Sin(t * 0.5f) * 8f - 30f;
            _neck.RotationDegrees = new Vector3(graze, 0, 0);
        }
        else if (_neck != null)
        {
            _neck.RotationDegrees = new Vector3(-10f, 0, 0); // head up when fleeing
        }

        // Tail flick
        if (_tail != null)
            _tail.RotationDegrees = new Vector3(0, Mathf.Sin(t * 2f) * 15f, 0);
    }

    private void AnimateBoar(float t, float speed, bool fleeing)
    {
        // Heavier stamp — less swing, more vertical
        float swing = Mathf.Sin(t * speed * 5f) * 15f;
        if (_legFL != null) _legFL.RotationDegrees = new Vector3( swing, 0, 0);
        if (_legFR != null) _legFR.RotationDegrees = new Vector3(-swing, 0, 0);
        if (_legBL != null) _legBL.RotationDegrees = new Vector3(-swing, 0, 0);
        if (_legBR != null) _legBR.RotationDegrees = new Vector3( swing, 0, 0);

        // Stamping body drop
        float stamp = Mathf.Abs(Mathf.Sin(t * speed * 5f)) * -0.03f;
        if (_body != null) _body.Position = new Vector3(0, stamp + 0.05f, 0);

        // Head low/aggressive when fleeing
        if (_head != null)
            _head.RotationDegrees = new Vector3(fleeing ? 15f : 0f, 0, 0);

        // Tail wag
        if (_tail != null)
            _tail.RotationDegrees = new Vector3(Mathf.Sin(t * 3f) * 20f, 0, 0);
    }

    private void AnimateRabbit(float t, float speed, bool fleeing)
    {
        if (fleeing)
        {
            // Hop: compress and extend body
            float hop  = Mathf.Abs(Mathf.Sin(t * speed * 8f));
            float sqsh = 1f - hop * 0.3f;
            if (_body != null) _body.Scale = new Vector3(1f + hop * 0.1f, sqsh, 1f + hop * 0.2f);
            if (_body != null) _body.Position = new Vector3(0, hop * 0.12f, 0);

            // All legs push back during hop
            float legPush = Mathf.Sin(t * speed * 8f) * 25f;
            if (_legFL != null) _legFL.RotationDegrees = new Vector3(-legPush, 0, 0);
            if (_legFR != null) _legFR.RotationDegrees = new Vector3(-legPush, 0, 0);
            if (_legBL != null) _legBL.RotationDegrees = new Vector3( legPush * 1.5f, 0, 0);
            if (_legBR != null) _legBR.RotationDegrees = new Vector3( legPush * 1.5f, 0, 0);
        }
        else
        {
            // Idle: gentle breathing + ear twitch
            if (_body != null) _body.Scale = Vector3.One;
            float breath = Mathf.Sin(t * 1.5f) * 0.015f;
            if (_body != null) _body.Position = new Vector3(0, 0.22f + breath, 0);

            // Ear twitch
            if (_earL != null) _earL.RotationDegrees = new Vector3(0, 0, Mathf.Sin(t * 0.7f) * 12f);
            if (_earR != null) _earR.RotationDegrees = new Vector3(0, 0, Mathf.Sin(t * 0.7f + 1f) * -12f);

            // Nose twitch (head micro-rotation)
            if (_head != null) _head.RotationDegrees = new Vector3(Mathf.Sin(t * 2.1f) * 4f, 0, 0);
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // MESH HELPERS
    // ═══════════════════════════════════════════════════════════════════
    private Node3D MakeLeg(Node3D parent, Vector3 pos, Color color, float length)
    {
        var pivot = MakeNode("Leg", pos);
        parent.AddChild(pivot);
        // Upper segment
        AddMesh(pivot, MakeBox(0.07f, length * 0.55f, 0.07f), color,
            new Vector3(0, -length * 0.27f, 0));
        // Lower segment (shin) — offset from pivot
        var shin = MakeNode("Shin", new Vector3(0, -length * 0.55f, 0));
        pivot.AddChild(shin);
        AddMesh(shin, MakeBox(0.05f, length * 0.50f, 0.05f), color,
            new Vector3(0, -length * 0.25f, 0));
        return pivot;
    }

    private static Node3D MakeNode(string name, Vector3 pos)
    {
        var n = new Node3D(); n.Name = name; n.Position = pos; return n;
    }

    private static void AddMesh(Node3D parent, Mesh mesh, Color color, Vector3 offset)
    {
        var mi = new MeshInstance3D();
        var mat = new StandardMaterial3D();
        mat.AlbedoColor = color;
        mesh.SurfaceSetMaterial(0, mat);
        mi.Mesh = mesh;
        mi.Position = offset;
        parent.AddChild(mi);
    }

    private static BoxMesh MakeBox(float x, float y, float z)
    { var m = new BoxMesh(); m.Size = new Vector3(x, y, z); return m; }

    private static SphereMesh MakeSphere(float r)
    { var m = new SphereMesh(); m.Radius = r; m.Height = r * 2f; return m; }

    private static CylinderMesh MakeCylinder(float r, float h)
    { var m = new CylinderMesh(); m.TopRadius = r; m.BottomRadius = r; m.Height = h; return m; }
}
