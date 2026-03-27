#nullable disable
using Godot;

/// <summary>
/// AnimalVisuals — redesigned low-poly animal meshes.
/// Tuned for top-down/isometric camera (30-45° angle).
///
/// Coordinate system: Animal.Position.Y = 0 (ground level).
/// All geometry is built with positive Y above ground.
///
/// Deer  — total height ~1.4u, slender body, long neck, antlers
/// Boar  — total height ~0.9u, wide powerful head, tusks, stocky
/// Rabbit — total height ~0.55u, upright body, tall ears
/// </summary>
public partial class AnimalVisuals : Node3D
{
    private Animal _owner;
    private double _animTimer = 0;

    // Articulated nodes for animation
    private Node3D _body;
    private Node3D _neck;
    private Node3D _head;
    private Node3D _tail;
    private Node3D _legFL, _legFR, _legBL, _legBR;
    private Node3D _earL, _earR;

    public override void _Ready()
    {
        _owner = GetParent<Animal>();
        switch (_owner.Type)
        {
            case AnimalType.Deer:   BuildDeer();   break;
            case AnimalType.Boar:   BuildBoar();   break;
            case AnimalType.Rabbit: BuildRabbit(); break;
        }
        BuildShadow();
    }

    public override void _Process(double delta)
    {
        if (_owner.IsDead) return;
        _animTimer += delta;
        Animate();
        FaceVelocity();
    }

    // ═══════════════════════════════════════════════════════════════════
    // DEER — slender, tall, graceful
    // ═══════════════════════════════════════════════════════════════════
    private void BuildDeer()
    {
        // Proportions (ground = Y 0):
        //   Leg bottom:   Y = 0.00
        //   Leg top:      Y = 0.60   (leg length = 0.60)
        //   Body center:  Y = 0.75   (body height = 0.30, bottom at 0.60)
        //   Neck base:    Y = 0.90
        //   Head center:  Y = 1.20

        var brown  = new Color(0.62f, 0.43f, 0.22f);
        var light  = new Color(0.82f, 0.68f, 0.48f);
        var dark   = new Color(0.32f, 0.20f, 0.10f);
        var white  = new Color(0.96f, 0.94f, 0.88f);

        // ── Body (elongated, slim)
        _body = NewNode("Body", new Vector3(0, 0.75f, 0));
        AddChild(_body);
        Mesh(_body, Box(0.28f, 0.30f, 0.70f), brown);
        Mesh(_body, Box(0.16f, 0.06f, 0.50f), light, new Vector3(0, -0.12f, 0)); // belly

        // ── Neck (angled forward-up)
        _neck = NewNode("Neck", new Vector3(0, 0.90f, 0.28f));
        _body.GetParent().CallDeferred(Node.MethodName.AddChild, _neck); // sibling of body
        AddChild(_neck);
        Mesh(_neck, Box(0.12f, 0.38f, 0.12f), brown);
        _neck.RotationDegrees = new Vector3(-38f, 0, 0);

        // ── Head
        _head = NewNode("Head", new Vector3(0, 0.30f, 0.05f));
        _neck.AddChild(_head);
        Mesh(_head, Box(0.18f, 0.16f, 0.26f), brown);
        Mesh(_head, Box(0.10f, 0.09f, 0.16f), light, new Vector3(0, -0.04f, 0.17f)); // snout
        Mesh(_head, Box(0.004f, 0.04f, 0.04f), new Color(0,0,0), new Vector3( 0.08f, 0.03f, 0.12f)); // eye L
        Mesh(_head, Box(0.004f, 0.04f, 0.04f), new Color(0,0,0), new Vector3(-0.08f, 0.03f, 0.12f)); // eye R
        // Ears (small, pointed)
        Mesh(_head, Box(0.04f, 0.10f, 0.03f), brown, new Vector3( 0.10f, 0.10f, -0.02f));
        Mesh(_head, Box(0.04f, 0.10f, 0.03f), brown, new Vector3(-0.10f, 0.10f, -0.02f));

        // ── Antlers (Y-fork shape)
        var antL = NewNode("AntlerL", new Vector3( 0.06f, 0.09f, 0));
        _head.AddChild(antL);
        Mesh(antL, Box(0.025f, 0.22f, 0.025f), dark);                          // main
        Mesh(antL, Box(0.14f, 0.025f, 0.025f), dark, new Vector3(0, 0.10f, 0)); // fork
        var antR = NewNode("AntlerR", new Vector3(-0.06f, 0.09f, 0));
        _head.AddChild(antR);
        Mesh(antR, Box(0.025f, 0.22f, 0.025f), dark);
        Mesh(antR, Box(0.14f, 0.025f, 0.025f), dark, new Vector3(0, 0.10f, 0));

        // ── Tail
        _tail = NewNode("Tail", new Vector3(0, 0.78f, -0.36f));
        AddChild(_tail);
        Mesh(_tail, Sphere(0.06f), white);

        // ── Legs (pivot at body bottom = Y 0.60, leg length 0.60)
        float legY = 0.60f;
        _legFL = MakeLeg(new Vector3( 0.12f, legY,  0.24f), dark, 0.60f);
        _legFR = MakeLeg(new Vector3(-0.12f, legY,  0.24f), dark, 0.60f);
        _legBL = MakeLeg(new Vector3( 0.12f, legY, -0.24f), dark, 0.60f);
        _legBR = MakeLeg(new Vector3(-0.12f, legY, -0.24f), dark, 0.60f);
    }

