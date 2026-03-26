#nullable disable
using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// What the player built on the canvas — as a recipe to decipher.
/// </summary>
public class Composition
{
    public List<PlacedStamp>        Stamps      { get; } = new();
    public List<DrawnStroke>        FreeStrokes { get; } = new();

    // Derived
    public Dictionary<string, int>  StampCounts { get; } = new();
    public string                   PrimaryIdea { get; set; } = "";
    public string                   Description { get; set; } = "";
}

/// <summary>
/// Analyzes placed stamps + freehand strokes and derives what concept is depicted.
/// Pure pattern matching — no AI.
/// </summary>
public static class CompositionAnalyzer
{
    // Recipe: required stamps → resulting idea + description
    private static readonly List<CompositionRecipe> Recipes = new()
    {
        // Fire-related
        new(new[]{"branch","branch","fire"},         "fire",        "Lagerfeuer",    0.9f),
        new(new[]{"branch","fire"},                  "fire",        "Feuer entzünden",0.7f),
        new(new[]{"stone","stone","fire"},            "fire",        "Feuersteine",   0.8f),
        new(new[]{"branch","branch","branch"},        "shelter",     "Holzhaufen/Bau",0.6f),

        // Hunting
        new(new[]{"branch","stone"},                 "hunting",     "Speerspitze",   0.7f),
        new(new[]{"animal","spear"},                 "hunting",     "Jagd",          0.9f),
        new(new[]{"animal","bone"},                  "hunting",     "Beute",         0.7f),
        new(new[]{"rope","branch"},                  "hunting",     "Falle",         0.7f),

        // Shelter
        new(new[]{"branch","branch","pelt"},         "shelter",     "Zelt/Unterkunft",0.85f),
        new(new[]{"hut"},                            "shelter",     "Hütte",         0.95f),
        new(new[]{"stone","stone","stone"},          "shelter",     "Steinmauer",    0.7f),

        // Agriculture
        new(new[]{"seed","water"},                   "agriculture", "Pflanze bewässern",0.8f),
        new(new[]{"seed","seed","seed","sun"},       "agriculture", "Feld anlegen",  0.9f),
        new(new[]{"leaf","berry","berry"},           "agriculture", "Nahrungspflanze",0.7f),

        // Medicine
        new(new[]{"leaf","berry","water"},           "medicine",    "Heiltrank",     0.8f),
        new(new[]{"leaf","leaf","pot"},              "medicine",    "Heilmittel kochen",0.85f),

        // Astronomy
        new(new[]{"sun","mountain"},                 "astronomy",   "Sonnenuhr/Berg",0.7f),
        new(new[]{"sun","rain"},                     "astronomy",   "Wetter beobachten",0.7f),

        // Writing/Language
        new(new[]{"stone","bone"},                   "language",    "Ritzzeichen",   0.7f),
        new(new[]{"tablet"},                         "writing",     "Schrifttafel",  0.9f),

        // Metalwork
        new(new[]{"stone","fire","stone"},           "metalwork",   "Stein schmelzen",0.7f),
        new(new[]{"anvil","fire"},                   "metalwork",   "Schmiedearbeit",0.9f),

        // Tools
        new(new[]{"stone","bone"},                   "tools",       "Knochenwerkzeug",0.7f),
        new(new[]{"stone","stone"},                  "tools",       "Steinwerkzeug", 0.65f),
    };

    public static Composition Analyze(List<PlacedStamp> stamps, List<DrawnStroke> strokes)
    {
        var comp = new Composition();
        comp.Stamps.AddRange(stamps);
        comp.FreeStrokes.AddRange(strokes ?? new List<DrawnStroke>());

        // Count stamps
        foreach (var s in stamps)
        {
            if (!comp.StampCounts.ContainsKey(s.StampId))
                comp.StampCounts[s.StampId] = 0;
            comp.StampCounts[s.StampId]++;
        }

        // Find best matching recipe
        CompositionRecipe bestRecipe = null;
        float bestScore = 0f;

        foreach (var recipe in Recipes)
        {
            float score = recipe.Match(comp.StampCounts);
            if (score > bestScore)
            {
                bestScore   = score;
                bestRecipe  = recipe;
            }
        }

        if (bestRecipe != null && bestScore > 0.3f)
        {
            comp.PrimaryIdea  = bestRecipe.IdeaId;
            comp.Description  = bestRecipe.Description;
        }
        else if (stamps.Count > 0)
        {
            // Fallback: most common stamp
            var top = comp.StampCounts.OrderByDescending(k => k.Value).First();
            var topStamp = StampLibrary.Get(top.Key);
            comp.PrimaryIdea = "unknown";
            comp.Description  = topStamp != null ? $"Viele {topStamp.Label}" : "Unbekannte Komposition";
        }
        else
        {
            comp.PrimaryIdea = "unknown";
            comp.Description  = "Leere Zeichnung";
        }

        return comp;
    }
}

public class CompositionRecipe
{
    public string[] RequiredStamps { get; }
    public string   IdeaId         { get; }
    public string   Description    { get; }
    public float    BaseWeight     { get; }

    public CompositionRecipe(string[] stamps, string ideaId, string desc, float weight)
    { RequiredStamps = stamps; IdeaId = ideaId; Description = desc; BaseWeight = weight; }

    /// <summary>Returns match score 0..1 based on how well the counts satisfy requirements.</summary>
    public float Match(Dictionary<string, int> counts)
    {
        var needed = new Dictionary<string, int>();
        foreach (var s in RequiredStamps)
        {
            if (!needed.ContainsKey(s)) needed[s] = 0;
            needed[s]++;
        }

        float total = needed.Count, matched = 0f;
        foreach (var kv in needed)
        {
            if (counts.TryGetValue(kv.Key, out int have))
                matched += Mathf.Min(have, kv.Value) / (float)kv.Value;
        }

        return (matched / total) * BaseWeight;
    }
}
