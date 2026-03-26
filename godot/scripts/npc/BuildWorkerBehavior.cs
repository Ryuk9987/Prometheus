#nullable disable
using Godot;

/// <summary>
/// NPC searches for open build orders it has knowledge for, then works on them.
/// Priority below CampfireBehavior, above Wander.
/// </summary>
public partial class BuildWorkerBehavior : Node
{
    private NpcEntity  _owner;
    private BuildOrder _target;
    private const float WorkRange   = 2.5f;
    private const float MoveSpeed   = 2.8f;
    private double      _checkTimer = 0;
    private const double CheckInterval = 8.0;

    public bool IsActive => _target != null && _target.Status != BuildOrderStatus.Done;

    public override void _Ready() => _owner = GetParent<NpcEntity>();

    public bool Tick(double delta)
    {
        // Clear completed/invalid target
        if (_target != null && (_target.Status == BuildOrderStatus.Done || !IsInstanceValid(_target)))
            _target = null;

        // Find new target periodically
        if (_target == null)
        {
            _checkTimer += delta;
            if (_checkTimer < CheckInterval) return false;
            _checkTimer = 0;
            _target = BuildOrderManager.Instance?.FindNearestRelevant(
                _owner.GlobalPosition, _owner);
            if (_target == null) return false;
            GD.Print($"[Builder] {_owner.NpcName} → going to build {_target.KnowledgeId}");
        }

        // Move to build site
        var dir = _target.GlobalPosition - _owner.GlobalPosition; dir.Y = 0;
        if (dir.Length() > WorkRange)
        {
            _owner.GlobalPosition += dir.Normalized() * MoveSpeed * (float)delta;
            return true;
        }

        // Work!
        float skill = 0.3f + _owner.Knowledge.Knowledge[_target.KnowledgeId].Depth * 0.5f;
        _target.Work(skill * (float)delta);
        return true;
    }
}