    // ═══════════════════════════════════════════════════════════════════
    // BOAR — stocky, aggressive, powerful
    // ═══════════════════════════════════════════════════════════════════
    private void BuildBoar()
    {
        // Proportions:
        //   Leg bottom: Y = 0.00
        //   Leg top:    Y = 0.36  (leg length = 0.36, short stocky)
        //   Body center: Y = 0.52  (body height = 0.32, bottom at 0.36)
        //   Head forward from body at same height

        var darkBrown  = new Color(0.28f, 0.22f, 0.16f);
        var midBrown   = new Color(0.40f, 0.32f, 0.22f);
        var bristle    = new Color(0.18f, 0.14f, 0.10f);
        var tusk       = new Color(0.94f, 0.90f, 0.78f);
        var eyeColor   = new Color(0.05f, 0.05f, 0.05f);

        // ── Body (barrel-shaped)
        _body = NewNode("Body", new Vector3(0, 0.52f, 0));
        AddChild(_body);
        Mesh(_body, Box(0.44f, 0.32f, 0.62f), midBrown);
        // Bristle mohawk on back
        Mesh(_body, Box(0.08f, 0.12f, 0.55f), bristle, new Vector3(0, 0.22f, 0));
        // Belly (lighter)
        Mesh(_body, Box(0.28f, 0.05f, 0.45f), new Color(0.52f, 0.42f, 0.30f), new Vector3(0,-0.15f,0));

        // ── Head (large, low, menacing)
        _head = NewNode("Head", new Vector3(0, 0.52f, 0.40f));
        AddChild(_head);
        Mesh(_head, Box(0.38f, 0.30f, 0.42f), midBrown);
        // Heavy brow ridge
        Mesh(_head, Box(0.34f, 0.06f, 0.10f), darkBrown, new Vector3(0, 0.14f, 0.18f));
        // Snout (disc)
        Mesh(_head, Box(0.22f, 0.16f, 0.14f), midBrown, new Vector3(0,-0.03f, 0.23f));
        Mesh(_head, Sphere(0.06f), darkBrown, new Vector3(0,-0.04f, 0.30f)); // nose tip
        Mesh(_head, Sphere(0.025f), bristle, new Vector3( 0.06f,-0.04f, 0.30f)); // nostril L
        Mesh(_head, Sphere(0.025f), bristle, new Vector3(-0.06f,-0.04f, 0.30f)); // nostril R
        // Eyes (small, deep-set)
        Mesh(_head, Sphere(0.035f), eyeColor, new Vector3( 0.16f, 0.08f, 0.17f));
        Mesh(_head, Sphere(0.035f), eyeColor, new Vector3(-0.16f, 0.08f, 0.17f));
        // Tusks (angled outward and up)
        var tuskL = NewNode("TuskL", new Vector3( 0.14f,-0.10f, 0.20f));
        _head.AddChild(tuskL);
        Mesh(tuskL, Box(0.04f, 0.05f, 0.16f), tusk);
        tuskL.RotationDegrees = new Vector3(-15f, 20f, 0);
        var tuskR = NewNode("TuskR", new Vector3(-0.14f,-0.10f, 0.20f));
        _head.AddChild(tuskR);
        Mesh(tuskR, Box(0.04f, 0.05f, 0.16f), tusk);
        tuskR.RotationDegrees = new Vector3(-15f,-20f, 0);
        // Ears (stubby, pinned back)
        Mesh(_head, Box(0.08f, 0.10f, 0.05f), darkBrown, new Vector3( 0.18f, 0.15f,-0.05f));
        Mesh(_head, Box(0.08f, 0.10f, 0.05f), darkBrown, new Vector3(-0.18f, 0.15f,-0.05f));

        // ── Tail (curly — just a small twisted cylinder)
        _tail = NewNode("Tail", new Vector3(0, 0.55f,-0.32f));
        AddChild(_tail);
        Mesh(_tail, Sphere(0.05f), darkBrown);

        // ── Short stocky legs
        float legY = 0.36f;
        _legFL = MakeLeg(new Vector3( 0.17f, legY,  0.20f), darkBrown, 0.36f);
        _legFR = MakeLeg(new Vector3(-0.17f, legY,  0.20f), darkBrown, 0.36f);
        _legBL = MakeLeg(new Vector3( 0.17f, legY, -0.20f), darkBrown, 0.36f);
        _legBR = MakeLeg(new Vector3(-0.17f, legY, -0.20f), darkBrown, 0.36f);
    }

