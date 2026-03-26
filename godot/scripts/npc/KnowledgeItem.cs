/// <summary>
/// Represents a single piece of knowledge held by an NPC.
/// Knowledge lives in individuals — not in the world.
/// </summary>
public class KnowledgeItem
{
    public string Id { get; set; }
    public float Depth { get; set; }        // 0.0 = heard of it, 1.0 = mastered
    public float Confidence { get; set; }   // 0.0 = uncertain, 1.0 = certain
    public string SourceNpcId { get; set; } // Who taught this?
    public bool IsVerified { get; set; }    // Confirmed through own experience?

    public KnowledgeItem(string id, float depth = 0.1f, float confidence = 0.3f)
    {
        Id = id;
        Depth = depth;
        Confidence = confidence;
        SourceNpcId = "";
        IsVerified = false;
    }

    public override string ToString() =>
        $"{Id} (depth:{Depth:F2}, confidence:{Confidence:F2}, verified:{IsVerified})";
}
