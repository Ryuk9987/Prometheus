#nullable disable
using Godot;

/// <summary>
/// Handles an NPC's participation in community tasks.
/// The NPC moves to the task location and contributes work.
/// Role is derived from personality: brave=hunter, empathetic=gatherer/builder.
/// </summary>
public partial class CooperationComponent : Node
{
    private NpcEntity     _owner;
    private CommunityTask _currentTask;
    private const float   WorkRange   = 3.5f;
    private const float   MoveSpeed   = 1.1f;

    public bool HasTask => _currentTask != null && !_currentTask.IsFinished;

    public string Role
    {
        get
        {
            if (_owner == null) return "none";
            if (_owner.Personality.Courage  > 0.6f) return "hunter";
            if (_owner.Personality.Empathy  > 0.6f) return "gatherer";
            if (_owner.Personality.Curiosity > 0.6f) return "scout";
            return "worker";
        }
    }

    public override void _Ready()
    {
        _owner = GetParent<NpcEntity>();
    }

    public void AssignTask(CommunityTask task)
    {
        _currentTask = task;
        GD.Print($"[Coop] {_owner.NpcName} ({Role}) assigned to {task.Type} task.");
    }

    /// <summary>
    /// Called each frame from NpcEntity when no survival need is active.
    /// Returns true if cooperation is taking over movement.
    /// </summary>
    public bool Tick(double delta)
    {
        if (_currentTask == null) return false;

        if (_currentTask.IsFinished)
        {
            _currentTask = null;
            return false;
        }

        Vector3 target = _currentTask.Location;

        // Update hunt target position (animals move)
        if (_currentTask.Type == TaskType.Hunt && _currentTask.HuntTarget != null)
        {
            if (!IsInstanceValid(_currentTask.HuntTarget))
            {
                _currentTask = null;
                return false;
            }
            target = _currentTask.HuntTarget.GlobalPosition;
            _currentTask.Location = target;
        }

        // Move toward task location
        var dir = target - _owner.GlobalPosition;
        dir.Y = 0;
        float dist = dir.Length();

        if (dist > WorkRange)
        {
            _owner.GlobalPosition += dir.Normalized() * MoveSpeed * (float)delta;
            return true;
        }

        // In range — do work
        PerformWork(delta);
        return true;
    }

    private void PerformWork(double delta)
    {
        float workRate = 0.3f + _owner.Personality.Courage * 0.4f;

        switch (_currentTask.Type)
        {
            case TaskType.Hunt:
                if (_currentTask.HuntTarget != null && IsInstanceValid(_currentTask.HuntTarget))
                {
                    bool killed = _currentTask.HuntTarget.Strike(workRate * (float)delta * 2f);
                    if (killed) _currentTask.HuntTarget = null;
                }
                break;

            case TaskType.Gather:
                if (_currentTask.GatherTarget != null && !_currentTask.GatherTarget.IsEmpty)
                {
                    float got = _currentTask.GatherTarget.Harvest(workRate * (float)delta);
                    _owner.Needs.Eat(got);
                }
                break;
        }

        bool done = _currentTask.Contribute(workRate * (float)delta);
        if (done) _currentTask = null;
    }
}