    // ═══════════════════════════════════════════════════════════════════
    // RABBIT — small, upright, long ears pointing straight up
    // ═══════════════════════════════════════════════════════════════════
    private void BuildRabbit()
    {
        // Proportions:
        //   Tiny legs: Y 0–0.16
        //   Body center: Y 0.26
        //   Head: Y 0.42
        //   Ear tips: Y ~0.75

        var fur   = new Color(0.82f, 0.76f, 0.64f);
        var belly = new Color(0.96f, 0.94f, 0.88f);
        var inner = new Color(0.90f, 0.60f, 0.62f); // pink ear inside
        var dark  = new Color(0.40f, 0.32f, 0.22f);
        var eye   = new Color(0.55f, 0.10f, 0.12f); // red eyes

        // ── Body (egg-shaped: wider at bottom)
        _body = NewNode("Body", new Vector3(0, 0.26f, 0));
        AddChild(_body);
        Mesh(_body, Box(0.20f, 0.22f, 0.24f), fur);
        Mesh(_body, Box(0.12f, 0.14f, 0.16f), belly, new Vector3(0, 0, 0.05f)); // belly patch

        // ── Head (round, on top of body, slightly forward)
        _head = NewNode("Head", new Vector3(0, 0.42f, 0.08f));
        AddChild(_head);
        Mesh(_head, Sphere(0.13f), fur);
        Mesh(_head, Sphere(0.07f), belly, new Vector3(0,-0.03f, 0.08f)); // cheek/muzzle
        Mesh(_head, Sphere(0.022f), new Color(0.8f,0.3f,0.4f), new Vector3(0,-0.02f, 0.14f)); // nose
        Mesh(_head, Sphere(0.030f), eye, new Vector3( 0.07f, 0.03f, 0.10f));
        Mesh(_head, Sphere(0.030f), eye, new Vector3(-0.07f, 0.03f, 0.10f));

        // ── Ears — TALL, pointing straight up from head
        // Ear pivot at top of head
        _earL = NewNode("EarL", new Vector3( 0.045f, 0.12f, -0.01f));
        _head.AddChild(_earL);
        Mesh(_earL, Box(0.05f, 0.26f, 0.03f), fur,   new Vector3(0, 0.13f, 0));
        Mesh(_earL, Box(0.03f, 0.20f, 0.015f), inner, new Vector3(0, 0.13f, 0.01f));

        _earR = NewNode("EarR", new Vector3(-0.045f, 0.12f, -0.01f));
        _head.AddChild(_earR);
        Mesh(_earR, Box(0.05f, 0.26f, 0.03f), fur,   new Vector3(0, 0.13f, 0));
        Mesh(_earR, Box(0.03f, 0.20f, 0.015f), inner, new Vector3(0, 0.13f, 0.01f));

        // ── Fluffy tail (back, white)
        _tail = NewNode("Tail", new Vector3(0, 0.28f,-0.13f));
        AddChild(_tail);
        Mesh(_tail, Sphere(0.07f), belly);

        // ── Tiny legs
        float legY = 0.16f;
        _legFL = MakeLeg(new Vector3( 0.08f, legY,  0.08f), dark, 0.16f);
        _legFR = MakeLeg(new Vector3(-0.08f, legY,  0.08f), dark, 0.16f);
        _legBL = MakeLeg(new Vector3( 0.09f, legY, -0.06f), dark, 0.20f); // longer hind legs
        _legBR = MakeLeg(new Vector3(-0.09f, legY, -0.06f), dark, 0.20f);
    }

