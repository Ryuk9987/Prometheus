#nullable disable
using System.Collections.Generic;

/// <summary>
/// Knowledge combination recipes for the Oracle Editor.
/// Player selects 1–3 known concepts → Oracle reveals a new one.
/// NPCs only understand the result if they have all required ingredients at min depth.
/// </summary>
public static class KnowledgeRecipes
{
    public class Recipe
    {
        public string[]  Ingredients  { get; }   // required knowledge IDs
        public float     MinDepth     { get; }   // min depth per ingredient
        public string    Result       { get; }   // knowledge ID unlocked
        public string    Hint         { get; }   // what the player sees in editor
        public string    NpcThought   { get; }   // what NPCs say when they learn it

        public Recipe(string[] ingredients, float minDepth, string result, string hint, string npcThought)
        {
            Ingredients = ingredients;
            MinDepth    = minDepth;
            Result      = result;
            Hint        = hint;
            NpcThought  = npcThought;
        }
    }

    public static readonly List<Recipe> All = new()
    {
        // ── Tier 1: Single-concept deepening ───────────────────────────
        new(new[]{"fire"},               0.1f, "fire_control",
            "Feuer besser kontrollieren",
            "Ich kann das Feuer kontrollieren!"),

        new(new[]{"fire"},               0.3f, "cooking",
            "Nahrung über Feuer erhitzen",
            "Gebratenes Fleisch schmeckt besser und macht uns stärker."),

        new(new[]{"stone"},              0.1f, "sharp_stone",
            "Stein schärfen/brechen",
            "Ein scharfer Stein ist nützlicher als ein runder."),

        new(new[]{"tools"},              0.2f, "axe",
            "Axt aus Stein und Ast",
            "Mit einer Axt kann ich Bäume fällen!"),

        // ── Tier 2: Two-ingredient ─────────────────────────────────────
        new(new[]{"fire", "stone"},      0.2f, "fire_starter",
            "Feuer mit Steinen entzünden",
            "Zwei Steine aneinanderreiben macht Funken!"),

        new(new[]{"fire", "cooking"},    0.2f, "preservation",
            "Nahrung durch Hitze haltbar machen",
            "Getrocknetes Fleisch hält sich viel länger."),

        new(new[]{"sharp_stone", "wood"},0.2f, "spear",
            "Speer aus Stein und Holz",
            "Mit einem Speer kann ich aus der Ferne jagen."),

        new(new[]{"spear", "hunting"},   0.3f, "group_hunt",
            "Gemeinsam jagen mit Speeren",
            "In der Gruppe fangen wir größere Tiere."),

        new(new[]{"fire", "clay"},       0.2f, "pottery",
            "Ton durch Feuer formen und härten",
            "Ein Tontopf kann Wasser und Nahrung aufbewahren."),

        new(new[]{"shelter", "wood"},    0.3f, "wooden_shelter",
            "Hütte aus Holz bauen",
            "Holzwände halten besser warm als Äste."),

        new(new[]{"wood", "axe"},        0.3f, "lumber",
            "Holzstämme zu Balken verarbeiten",
            "Behauene Balken eignen sich zum Bauen."),

        new(new[]{"fire", "lumber"},     0.3f, "charcoal",
            "Holz zu Kohle brennen",
            "Kohle brennt länger und heißer als Holz."),

        new(new[]{"agriculture", "water"},0.2f, "irrigation",
            "Wasser zu Pflanzen leiten",
            "Wenn wir Wasser zu den Pflanzen bringen, wächst mehr."),

        new(new[]{"agriculture", "tools"},0.3f, "farming",
            "Felder bestellen mit Werkzeug",
            "Mit einem Werkzeug kann ich die Erde bearbeiten und mehr pflanzen."),

        new(new[]{"medicine", "fire"},   0.3f, "herbal_medicine",
            "Heilkräuter über Feuer zubereiten",
            "Gekochte Kräuter heilen besser als rohe."),

        new(new[]{"medicine", "knowledge"},0.4f, "healer",
            "Einen Heiler ausbilden",
            "Jemand sollte sich nur um die Heilkunde kümmern."),

        // ── Tier 3: Three-ingredient ───────────────────────────────────
        new(new[]{"pottery", "agriculture", "fire"}, 0.3f, "food_storage",
            "Nahrung in Töpfen für den Winter aufbewahren",
            "Wir müssen für schlechte Zeiten vorsorgen."),

        new(new[]{"lumber", "pottery", "farming"}, 0.3f, "village",
            "Ein richtiges Dorf bauen",
            "Wir brauchen feste Häuser und Vorräte für alle."),

        new(new[]{"spear", "group_hunt", "farming"}, 0.4f, "specialization",
            "Jeder macht was er am besten kann",
            "Wenn jeder eine Aufgabe hat, sind wir alle stärker."),

        new(new[]{"fire_control", "charcoal", "sharp_stone"}, 0.4f, "metalworking",
            "Steine erhitzen um Metall zu gewinnen",
            "Manche Steine werden weich wenn man sie sehr heiß macht!"),

        new(new[]{"irrigation", "farming", "pottery"}, 0.4f, "surplus",
            "Mehr erzeugen als man braucht",
            "Mit Vorräten können wir auch schlechte Zeiten überstehen."),

        new(new[]{"healer", "herbal_medicine", "preservation"}, 0.4f, "medicine_advanced",
            "Systematische Heilkunde entwickeln",
            "Wir können Krankheiten vorbeugen, nicht nur behandeln."),
    };

    /// <summary>Find recipes where all ingredients match the given IDs.</summary>
    public static List<Recipe> FindMatches(string[] selected)
    {
        var result = new List<Recipe>();
        var selSet = new System.Collections.Generic.HashSet<string>(selected);
        foreach (var r in All)
        {
            bool allPresent = true;
            foreach (var ing in r.Ingredients)
                if (!selSet.Contains(ing)) { allPresent = false; break; }
            if (allPresent) result.Add(r);
        }
        return result;
    }

    /// <summary>Which recipes are available given a NPC's knowledge?</summary>
    public static List<Recipe> AvailableFor(KnowledgeComponent knowledge)
    {
        var result = new List<Recipe>();
        foreach (var r in All)
        {
            bool ok = true;
            foreach (var ing in r.Ingredients)
                if (!knowledge.Knows(ing) || knowledge.Knowledge[ing].Depth < r.MinDepth)
                { ok = false; break; }
            if (ok && !knowledge.Knows(r.Result))
                result.Add(r);
        }
        return result;
    }
}
