#nullable disable
using Godot;
using System.Linq;

/// <summary>
/// NPC behavior for campfire management.
/// 
/// Building: places a BuildOrder for "campfire" — BuildWorkerBehavior handles
///           the actual construction. CampfireManager tracks pending sites so
///           only one campfire is initiated per 20m radius.
/// 
/// Tending:  NPCs with fire knowledge add fuel to nearby low-fuel fires.
/// </summary>
public partial class CampfireBehavior : Node
{
    private enum BState { Idle, SeekWood, CarryWood, Tend }

    private NpcEntity _owner;
    private BState    _state     = BState.Idle;
    private double    _idleTimer = 0;
    private float     _carriedWood = 0f;
    private Campfire  _targetFire  = null;

    private const double IdleCheckInterval = 8.0;
    private const float  WorkRange         = 1.8f;
    private const float  MoveSpeed         = 2.8f;
    private const float  WoodNeeded        = 3f;

    public bool IsActive => _state != BState.Idle;

    public override void _Ready() => _owner = GetParent<NpcEntity>();

    public bool Tick(double delta)
    {
        if (!_owner.Knowledge.Knows("fire")) return false;
        var role = _owner.SocialRole;
        if (role != SocialRole.Unassigned && role != SocialRole.Builder &&
            role != SocialRole.Leader) return false;
        if (_owner.Knowledge.Knowledge["fire"].Depth < 0.15f) return false;

        switch (_state)
        {
            case BState.Idle:
                _idleTimer += delta;
                if (_idleTimer < IdleCheckInterval) return false;
                _idleTimer = 0;
                DecideNextAction();
                return false;

            case BState.SeekWood:  return TickSeekWood(delta);
            case BState.CarryWood: return TickCarryWood(delta);
            case BState.Tend:      return TickTend(delta);
        }
        return false;
    }

    private void DecideNextAction()
    {
        // ── Tend: fuel up a nearby low-fuel fire ──────────────────────────
        var nearFire = CampfireManager.Instance?.FindNearestNeedingFuel(_owner.GlobalPosition, 25f);
        if (nearFire != null)
        {
            _targetFire = nearFire;
            _state = BState.SeekWood;
            GD.Print($"[Campfire] {_owner.NpcName}: tends fire.");
            return;
        }

        // ── Build: place a BuildOrder if no fire/pending within 20m ──────
        if (CampfireManager.Instance?.HasFireNear(_owner.GlobalPosition, 20f) == false)
        {
            var comp = OracleTablet.Instance?.LastComposition;
            bool withStone = comp != null && comp.StampCounts.ContainsKey("stone");
            string kid = withStone ? "campfire_stone" : "campfire";

            if (CampfireManager.Instance?.TryClaimBuildSite(_owner.GlobalPosition, 20f) == true)
            {
                var order = new BuildOrder();
                order.KnowledgeId  = kid;
                order.Position     = _owner.GlobalPosition;
                order.TribeId      = _owner.TribeId;
                order.IsAutonomous = true;
                order.Required     = 3f; // campfire builds fast
                // Release site claim once the BuildOrder node is ready (Campfire._Ready registers it)
                // We release here — BuildOrder itself will appear in BuildOrderManager,
                // which CampfireManager.HasFireNear doesn't check yet, so we need pending site
                // to stay until OnBuildOrderCompleted fires. Release happens in SettlementManager.
                _owner.GetParent().CallDeferred(Node.MethodName.AddChild, order);

                string style = withStone ? "mit Steinkranz" : "nur Asthaufen";
                GD.Print($"[Campfire] {_owner.NpcName}: platziert Bauauftrag Lagerfeuer ({style}).");
            }
        }
    }

    // ── Tending logic (collect wood → carry to fire) ──────────────────────
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
            { _state = BState.Idle; return false; }

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
            _owner.Knowledge.Verify("fire", 0.1f);
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