    // ═══════════════════════════════════════════════════════════════════
    // SHADOW
    // ═══════════════════════════════════════════════════════════════════
    private void BuildShadow()
    {
        float r = _owner.Type switch {
            AnimalType.Deer   => 0.28f,
            AnimalType.Boar   => 0.26f,
            AnimalType.Rabbit => 0.12f,
            _ => 0.20f
        };
        var mi = new MeshInstance3D();
        var mat = new StandardMaterial3D();
        mat.AlbedoColor = new Color(0, 0, 0, 0.28f);
        mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        mat.ShadingMode  = BaseMaterial3D.ShadingModeEnum.Unshaded;
        var cyl = new CylinderMesh();
        cyl.TopRadius = r; cyl.BottomRadius = r * 1.3f; cyl.Height = 0.01f;
        cyl.SurfaceSetMaterial(0, mat);
        mi.Mesh = cyl;
        mi.Position = new Vector3(0, 0.005f, 0);
        AddChild(mi);
    }

    // ═══════════════════════════════════════════════════════════════════
    // ANIMATION
    // ═══════════════════════════════════════════════════════════════════
    private void Animate()
    {
        float t     = (float)_animTimer;
        bool  flee  = _owner.IsFleeing;
        float speed = flee ? 2.8f : (HasVelocity() ? 1.0f : 0f);

        switch (_owner.Type)
        {
            case AnimalType.Deer:   AnimDeer(t, speed, flee);   break;
            case AnimalType.Boar:   AnimBoar(t, speed, flee);   break;
            case AnimalType.Rabbit: AnimRabbit(t, speed, flee); break;
        }
    }

    private void AnimDeer(float t, float speed, bool flee)
    {
        bool moving = speed > 0.1f;
        float swing  = moving ? Mathf.Sin(t * speed * 3.5f) * 22f : 0f;
        float bob    = moving ? Mathf.Abs(Mathf.Sin(t * speed * 3.5f)) * 0.03f : 0f;

        SetLegAngle(_legFL,  swing); SetLegAngle(_legBR,  swing);
        SetLegAngle(_legFR, -swing); SetLegAngle(_legBL, -swing);

        if (_body != null) _body.Position = new Vector3(0, 0.75f + bob, 0);

        // Head: grazing idle, alert when moving/fleeing
        if (_neck != null)
        {
            float neckPitch = flee ? -20f : (moving ? -32f : -38f + Mathf.Sin(t*0.4f)*8f);
            _neck.RotationDegrees = new Vector3(neckPitch, 0, 0);
        }

        if (_tail != null)
            _tail.RotationDegrees = new Vector3(0, Mathf.Sin(t*1.8f)*14f, 0);
    }

    private void AnimBoar(float t, float speed, bool flee)
    {
        bool moving = speed > 0.1f;
        float swing  = moving ? Mathf.Sin(t * speed * 4.0f) * 16f : 0f;
        // Stamp: body dips with each step
        float stamp  = moving ? Mathf.Abs(Mathf.Sin(t * speed * 4.0f)) * -0.025f : 0f;

        SetLegAngle(_legFL,  swing); SetLegAngle(_legBR,  swing);
        SetLegAngle(_legFR, -swing); SetLegAngle(_legBL, -swing);

        if (_body != null) _body.Position = new Vector3(0, 0.52f + stamp, 0);
        if (_head != null)
        {
            // Head low and aggressive when fleeing
            _head.RotationDegrees = new Vector3(flee ? 12f : 0f, 0, 0);
            // Sync head with body stamp
            _head.Position = new Vector3(0, 0.52f + stamp, 0.40f);
        }
        if (_tail != null)
            _tail.RotationDegrees = new Vector3(Mathf.Sin(t*2.5f)*18f, 0, 0);
    }

