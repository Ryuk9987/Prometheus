using Godot;
using System.Collections.Generic;

public partial class GameManager : Node
{
    public static GameManager Instance { get; private set; }
    public List<NpcEntity> AllNpcs { get; private set; } = new();

    private double _tickTimer = 0.0;
    private const double TickInterval = 1.0;

    [Signal] public delegate void WorldTickEventHandler(double delta);

    public override void _Ready()
    {
        Instance = this;
        GD.Print("[GameManager] Ready. World simulation started.");
    }

    public override void _Process(double delta)
    {
        _tickTimer += delta;
        if (_tickTimer >= TickInterval)
        {
            _tickTimer = 0.0;
            EmitSignal(SignalName.WorldTick, TickInterval);
        }
    }

    public void RegisterNpc(NpcEntity npc)
    {
        AllNpcs.Add(npc);
        GD.Print($"[GameManager] NPC registered: {npc.NpcName} | Total: {AllNpcs.Count}");
    }

    public void UnregisterNpc(NpcEntity npc)
    {
        AllNpcs.Remove(npc);
    }
}
