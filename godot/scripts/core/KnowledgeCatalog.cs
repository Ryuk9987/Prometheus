#nullable disable
using Godot;
using System.Collections.Generic;
using System.Linq;

public enum KnowledgeCategory
{
    Nature,     // fire, water, plants — found in world
    Tool,       // axe, spear, bow, rope — craftable items
    Building,   // shelter, hut, wall, tower — placeable structures
    Skill,      // hunting, farming, medicine — passive behaviors
    Concept,    // language, writing, astronomy — meta-knowledge
}

/// <summary>
/// A fully defined knowledge entry — what it is, what it unlocks, what it costs.
/// </summary>
public class KnowledgeDefinition
{
    public string           Id           { get; }
    public string           DisplayName  { get; }
    public string           Description  { get; }
    public KnowledgeCategory Category    { get; }
    public string           Icon         { get; }
    public float            MinDepth     { get; }  // minimum depth to use/build
    public List<MaterialCost> Materials  { get; }  // what's needed to build/craft
    public List<string>     Unlocks      { get; }  // other knowledge ids this enables
    public List<string>     Requires     { get; }  // prerequisite knowledge

    public KnowledgeDefinition(string id, string name, string desc,
        KnowledgeCategory cat, string icon, float minDepth,
        List<MaterialCost> materials = null,
        List<string> unlocks = null,
        List<string> requires = null)
    {
        Id = id; DisplayName = name; Description = desc;
        Category = cat; Icon = icon; MinDepth = minDepth;
        Materials = materials ?? new();
        Unlocks   = unlocks   ?? new();
        Requires  = requires  ?? new();
    }
}

public class MaterialCost
{
    public ResourceType Resource { get; }
    public float        Amount   { get; }
    public MaterialCost(ResourceType r, float a) { Resource = r; Amount = a; }
}

/// <summary>
/// Master catalog of all knowledge in the game.
/// NPCs discover entries through oracle, experience, or teaching.
/// </summary>
public static class KnowledgeCatalog
{
    public static readonly Dictionary<string, KnowledgeDefinition> All = new();

