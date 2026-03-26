#nullable disable
using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Each NPC interprets a drawing through their own lens:
/// their needs, knowledge, personality, culture.
/// Same drawing → different interpretations.
/// </summary>
public class InterpretationResult
{
    public string IdeaId     { get; set; }   // what concept did they extract?
    public string IdeaLabel  { get; set; }   // human-readable
    public float  Depth      { get; set; }   // how well they understood
    public float  Confidence { get; set; }
    public string Reasoning  { get; set; }   // why this NPC interpreted it this way
}

public static class InterpretationEngine
{
    // Interpretation rules: feature pattern → possible ideas with weights
    // Each rule: (shapeMatch, contextCheck, ideaId, label, baseWeight)
    private static readonly List<InterpretationRule> Rules = new()
    {
        // Shape-based
        new("circle",    null,          "astronomy",   "Sonne/Mond",    0.5f),
        new("circle",    "fire",        "astronomy",   "Feuerball",     0.7f),
        new("circle",    "hungry",      "agriculture", "Frucht/Korn",   0.8f),
        new("triangle",  null,          "shelter",     "Dach/Zelt",     0.6f),
        new("triangle",  "tools",       "metalwork",   "Pfeilspitze",   0.7f),
        new("triangle",  "shelter",     "shelter",     "Hütte",         0.9f),
        new("rectangle", null,          "shelter",     "Wand/Haus",     0.6f),
        new("rectangle", "agriculture", "agriculture", "Feld",          0.8f),
        new("rectangle", "writing",     "writing",     "Tafel/Buch",    0.9f),
        new("line",      null,          "hunting",     "Speer",         0.5f),
        new("line",      "hunting",     "hunting",     "Pfad/Jagd",     0.8f),
        new("line",      "language",    "writing",     "Schrift",       0.7f),

        // Complex drawings
        new("complex",   null,          "language",    "Symbole",       0.4f),
        new("complex",   "language",    "writing",     "Schrift",       0.7f),
        new("complex",   "astronomy",   "astronomy",   "Sternkarte",    0.8f),
        new("complex",   "medicine",    "medicine",    "Heilrezept",    0.7f),
        new("complex",   "metalwork",   "metalwork",   "Schmiedeplan",  0.8f),

        // Curious NPCs see more
        new("*",         "curious",     "language",    "Neue Zeichen",  0.3f),
    };

    public static InterpretationResult Interpret(DrawingFeatures features, NpcEntity npc)
    {
        var rng = new RandomNumberGenerator();
        rng.Randomize();

        // Build context flags for this NPC
        var ctx = BuildContext(npc);

        // Score each rule
        var candidates = new List<(InterpretationRule rule, float score)>();
        foreach (var rule in Rules)
        {
            // Shape match
            if (rule.Shape != "*" && rule.Shape != features.DominantShape) continue;

            float score = rule.BaseWeight;

            // Context bonus
            if (rule.ContextFlag != null && ctx.Contains(rule.ContextFlag))
                score += 0.3f;
            else if (rule.ContextFlag != null)
                score -= 0.2f; // context mismatch penalty

            // Personality modifiers
            score += npc.Personality.Curiosity  * 0.1f;
            score += npc.Personality.Empathy    * 0.05f;
            score -= npc.Personality.Distrust   * 0.1f;

            // Belief boosts understanding
            score += npc.Belief.Belief * 0.15f;

            // Size and complexity match skill level
            score -= Mathf.Abs(features.Complexity - npc.Personality.Curiosity) * 0.2f;

            score += rng.RandfRange(-0.1f, 0.1f); // noise

            if (score > 0f) candidates.Add((rule, score));
        }

        if (candidates.Count == 0)
            return new InterpretationResult {
                IdeaId = "unknown", IdeaLabel = "Unverständliches",
                Depth = 0.05f, Confidence = 0.1f,
                Reasoning = "Ich verstehe die Zeichen nicht."
            };

        // Pick best match
        candidates.Sort((a, b) => b.score.CompareTo(a.score));
        var best = candidates[0];

        float depth = Mathf.Clamp(best.score * npc.Belief.Belief
                                  * (0.5f + npc.Personality.Curiosity * 0.5f), 0f, 1f);
        float conf  = Mathf.Clamp(best.score * 0.8f, 0f, 1f);

        return new InterpretationResult {
            IdeaId     = best.rule.IdeaId,
            IdeaLabel  = best.rule.IdeaLabel,
            Depth      = depth,
            Confidence = conf,
            Reasoning  = BuildReasoning(npc, best.rule, features)
        };
    }

    private static HashSet<string> BuildContext(NpcEntity npc)
    {
        var ctx = new HashSet<string>();

        // Knowledge context
        foreach (var k in npc.Knowledge.Knowledge.Keys)
            ctx.Add(k);

        // Need context
        if (npc.Needs.IsHungry)  ctx.Add("hungry");
        if (npc.Needs.IsThirsty) ctx.Add("thirsty");

        // Personality
        if (npc.Personality.Curiosity > 0.6f) ctx.Add("curious");
        if (npc.Personality.Courage   > 0.6f) ctx.Add("brave");

        return ctx;
    }

    private static string BuildReasoning(NpcEntity npc, InterpretationRule rule, DrawingFeatures f)
    {
        var lines = new List<string>();

        if (npc.Needs.IsHungry)
            lines.Add("Ich bin hungrig — ich denke an Essen.");
        if (npc.Knowledge.Knows(rule.IdeaId))
            lines.Add($"Ich kenne bereits '{rule.IdeaLabel}'.");
        if (npc.Personality.Curiosity > 0.6f)
            lines.Add("Meine Neugier lässt mich tiefer nachdenken.");
        if (f.DominantShape == "circle")
            lines.Add("Die runde Form erinnert mich an...");
        if (f.DominantShape == "triangle")
            lines.Add("Die spitze Form deutet auf...");
        if (f.Complexity > 0.5f)
            lines.Add("Die Zeichen sind komplex.");

        lines.Add($"→ Ich glaube es bedeutet: \"{rule.IdeaLabel}\"");
        return string.Join(" ", lines);
    }
}

public class InterpretationRule
{
    public string Shape       { get; }
    public string ContextFlag { get; }
    public string IdeaId      { get; }
    public string IdeaLabel   { get; }
    public float  BaseWeight  { get; }

    public InterpretationRule(string shape, string ctx, string ideaId, string label, float weight)
    { Shape = shape; ContextFlag = ctx; IdeaId = ideaId; IdeaLabel = label; BaseWeight = weight; }
}
