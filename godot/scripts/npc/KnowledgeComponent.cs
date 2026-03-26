using Godot;
using System.Collections.Generic;

/// <summary>
/// Knowledge lives in individuals.
/// Without writing, it can only spread mouth-to-mouth — with distortion and loss.
/// When an NPC dies, their unshared knowledge dies with them.
/// </summary>
public partial class KnowledgeComponent : Node
{
    public Dictionary<string, KnowledgeItem> Knowledge { get; private set; } = new();

    /// <summary>Learn or reinforce a piece of knowledge.</summary>
    public void Learn(string id, float depth, float confidence, string sourceId = "")
    {
        if (Knowledge.TryGetValue(id, out var existing))
        {
            // Reinforce existing knowledge
            existing.Depth      = Mathf.Max(existing.Depth, depth);
            existing.Confidence = Mathf.Lerp(existing.Confidence, confidence, 0.3f);
        }
        else
        {
            Knowledge[id] = new KnowledgeItem(id, depth, confidence) { SourceNpcId = sourceId };
            GD.Print($"[Knowledge] {GetParent().Name} learned: {id} (depth:{depth:F2})");
        }
    }

    /// <summary>Verify knowledge through personal experience — increases depth and confidence.</summary>
    public void Verify(string id, float experienceGain = 0.2f)
    {
        if (Knowledge.TryGetValue(id, out var item))
        {
            item.Depth      = Mathf.Min(item.Depth + experienceGain, 1.0f);
            item.Confidence = Mathf.Min(item.Confidence + 0.1f, 1.0f);
            item.IsVerified = true;
        }
    }

    public bool Knows(string id) => Knowledge.ContainsKey(id);

    /// <summary>
    /// Teach a piece of knowledge to another NPC.
    /// Transfer quality depends on teacher skill and learner curiosity.
    /// Distortion simulates oral tradition imperfection.
    /// </summary>
    public void TeachTo(KnowledgeComponent other, string id, float teacherEmpathy, float learnerCuriosity)
    {
        if (!Knowledge.TryGetValue(id, out var item)) return;

        var rng = new RandomNumberGenerator();
        rng.Randomize();

        float transferRate  = item.Depth * teacherEmpathy * learnerCuriosity;
        float transferred   = transferRate * rng.RandfRange(0.5f, 1.0f);
        float distortion    = rng.RandfRange(-0.05f, 0.05f);  // oral tradition loss/distortion

        float finalDepth      = Mathf.Clamp(transferred + distortion, 0f, 1f);
        float finalConfidence = item.Confidence * 0.75f; // confidence reduces when passed on

        other.Learn(id, finalDepth, finalConfidence, GetParent().Name);
        GD.Print($"[Knowledge] {GetParent().Name} taught {id} to {other.GetParent().Name} (depth:{finalDepth:F2})");
    }

    public string Summary()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var k in Knowledge.Values)
            sb.AppendLine($"  - {k}");
        return sb.ToString();
    }
}