    static KnowledgeCatalog()
    {
        // ── Nature ────────────────────────────────────────────────────────
        Add("fire",     "Feuer",       "Wärme und Licht aus Holz.",
            KnowledgeCategory.Nature, "🔥", 0.1f,
            materials: new(){ new(ResourceType.Wood, 3f) });

        Add("water",    "Wasser",      "Lebensnotwendige Flüssigkeit.",
            KnowledgeCategory.Nature, "💧", 0.05f);

        Add("medicine", "Heilkunde",   "Blätter und Beeren heilen Wunden.",
            KnowledgeCategory.Nature, "🌿", 0.3f,
            requires: new(){ "fire" });

        // ── Tools ─────────────────────────────────────────────────────────
        Add("tools",    "Stein-Werkzeug","Stein auf Stein ergibt scharfe Kanten.",
            KnowledgeCategory.Tool, "🪨", 0.2f,
            materials: new(){ new(ResourceType.Stone, 2f) });

        Add("hunting",  "Speer",       "Ein Ast mit Steinspitze — tödlich auf Distanz.",
            KnowledgeCategory.Tool, "🏹", 0.25f,
            materials: new(){ new(ResourceType.Wood, 1f), new(ResourceType.Stone, 1f) },
            requires: new(){ "tools" });

        Add("bow",      "Bogen & Pfeil","Biegbarer Ast + Sehne + Pfeil.",
            KnowledgeCategory.Tool, "🎯", 0.3f,
            materials: new(){ new(ResourceType.Wood, 2f) },
            requires: new(){ "hunting" },
            unlocks: new(){ "hunting" });

        Add("rope",     "Seil",        "Gedrehte Fasern halten vieles zusammen.",
            KnowledgeCategory.Tool, "🪢", 0.2f,
            materials: new(){ new(ResourceType.Wood, 1f) });

        Add("axe",      "Axt",         "Schwerer Stein, gebunden an Holzgriff.",
            KnowledgeCategory.Tool, "🪓", 0.35f,
            materials: new(){ new(ResourceType.Stone, 2f), new(ResourceType.Wood, 1f) },
            requires: new(){ "tools", "rope" });

        Add("shovel",   "Schaufel",    "Flacher Stein am Stock — gräbt die Erde.",
            KnowledgeCategory.Tool, "⛏", 0.35f,
            materials: new(){ new(ResourceType.Stone, 2f), new(ResourceType.Wood, 1f) },
            requires: new(){ "tools" },
            unlocks: new(){ "agriculture" });

        Add("pot",      "Tonkrug",     "Geformter Lehm, gebrannt im Feuer.",
            KnowledgeCategory.Tool, "🏺", 0.4f,
            materials: new(){ new(ResourceType.Stone, 1f) },
            requires: new(){ "fire" },
            unlocks: new(){ "medicine" });

        // ── Buildings ─────────────────────────────────────────────────────
        Add("shelter",  "Unterkunft",  "Äste und Fell — schützt vor Regen.",
            KnowledgeCategory.Building, "🏚", 0.2f,
            materials: new(){ new(ResourceType.Wood, 5f) });

        Add("hut",      "Hütte",       "Stabiler Unterstand mit Steinfundament.",
            KnowledgeCategory.Building, "🏠", 0.4f,
            materials: new(){ new(ResourceType.Wood, 8f), new(ResourceType.Stone, 4f) },
            requires: new(){ "shelter", "tools" },
            unlocks: new(){ "writing" });

        Add("wall",     "Mauer",       "Steinmauer schützt die Gemeinschaft.",
            KnowledgeCategory.Building, "🧱", 0.45f,
            materials: new(){ new(ResourceType.Stone, 10f) },
            requires: new(){ "tools" });

        Add("storehouse","Vorratslager","Schutz vor Verderb — Essen hält länger.",
            KnowledgeCategory.Building, "🏛", 0.4f,
            materials: new(){ new(ResourceType.Wood, 6f), new(ResourceType.Stone, 2f) },
            requires: new(){ "hut" },
            unlocks: new(){ "agriculture" });

        // ── Skills ────────────────────────────────────────────────────────
        Add("agriculture","Ackerbau",  "Samen in Erde — Nahrung wächst von selbst.",
            KnowledgeCategory.Skill, "🌾", 0.3f,
            materials: new(){ },
            requires: new(){ "shovel" });

        // ── Concepts ──────────────────────────────────────────────────────
        Add("language", "Sprache",     "Gemeinsame Laute tragen Bedeutung.",
            KnowledgeCategory.Concept, "💬", 0.2f,
            unlocks: new(){ "writing" });

        Add("writing",  "Schrift",     "Zeichen auf Stein — Wissen überlebt den Tod.",
            KnowledgeCategory.Concept, "📜", 0.5f,
            materials: new(){ new(ResourceType.Stone, 1f) },
            requires: new(){ "language" },
            unlocks: new(){ "astronomy" });

        Add("astronomy","Astronomie",  "Die Sterne zeigen Zeit und Richtung.",
            KnowledgeCategory.Concept, "⭐", 0.5f,
            requires: new(){ "writing" });

        Add("metalwork","Metallarbeit","Feuer verwandelt Stein in etwas Härteres.",
            KnowledgeCategory.Concept, "⚒", 0.6f,
            requires: new(){ "fire", "tools" },
            materials: new(){ new(ResourceType.Stone, 3f) });
    }

    private static void Add(string id, string name, string desc,
        KnowledgeCategory cat, string icon, float minDepth,
        List<MaterialCost> materials = null,
        List<string> requires = null,
        List<string> unlocks = null)
    {
        All[id] = new KnowledgeDefinition(id, name, desc, cat, icon, minDepth,
            materials, unlocks, requires);
    }

    public static KnowledgeDefinition Get(string id)
        => All.TryGetValue(id, out var def) ? def : null;

    public static List<KnowledgeDefinition> GetByCategory(KnowledgeCategory cat)
        => All.Values.Where(d => d.Category == cat).ToList();

    /// <summary>Returns all knowledge that a given NPC has reached minimum depth for.</summary>
    public static List<KnowledgeDefinition> GetUnlocked(NpcEntity npc)
    {
        var result = new List<KnowledgeDefinition>();
        foreach (var def in All.Values)
        {
            if (!npc.Knowledge.Knows(def.Id)) continue;
            if (npc.Knowledge.Knowledge[def.Id].Depth < def.MinDepth) continue;
            result.Add(def);
        }
        return result;
    }
}
