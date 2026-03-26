#nullable disable
using Godot;

/// <summary>
/// NPC behavior for building and using campfires.
/// Triggered when NPC knows "fire" with sufficient depth.
/// 
/// States:
///   SeekWood    → find nearest branch resource
///   CarryWood   → carry to campfire site (or create new one)
///   Build       → deposit wood, build pile
///   Light       → light the fire (needs courage)
///   Tend        → add fuel to existing fires
/// </summary>
public partial class CampfireBehavior : Node
{
    private enum BState { Idle, SeekWood, CarryWood, Build, Light, Tend }

    private NpcEntity  _owner;
    private BState     _state      = BState.Idle;
    private double     _idleTimer  = 0;
    private float      _carriedWood = 0f;
    private Vector3    _buildSite;
    private Campfire   _targetFire = null;
    private bool       _hasSite    = false;

    private const double IdleCheckInterval = 6.0;
    private const float  WorkRange         = 1.8f;
    private const float  MoveSpeed         = 2.8f;
    private const float  WoodNeeded        = 3f;

    // Build style derived from oracle composition
    private bool _withStoneRing = false;

    public bool IsActive => _state != BState.Idle;

    public override void _Ready()
    {
        _owner = GetParent<NpcEntity>();
    }

    /// <summary>Called each frame. Returns true if taking over movement.</summary>
    public bool Tick(double delta)
    {
        // Only active if NPC knows fire well enough
        if (!_owner.Knowledge.Knows("fire")) return false;
        // Only unassigned, builders, or leaders spontaneously tend/build fire
        var role = _owner.SocialRole;
        if (role != SocialRole.Unassigned && role != SocialRole.Builder &&
            role != SocialRole.Leader) return false;
        var fireKnowledge = _owner.Knowledge.Knowledge["fire"];
        if (fireKnowledge.Depth < 0.15f) return false;

        switch (_state)
        {
            case BState.Idle:
                _idleTimer += delta;
                if (_idleTimer < IdleCheckInterval) return false;
                _idleTimer = 0;
                DecideNextAction();
                return false;

            case BState.SeekWood:
                return TickSeekWood(delta);

            case BState.CarryWood:
                return TickCarryWood(delta);

            case BState.Build:
                return TickBuild(delta);

            case BState.Light:
                return TickLight(delta);

            case BState.Tend:
                return TickTend(delta);
        }
        return false;
    }

    private void DecideNextAction()
    {
        // Read oracle composition to determine build style
        var comp = OracleTablet.Instance?.LastComposition;
        _withStoneRing = comp != null && comp.StampCounts.ContainsKey("stone");

        // Tend existing fire if low fuel
        var nearFire = CampfireManager.Instance?.FindNearestNeedingFuel(_owner.GlobalPosition);
        if (nearFire != null && _owner.GlobalPosition.DistanceTo(nearFire.GlobalPosition) < 25f)
        {
            _targetFire = nearFire;
            _state = BState.SeekWood;
            _hasSite = true;
            _buildSite = nearFire.GlobalPosition;
            GD.Print($"[Campfire] {_owner.NpcName}: tends fire.");
            return;
        }

        // Build new fire if none nearby
        var anyFire = CampfireManager.Instance?.FindNearest(_owner.GlobalPosition, 20f);
        if (anyFire == null)
        {
            _state = BState.SeekWood;
            _hasSite = false;
            string style = _withStoneRing ? "mit Steinkranz" : "nur Asthaufen";
            GD.Print($"[Campfire] {_owner.NpcName}: baut Lagerfeuer ({style}).");
        }
    }

    private bool TickSeekWood(double delta)
    {
        var wood = ResourceManager.Instance?.FindNearest(_owner.GlobalPosition, ResourceType.Wood);
        if (wood == null) { _state = BState.Idle; return false; }

        if (MoveTo(_owner.GlobalPosition, wood.GlobalPosition, delta, WorkRange))
        {
            float got = wood.Harvest(1.5f);
            _carriedWood += got;
            GD.Print($"[Campfire] {_owner.NpcName} picked up wood ({_carriedWood:F1}/{WoodNeeded})");
            if (_carriedWood >= WoodNeeded)
            {
                // Choose or create build site
                if (!_hasSite)
                {
                    _buildSite = _owner.GlobalPosition + new Vector3(
                        (float)GD.RandRange(-3.0, 3.0), 0, (float)GD.RandRange(-3.0, 3.0));
                    _hasSite = true;
                }
                _state = BState.CarryWood;
            }
        }
        return true;
    }

    private bool TickCarryWood(double delta)
    {
        if (MoveTo(_owner.GlobalPosition, _buildSite, delta, WorkRange))
        {
            _state = BState.Build;
        }
        return true;
    }

    private bool TickBuild(double delta)
    {
        if (_targetFire == null)
        {
            var fire = new Campfire();
            fire.WithStoneRing = _withStoneRing;
            fire.Position = _buildSite;
            _owner.GetParent().AddChild(fire);
            _targetFire = fire;
        }

        _targetFire.AddFuel(_carriedWood);
        _carriedWood = 0f;
        GD.Print($"[Campfire] {_owner.NpcName} deposited wood. Fuel: {_targetFire.Fuel:F1}");

        _state = _targetFire.Fuel >= 3f ? BState.Light : BState.Idle;
        return false;
    }

    private bool TickLight(double delta)
    {
        if (_targetFire == null) { _state = BState.Idle; return false; }

        if (MoveTo(_owner.GlobalPosition, _targetFire.GlobalPosition, delta, WorkRange))
        {
            _targetFire.Light();
            _state = BState.Idle;
            _targetFire = null;
            // Lighting fire reinforces fire knowledge
            _owner.Knowledge.Verify("fire", 0.25f);
        }
        return true;
    }

    private bool TickTend(double delta)
    {
        if (_targetFire == null) { _state = BState.Idle; return false; }
        _targetFire.AddFuel(_carriedWood);
        _carriedWood = 0;
        _state = BState.Idle;
        return false;
    }

    private bool MoveTo(Vector3 from, Vector3 to, double delta, float range)
    {
        var dir = to - from; dir.Y = 0;
        if (dir.Length() <= range) return true;
        _owner.GlobalPosition += dir.Normalized() * MoveSpeed * (float)delta;
        return false;
    }
}
