#nullable disable
using Godot;
using System.Collections.Generic;

/// <summary>
/// Handles NPC social interactions — proximity-based knowledge sharing.
/// NPCs within TalkRadius will occasionally "talk" and exchange knowledge.
/// Without writing, this is the only way knowledge spreads.
/// </summary>
public partial class SocialComponent : Node
{
    [Export] public float TalkRadius    { get; set; } = 4.0f;
    [Export] public float TalkCooldown  { get; set; } = 5.0f; // seconds between talks

    private NpcEntity      _owner;
    private double         _talkTimer = 0;

    public override void _Ready()
    {
        _owner = GetParent<NpcEntity>();
    }

    public void OnWorldTick(double delta)
    {
        _talkTimer += delta;
        if (_talkTimer < TalkCooldown) return;
        _talkTimer = 0;

        TryTalkToNearbyNpc();
    }

    private void TryTalkToNearbyNpc()
    {
        if (GameManager.Instance == null) return;

        NpcEntity closest = null;
        float closestDist = TalkRadius;

        foreach (var other in GameManager.Instance.AllNpcs)
        {
            if (other == _owner) continue;
            float dist = _owner.GlobalPosition.DistanceTo(other.GlobalPosition);
            if (dist < closestDist)
            {
                closestDist = dist;
                closest = other;
            }
        }

        if (closest == null) return;

        // Spread belief
        _owner.Belief.SpreadTo(closest.Belief, _owner.Personality.Empathy);

        // Share a random piece of knowledge
        var myKnowledge = _owner.Knowledge.Knowledge;
        if (myKnowledge.Count == 0) return;

        var keys = new List<string>(myKnowledge.Keys);
        string topic = keys[GD.RandRange(0, keys.Count - 1)];

        _owner.Knowledge.TeachTo(
            closest.Knowledge,
            topic,
            _owner.Personality.Empathy,
            closest.Personality.Curiosity
        );
    }
}
