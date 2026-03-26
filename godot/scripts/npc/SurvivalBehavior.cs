#nullable disable
using Godot;

/// <summary>
/// Drives NPC survival behavior.
/// Priority: Thirst > Hunger > Wander
/// When a need is urgent, the NPC seeks the nearest resource and harvests it.
/// </summary>
public partial class SurvivalBehavior : Node
{
    private NpcEntity      _owner;
    private ResourceNode   _target;
    private const float    HarvestRange = 1.5f;

    public bool IsSeekingResource => _target != null;

    public override void _Ready()
    {
        _owner = GetParent<NpcEntity>();
    }

    /// <summary>
    /// Called every frame from NpcEntity.
    /// Returns true if survival behavior is taking over movement.
    /// </summary>
    public bool Tick(double delta)
    {
        var need = _owner.Needs.UrgentNeed;

        // Clear invalid target
        if (_target != null && (_target.IsEmpty || !IsInstanceValid(_target)))
            _target = null;

        if (need == null)
        {
            _target = null;
            return false; // no urgent need — let wander take over
        }

        // Find a target if we don't have one
        if (_target == null)
        {
            _target = ResourceManager.Instance?.FindNearest(
                _owner.GlobalPosition, need.Value);

            if (_target == null) return false; // nothing found, keep wandering
        }

        // Move toward target
        var dir = _target.GlobalPosition - _owner.GlobalPosition;
        dir.Y = 0;
        float dist = dir.Length();

        if (dist > HarvestRange)
        {
            _owner.GlobalPosition += dir.Normalized() * 3.0f * (float)delta;
            return true;
        }

        // In range — harvest
        float harvested = _target.Harvest(1f);
        if (need == ResourceType.Food)  _owner.Needs.Eat(harvested);
        if (need == ResourceType.Water) _owner.Needs.Drink(harvested);

        // Update NPC label color to show satiation
        UpdateLabel();

        if (_target.IsEmpty) _target = null;
        return true;
    }

    private void UpdateLabel()
    {
        var label = _owner.GetNodeOrNull<Label3D>("Label3D");
        if (label == null) return;

        bool critical = _owner.Needs.IsStarving || _owner.Needs.Thirst >= 0.95f;
        label.Modulate = critical
            ? new Color(1f, 0.2f, 0.2f)   // red = critical
            : _owner.Needs.IsHungry || _owner.Needs.IsThirsty
                ? new Color(1f, 0.7f, 0.1f)  // orange = hungry/thirsty
                : new Color(1f, 1f, 1f);      // white = ok
    }
}