    private void AnimRabbit(float t, float speed, bool flee)
    {
        if (flee)
        {
            // Hop — full body squash & stretch
            float phase = t * 7.0f;
            float hop   = Mathf.Max(0f, Mathf.Sin(phase));
            float squat = 1f - hop * 0.25f;
            if (_body != null)
            {
                _body.Scale    = new Vector3(1f + hop*0.10f, squat, 1f + hop*0.18f);
                _body.Position = new Vector3(0, 0.26f + hop*0.14f, 0);
            }
            if (_head != null)
                _head.Position = new Vector3(0, 0.42f + hop*0.10f, 0.08f);

            // All legs push back during hop
            float push = Mathf.Sin(phase) * 28f;
            SetLegAngle(_legFL, -push * 0.6f);
            SetLegAngle(_legFR, -push * 0.6f);
            SetLegAngle(_legBL,  push * 1.2f);
            SetLegAngle(_legBR,  push * 1.2f);
        }
        else
        {
            // Idle: reset scale, gentle breathing
            if (_body != null)
            {
                _body.Scale    = Vector3.One;
                float breath   = Mathf.Sin(t * 1.8f) * 0.008f;
                _body.Position = new Vector3(0, 0.26f + breath, 0);
            }
            if (_head != null)
                _head.Position = new Vector3(0, 0.42f, 0.08f);

            SetLegAngle(_legFL, 0); SetLegAngle(_legFR, 0);
            SetLegAngle(_legBL, 0); SetLegAngle(_legBR, 0);

            // Ear twitch (independent left/right)
            if (_earL != null) _earL.RotationDegrees = new Vector3(0, 0,  Mathf.Sin(t * 0.9f) * 10f);
            if (_earR != null) _earR.RotationDegrees = new Vector3(0, 0, -Mathf.Sin(t * 0.7f + 0.8f) * 10f);
            // Nose twitch
            if (_head != null) _head.RotationDegrees = new Vector3(Mathf.Sin(t * 2.3f) * 3f, 0, 0);
        }
    }

    private void FaceVelocity()
    {
        if (!HasVelocity()) return;
        var vel = _owner.Velocity; vel.Y = 0;
        if (vel.LengthSquared() < 0.001f) return;
        float targetYaw = Mathf.Atan2(vel.X, vel.Z);
        Rotation = new Vector3(0, Mathf.LerpAngle(Rotation.Y, targetYaw, 0.12f), 0);
    }

    private bool HasVelocity() => _owner.Velocity.LengthSquared() > 0.01f;

    // ═══════════════════════════════════════════════════════════════════
    // LEG BUILDER
    // pivot = top of leg (at body-bottom height), leg hangs downward
    // ═══════════════════════════════════════════════════════════════════
    private Node3D MakeLeg(Vector3 pivotPos, Color color, float totalLen)
    {
        float upper = totalLen * 0.52f;
        float lower = totalLen * 0.52f;

        var pivot = NewNode("Leg", pivotPos);
        AddChild(pivot);

        // Thigh
        Mesh(pivot, Box(0.07f, upper, 0.07f), color, new Vector3(0, -upper * 0.5f, 0));

        // Shin (below thigh)
        var shin = NewNode("Shin", new Vector3(0, -upper, 0));
        pivot.AddChild(shin);
        Mesh(shin, Box(0.055f, lower, 0.055f), color, new Vector3(0, -lower * 0.5f, 0));

        // Hoof/paw
        var hoof = _owner.Type == AnimalType.Rabbit ? new Color(0.3f,0.22f,0.14f) : new Color(0.18f,0.14f,0.10f);
        Mesh(shin, Box(0.08f, 0.04f, 0.09f), hoof, new Vector3(0, -lower, 0));

        return pivot;
    }

    private static void SetLegAngle(Node3D leg, float angleDeg)
    {
        if (leg != null) leg.RotationDegrees = new Vector3(angleDeg, 0, 0);
    }

    // ═══════════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════════
    private static Node3D NewNode(string name, Vector3 pos)
    { var n = new Node3D(); n.Name = name; n.Position = pos; return n; }

    private static void Mesh(Node3D parent, Mesh mesh, Color color, Vector3 offset = default)
    {
        var mi  = new MeshInstance3D();
        var mat = new StandardMaterial3D();
        mat.AlbedoColor = color;
        mesh.SurfaceSetMaterial(0, mat);
        mi.Mesh     = mesh;
        mi.Position = offset;
        parent.AddChild(mi);
    }

    private static BoxMesh Box(float x, float y, float z)
    { var m = new BoxMesh(); m.Size = new Vector3(x, y, z); return m; }

    private static SphereMesh Sphere(float r)
    { var m = new SphereMesh(); m.Radius = r; m.Height = r * 2f;
      m.RadialSegments = 8; m.Rings = 4; return m; }

    private static CylinderMesh Cylinder(float r, float h)
    { var m = new CylinderMesh(); m.TopRadius = r; m.BottomRadius = r; m.Height = h; return m; }
}
