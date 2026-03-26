#nullable disable
using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Creates and manages community tasks.
/// Periodically scans the world for opportunities:
///   — animals nearby → propose Hunt task
///   — multiple NPCs hungry → propose Gather task
/// NPCs with fitting personalities get recruited.
/// </summary>
public partial class TaskManager : Node
{
    public static TaskManager Instance { get; private set; }

    private readonly List<CommunityTask> _tasks = new();
    private double _scanTimer = 0;
    private const double ScanInterval = 8.0; // seconds between world scans

    public IReadOnlyList<CommunityTask> Tasks => _tasks;

    public override void _Ready()
    {
        Instance = this;
    }

    public override void _Process(double delta)
    {
        _scanTimer += delta;
        if (_scanTimer >= ScanInterval)
        {
            _scanTimer = 0;
            ScanWorld();
        }

        // Clean finished tasks
        _tasks.RemoveAll(t => t.IsFinished);
    }

    private void ScanWorld()
    {
        if (GameManager.Instance == null) return;
        var npcs = GameManager.Instance.AllNpcs;
        if (npcs.Count == 0) return;

        // --- Hunt opportunity ---
        if (AnimalManager.Instance != null && AnimalManager.Instance.Animals.Count > 0)
        {
            // Find cluster of hungry NPCs
            foreach (var npc in npcs)
            {
                if (!npc.Needs.IsHungry) continue;
                if (IsAlreadyInHuntTask(npc)) continue;

                var animal = AnimalManager.Instance.FindNearest(npc.GlobalPosition, 35f);
                if (animal == null) continue;

                // Create hunt task
                var task = new CommunityTask(TaskType.Hunt, maxMembers: 3, required: 5f);
                task.Location   = animal.GlobalPosition;
                task.HuntTarget = animal;
                _tasks.Add(task);

                // Recruit nearby brave NPCs
                RecruitNearby(task, npc.GlobalPosition, 20f,
                    candidate => candidate.Personality.Courage > 0.4f
                              && candidate.Needs.IsHungry
                              && !IsInAnyTask(candidate));

                if (task.HasMembers)
                    GD.Print($"[TaskManager] Hunt task created near {animal.Type}. Members: {task.Members.Count}");
                else
                    _tasks.Remove(task);

                break; // one hunt task per scan
            }
        }

        // --- Gather opportunity ---
        if (ResourceManager.Instance != null)
        {
            int hungryCount = 0;
            NpcEntity seedNpc = null;
            foreach (var npc in npcs)
            {
                if (npc.Needs.IsHungry && !IsInAnyTask(npc)) { hungryCount++; seedNpc = npc; }
            }

            if (hungryCount >= 3 && seedNpc != null)
            {
                var res = ResourceManager.Instance.FindNearest(seedNpc.GlobalPosition, ResourceType.Food);
                if (res != null)
                {
                    var task = new CommunityTask(TaskType.Gather, maxMembers: 4, required: 3f);
                    task.Location      = res.GlobalPosition;
                    task.GatherTarget  = res;
                    _tasks.Add(task);

                    RecruitNearby(task, seedNpc.GlobalPosition, 25f,
                        candidate => candidate.Personality.Empathy > 0.3f
                                  && candidate.Needs.IsHungry
                                  && !IsInAnyTask(candidate));

                    if (task.HasMembers)
                        GD.Print($"[TaskManager] Gather task created. Members: {task.Members.Count}");
                    else
                        _tasks.Remove(task);
                }
            }
        }
    }

    private void RecruitNearby(CommunityTask task, Vector3 origin, float radius,
        System.Func<NpcEntity, bool> filter)
    {
        foreach (var npc in GameManager.Instance.AllNpcs)
        {
            if (!task.CanJoin) break;
            if (!filter(npc)) continue;
            if (npc.GlobalPosition.DistanceTo(origin) > radius) continue;
            task.Join(npc);
            npc.Cooperation.AssignTask(task);
        }
    }

    private bool IsInAnyTask(NpcEntity npc)
    {
        foreach (var t in _tasks)
            if (t.Members.Contains(npc)) return true;
        return false;
    }

    private bool IsAlreadyInHuntTask(NpcEntity npc)
    {
        foreach (var t in _tasks)
            if (t.Type == TaskType.Hunt && t.Members.Contains(npc)) return true;
        return false;
    }
}
