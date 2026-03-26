#nullable disable
using Godot;
using System.Collections.Generic;

public enum TaskType { Hunt, Gather, Build }
public enum TaskState { Recruiting, Active, Completed, Failed }

/// <summary>
/// A shared community task that multiple NPCs can join.
/// Emergent cooperation: NPCs with matching personality traits are recruited.
/// </summary>
public partial class CommunityTask : RefCounted
{
    public string    Id          { get; } = System.Guid.NewGuid().ToString()[..8];
    public TaskType  Type        { get; }
    public TaskState State       { get; private set; } = TaskState.Recruiting;
    public Vector3   Location    { get; set; }
    public int       MaxMembers  { get; }
    public float     Progress    { get; private set; } = 0f;
    public float     Required    { get; }             // total work needed

    public Animal    HuntTarget  { get; set; }        // for Hunt tasks
    public ResourceNode GatherTarget { get; set; }    // for Gather tasks

    private readonly List<NpcEntity> _members = new();
    public  IReadOnlyList<NpcEntity>  Members => _members;

    public CommunityTask(TaskType type, int maxMembers, float required)
    {
        Type = type; MaxMembers = maxMembers; Required = required;
    }

    public bool CanJoin     => State == TaskState.Recruiting && _members.Count < MaxMembers;
    public bool HasMembers  => _members.Count > 0;

    public void Join(NpcEntity npc)
    {
        if (!CanJoin || _members.Contains(npc)) return;
        _members.Add(npc);
        if (_members.Count >= MaxMembers) State = TaskState.Active;
        GD.Print($"[Task:{Type}] {npc.NpcName} joined. ({_members.Count}/{MaxMembers})");
    }

    public void Leave(NpcEntity npc)
    {
        _members.Remove(npc);
        if (_members.Count == 0) State = TaskState.Failed;
    }

    /// <summary>Called each frame by each participating NPC. Returns true when done.</summary>
    public bool Contribute(float amount)
    {
        if (State == TaskState.Completed || State == TaskState.Failed) return true;
        State = TaskState.Active;
        Progress += amount;
        if (Progress >= Required)
        {
            State = TaskState.Completed;
            GD.Print($"[Task:{Type}:{Id}] Completed!");
            return true;
        }
        return false;
    }

    public bool IsFinished => State == TaskState.Completed || State == TaskState.Failed;
}
