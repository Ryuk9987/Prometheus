#nullable disable
using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Manages tribes — groups of NPCs that live together.
/// NPCs self-organize: gather around campfires, form bonds, elect leaders.
/// Tribes can split when sub-groups diverge far enough in belief or knowledge.
/// </summary>
public partial class TribeManager : Node
{
    public static TribeManager Instance { get; private set; }

    public readonly List<Tribe> Tribes = new();
    private double _scanTimer = 0;
    private const double ScanInterval = 15.0;

    public override void _Ready()
    {
        Instance = this;

        // Create initial tribe
        var initial = new Tribe("Alpha", new Color(0.3f, 0.8f, 0.3f));
        Tribes.Add(initial);

        // Assign all starting NPCs to initial tribe
        CallDeferred(MethodName.AssignStartingNpcs);
    }

    private void AssignStartingNpcs()
    {
        if (GameManager.Instance == null) return;
        foreach (var npc in GameManager.Instance.AllNpcs)
            Tribes[0].AddMember(npc);
    }

    public override void _Process(double delta)
    {
        _scanTimer += delta;
        if (_scanTimer < ScanInterval) return;
        _scanTimer = 0;

        foreach (var tribe in Tribes.ToList())
            tribe.Update();

        TryFormNewTribes();
        TrySplit();

        // Update NPC labels with tribe color
        foreach (var tribe in Tribes)
            foreach (var npc in tribe.Members)
                UpdateNpcLabel(npc, tribe);
    }

    private void TryFormNewTribes()
    {
        // NPCs without tribe — assign to nearest tribe or form new one
        if (GameManager.Instance == null) return;
        foreach (var npc in GameManager.Instance.AllNpcs)
        {
            if (GetTribe(npc) != null) continue;

            Tribe closest = null; float bestDist = 30f;
            foreach (var tribe in Tribes)
            {
                if (tribe.Members.Count == 0) continue;
                float d = tribe.Center.DistanceTo(npc.GlobalPosition);
                if (d < bestDist) { bestDist = d; closest = tribe; }
            }

            if (closest != null) closest.AddMember(npc);
            else
            {
                // Form new tribe
                var newTribe = new Tribe($"Tribe_{Tribes.Count + 1}", RandomColor());
                newTribe.AddMember(npc);
                Tribes.Add(newTribe);
                GD.Print($"[TribeManager] New tribe formed: {newTribe.Name}");
            }
        }
    }

    private void TrySplit()
    {
        foreach (var tribe in Tribes.ToList())
        {
            if (tribe.Members.Count < 6) continue;

            // Find sub-groups too far from tribe center
            var farMembers = tribe.Members
                .Where(n => n.GlobalPosition.DistanceTo(tribe.Center) > 40f)
                .ToList();

            if (farMembers.Count >= 3)
            {
                var splinter = new Tribe($"{tribe.Name}_Splinter", RandomColor());
                foreach (var m in farMembers)
                {
                    tribe.RemoveMember(m);
                    splinter.AddMember(m);
                }
                Tribes.Add(splinter);
                GD.Print($"[TribeManager] Tribe split! {splinter.Name} formed from {tribe.Name} ({farMembers.Count} members).");
            }
        }
    }

    public Tribe GetTribe(NpcEntity npc)
    {
        foreach (var t in Tribes)
            if (t.Members.Contains(npc)) return t;
        return null;
    }

    private void UpdateNpcLabel(NpcEntity npc, Tribe tribe)
    {
        var label = npc.GetNodeOrNull<Label3D>("Label3D");
        if (label != null) label.Modulate = tribe.Color;
    }

    private static Color RandomColor()
    {
        var rng = new RandomNumberGenerator(); rng.Randomize();
        return Color.FromHsv(rng.RandfRange(0f, 1f), 0.7f, 0.9f);
    }
}

public class Tribe
{
    public string            Name     { get; }
    public Color             Color    { get; }
    public List<NpcEntity>   Members  { get; } = new();
    public NpcEntity         Leader   { get; private set; }
    public Vector3           Center   { get; private set; }

    /// <summary>
    /// Permanent settlement center set by the Leader NPC.
    /// Unlike Center (dynamic avg position), this stays fixed once established.
    /// </summary>
    public Vector3           SettlementCenter   { get; set; }
    public bool              HasSettlementCenter { get; set; } = false;

    public Tribe(string name, Color color) { Name = name; Color = color; }

    public void AddMember(NpcEntity npc)
    {
        if (!Members.Contains(npc)) Members.Add(npc);
        npc.TribeId = Name;
    }

    public void RemoveMember(NpcEntity npc) => Members.Remove(npc);

    public void Update()
    {
        if (Members.Count == 0) return;

        // Recalculate center
        var avg = Vector3.Zero;
        foreach (var m in Members) avg += m.GlobalPosition;
        Center = avg / Members.Count;

        // Elect leader: most knowledgeable + trusted
        Leader = Members
            .OrderByDescending(n => n.Knowledge.Knowledge.Sum(k => k.Value.Depth)
                                  + n.Belief.Belief * 0.5f)
            .FirstOrDefault();

        // Rally behavior: non-leader members drift toward tribe center at night
        if (DayCycle.Instance != null && DayCycle.Instance.IsNight)
        {
            foreach (var m in Members)
            {
                if (m == Leader) continue;
                // Nudge toward center (gentle, not override)
                m.TribeCenterHint = Center;
                m.ShouldRallyToTribe = true;
            }
        }
        else
        {
            foreach (var m in Members)
                m.ShouldRallyToTribe = false;
        }
    }
}
