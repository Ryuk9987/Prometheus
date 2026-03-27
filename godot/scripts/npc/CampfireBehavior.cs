#nullable disable
using Godot;

/// <summary>
/// CampfireBehavior — NPCs tend (refuel) existing campfires.
/// Building campfires is exclusively handled by LeaderBehavior via BuildOrders.
/// </summary>
public partial class CampfireBehavior : Node
{
    private enum BState { Idle, SeekWood, CarryWood, Tend }

    private NpcEntity _owner;
    private BState    _state       = BState.Idle;
    private double    _idleTimer   = 0;
    private float     _carriedWood = 0f;
    private Campfire  _targetFire  = null;

    private const double IdleCheckInterval = 10.0;
    private const float  WorkRange         = 1.8f;
    private const float  MoveSpeed         = 0.9f;
    private const float  WoodNeeded        = 3f;

    public bool IsActive => _state != BState.Idle;

    public override void _Ready() => _owner = GetParent<NpcEntity>();

    public bool Tick(double delta)
    {
        if (!_owner.Knowledge.Knows("fire")) return false;
        if (_owner.Knowledge.Knowledge["fire"].Depth < 0.15f) return false;

        switch (_state)
        {
            case BState.Idle:
                _idleTimer += delta;
                if (_idleTimer < IdleCheckInterval) return false;
                _idleTimer = 0;
                DecideTend();
                return false;

            case BState.SeekWood:  return TickSeekWood(delta);
            case BState.CarryWood: return TickCarryWood(delta);
            case BState.Tend:      return TickTend(delta);
        }
        return false;
    }

    private void DecideTend()
    {
        var fire = CampfireManager.Instance?.FindNearestNeedingFuel(_owner.GlobalPosition, 30f);
        if (fire == null) return;
        _targetFire = fire;
        _state = BState.SeekWood;
        GD.Print($"[Campfire] {_owner.NpcName}: tends fire.");
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
                _state = BState.CarryWood;
        }
        return true;
    }

    private bool TickCarryWood(double delta)
    {
        if (_targetFire == null || !IsInstanceValid(_targetFire))
            { _state = BState.Idle; _carriedWood = 0f; return false; }

        if (MoveTo(_owner.GlobalPosition, _targetFire.GlobalPosition, delta, WorkRange))
            _state = BState.Tend;
        return true;
    }

    private bool TickTend(double delta)
    {
        if (_targetFire == null || !IsInstanceValid(_targetFire))
            { _state = BState.Idle; _carriedWood = 0f; _targetFire = null; return false; }

        _targetFire.AddFuel(_carriedWood);
        GD.Print($"[Campfire] {_owner.NpcName} deposited wood. Fuel: {_targetFire.Fuel:F1}");
        if (_targetFire.Fuel >= 3f)
        {
            _targetFire.Light();
            _owner.Knowledge.Verify("fire", 0.05f);
        }
        _carriedWood = 0f;
        _targetFire  = null;
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
