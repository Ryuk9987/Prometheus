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

public class KnowledgeDefinition
{
    public string           Id           { get; }
    public string           DisplayName  { get; }
    public string           Description  { get; }
    public KnowledgeCategory Category    { get; }
    public string           Icon         { get; }
    public float            MinDepth     { get; }
    public List<MaterialCost> Materials  { get; }
    public List<string>     Unlocks      { get; }
    public List<string>     Requires     { get; }

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

public static class KnowledgeCatalog
{
    public static readonly Dictionary<string, KnowledgeDefinition> All = new();

    static KnowledgeCatalog()
    {
        // ════════════════════════════════════════════════════════════════
        // NATURE — raw knowledge about the world
        // ════════════════════════════════════════════════════════════════
        Add("stone",    "Stein",       "Harter Stein — Grundwerkzeug und Baumaterial.",
            KnowledgeCategory.Nature, "🪨", 0.1f);

        Add("wood",     "Holz",        "Äste und Stämme — vielseitig verwendbar.",
            KnowledgeCategory.Nature, "🪵", 0.1f);

        Add("water",    "Wasser",      "Lebensnotwendige Flüssigkeit — Quellen und Bäche.",
            KnowledgeCategory.Nature, "💧", 0.1f);

        Add("clay",     "Ton",         "Formbarer Ton aus feuchter Erde.",
            KnowledgeCategory.Nature, "🟤", 0.15f);

        Add("plants",   "Pflanzen",    "Gräser, Moos, Blätter — Material für vieles.",
            KnowledgeCategory.Nature, "🌿", 0.1f);

        // ════════════════════════════════════════════════════════════════
        // FIRE — aufgeteilt in: Feuer machen vs. Lagerfeuer bauen
        // ════════════════════════════════════════════════════════════════

        // Tier 1: Feuer machen — Grundtechnik (Feuerbohrer per Hand)
        Add("fire_making", "Feuer machen",
            "Zwei Stöcke schnell reiben bis eine Glut entsteht. Mühsam aber möglich.",
            KnowledgeCategory.Skill, "🔥", 0.1f,
            materials: new(){ new(ResourceType.Wood, 1f) });

        // Tier 2: Feuer — Wissen über Feuer selbst (Eigenschaften, Pflege)
        Add("fire",     "Feuer",
            "Wärme und Licht. Hält Raubtiere fern. Ohne Pflege erlischt es.",
            KnowledgeCategory.Nature, "🔥", 0.1f);

        // Tier 3: Feuertechniken (Upgrades von fire_making)
        Add("fire_drill", "Feuerbohrer (Bogen)",
            "Bogen dreht den Bohrstock schneller — Feuer in der Hälfte der Zeit.",
            KnowledgeCategory.Tool, "🏹", 0.25f,
            materials: new(){ new(ResourceType.Wood, 1f) },
            requires: new(){ "fire_making", "rope_making" },
            unlocks: new(){ "fire_making" });

        Add("fire_striker", "Feuerstein",
            "Feuerstein auf Pyrit schlagen — Funken ins Zunder-Nest.",
            KnowledgeCategory.Tool, "✨", 0.3f,
            requires: new(){ "fire_making", "stone" },
            unlocks: new(){ "fire_making" });

        // ════════════════════════════════════════════════════════════════
        // CAMPFIRE — Lagerfeuer (Upgrade-Kette)
        // ════════════════════════════════════════════════════════════════

        // Tier 1: Einfaches Lagerfeuer (nur Äste, kein Schutz)
        Add("campfire", "Lagerfeuer",
            "Äste aufgeschichtet und entzündet. Wärmt, leuchtet, hält Tiere fern. " +
            "Kein Windschutz — erlischt schnell.",
            KnowledgeCategory.Building, "🔥", 0.1f,
            materials: new(){ new(ResourceType.Wood, 3f) },
            requires: new(){ "fire_making" },
            unlocks: new(){ "cooking", "campfire_stone" });

        // Tier 2: Steinkranz — besser, windgeschützt
        Add("campfire_stone", "Lagerfeuer mit Steinkranz",
            "Steine halten Windschutz und Hitze besser. Kocht Nahrung gleichmäßiger.",
            KnowledgeCategory.Building, "🔥🪨", 0.15f,
            materials: new(){ new(ResourceType.Wood, 3f), new(ResourceType.Stone, 4f) },
            requires: new(){ "campfire", "stone" },
            unlocks: new(){ "campfire_pot_hook" });

        // Tier 3: Aufhängevorrichtung (Topf über Feuer)
        Add("campfire_pot_hook", "Lagerfeuer mit Topfaufhängung",
            "Querbalken über dem Feuer hält einen Topf — gleichmäßiges Kochen.",
            KnowledgeCategory.Building, "🍲", 0.25f,
            materials: new(){ new(ResourceType.Wood, 4f), new(ResourceType.Stone, 4f) },
            requires: new(){ "campfire_stone", "pottery", "rope_making" },
            unlocks: new(){ "cooking_advanced" });

        // ════════════════════════════════════════════════════════════════
        // COOKING
        // ════════════════════════════════════════════════════════════════
        Add("cooking", "Einfaches Kochen",
            "Fleisch und Wurzeln über dem Feuer garen — besser verdaulich.",
            KnowledgeCategory.Skill, "🍖", 0.2f,
            requires: new(){ "fire", "campfire" });

        Add("cooking_advanced", "Topfkochen",
            "Im Topf kochen — Suppen, Eintöpfe, länger haltbar.",
            KnowledgeCategory.Skill, "🍲", 0.35f,
            requires: new(){ "cooking", "pottery", "campfire_pot_hook" });

        // ════════════════════════════════════════════════════════════════
        // SHELTER — Unterkunft (Upgrade-Kette)
        // ════════════════════════════════════════════════════════════════

        // Tier 1: Äste-Unterkunft (Lean-to) — kein Komfort, minimal
        Add("shelter", "Ast-Unterkunft",
            "Schräg gestellte Äste gegen den Wind — kein Bett, kein Boden. " +
            "Hält Regen ab. Dach aus Blättern oder Moos.",
            KnowledgeCategory.Building, "🏚", 0.1f,
            materials: new(){ new(ResourceType.Wood, 5f), new(ResourceType.Branch, 3f) },
            unlocks: new(){ "shelter_improved" });

        // Tier 2: Verbesserte Ast-Hütte (Gerüst mit Blättern/Fell)
        Add("shelter_improved", "Einfache Hütte",
            "Rundes Gerüst aus Stangen, bedeckt mit Blättern und Fell. " +
            "Etwas Wärme, etwas Sicherheit. Kein Boden.",
            KnowledgeCategory.Building, "🏠", 0.2f,
            materials: new(){ new(ResourceType.Wood, 8f), new(ResourceType.Branch, 5f) },
            requires: new(){ "shelter" },
            unlocks: new(){ "shelter_mud", "hut" });

        // Tier 3a: Lehmbewurf (Dämmung)
        Add("shelter_mud", "Lehmhütte",
            "Wände mit Lehm verputzt — isoliert besser, hält Wind und Kälte ab.",
            KnowledgeCategory.Building, "🏠", 0.3f,
            materials: new(){ new(ResourceType.Wood, 8f), new(ResourceType.Branch, 5f),
                              new(ResourceType.Stone, 2f) },
            requires: new(){ "shelter_improved", "clay" },
            unlocks: new(){ "hut" });

        // Tier 3b: Echte Hütte (braucht Holzverarbeitung)
        Add("hut", "Hütte",
            "Stabiler Unterstand mit Holzbalken und Steinfundament.",
            KnowledgeCategory.Building, "🏠", 0.4f,
            materials: new(){ new(ResourceType.LogHardwood, 4f), new(ResourceType.Stone, 4f) },
            requires: new(){ "shelter_improved", "lumber" },
            unlocks: new(){ "writing", "storehouse", "wooden_shelter" });

        Add("wooden_shelter", "Holzhütte",
            "Stabile Behausung aus Holzbalken und Brettern.",
            KnowledgeCategory.Building, "🪵", 0.45f,
            materials: new(){ new(ResourceType.LogHardwood, 6f), new(ResourceType.Wood, 4f) },
            requires: new(){ "hut", "lumber" });

        // ════════════════════════════════════════════════════════════════
        // LANGUAGE — Stammessprache
        // ════════════════════════════════════════════════════════════════
        Add("language", "Stammessprache",
            "Gemeinsame Laute und Gesten. Koordination bei Jagd und Bau. " +
            "Geschichten am Lagerfeuer — Wissen wird weitergegeben.",
            KnowledgeCategory.Concept, "💬", 0.1f,
            unlocks: new(){ "group_hunt", "teaching", "writing" });

        Add("teaching", "Wissen weitergeben",
            "Aktives Unterweisen statt zufälliges Beobachten. Wissen überträgt sich tiefer.",
            KnowledgeCategory.Concept, "📖", 0.3f,
            requires: new(){ "language" });

        // ════════════════════════════════════════════════════════════════
        // SURVIVAL BASICS — Grundkenntnisse zum Überleben
        // ════════════════════════════════════════════════════════════════

        // Sammeln
        Add("foraging", "Sammeln",
            "Essbare Beeren, Früchte, Pilze und Wurzeln erkennen und sammeln. " +
            "Giftige Pflanzen meiden — wird durch Erfahrung gelernt.",
            KnowledgeCategory.Skill, "🫐", 0.1f,
            unlocks: new(){ "food_storage" });

        Add("food_gathering", "Vorratshaltung (einfach)",
            "Gesammelte Nahrung an einem sicheren Ort lagern.",
            KnowledgeCategory.Skill, "🧺", 0.2f,
            requires: new(){ "foraging" });

        // Jagd
        Add("hunting", "Jagd",
            "Tierspuren lesen, Fallen stellen, anschleichen. Anfangs nur Kleinwild.",
            KnowledgeCategory.Skill, "🏹", 0.15f,
            unlocks: new(){ "group_hunt", "hide_working", "spear" });

        Add("group_hunt", "Gemeinschaftsjagd",
            "Koordiniertes Jagen in der Gruppe — größere Tiere werden möglich.",
            KnowledgeCategory.Skill, "🦌", 0.35f,
            requires: new(){ "hunting", "language" });

        // Seile aus Pflanzenfasern
        Add("rope_making", "Seilherstellung",
            "Gras, Baumrinde und Pflanzenfasern zu einfachen Seilen drehen. " +
            "Grundlage für Werkzeuge und Bau.",
            KnowledgeCategory.Skill, "🪢", 0.15f,
            materials: new(){ new(ResourceType.Branch, 2f) },
            unlocks: new(){ "axe", "bow", "fire_drill" });

        // Fell-Bearbeitung (ohne Nadel/Schere)
        Add("hide_working", "Fellbearbeitung",
            "Fell mit einem Stein schaben und glätten. Kein Schnitt, kein Nähen — " +
            "nur grobe Umhänge. Für richtige Kleidung braucht man Nadeln.",
            KnowledgeCategory.Skill, "🧣", 0.15f,
            materials: new(){ new(ResourceType.Stone, 1f) },
            requires: new(){ "hunting" },
            unlocks: new(){ "clothing_basic" });

        // Einfache Fellkleidung
        Add("clothing_basic", "Einfache Fellkleidung",
            "Grobe Umhänge aus Fell — schützt gegen Kälte. Kein Schnitt, kein Nähen. " +
            "Für bessere Kleidung braucht man Knochennadeln.",
            KnowledgeCategory.Skill, "🧥", 0.2f,
            requires: new(){ "hide_working" },
            unlocks: new(){ "clothing_sewn" });

        // Knochennadel → genähte Kleidung
        Add("bone_needle", "Knochennadel",
            "Aus Tierknochen geschliffen — ermöglicht Nähen.",
            KnowledgeCategory.Tool, "🦴", 0.3f,
            materials: new(){ new(ResourceType.Stone, 1f) },
            requires: new(){ "hunting", "sharp_stone" },
            unlocks: new(){ "clothing_sewn" });

        Add("clothing_sewn", "Genähte Kleidung",
            "Mit Knochennadel und Sehne genähtes Fell — passt besser, wärmt mehr.",
            KnowledgeCategory.Skill, "🧥", 0.35f,
            requires: new(){ "clothing_basic", "bone_needle", "rope_making" });

        // ════════════════════════════════════════════════════════════════
        // TOOLS
        // ════════════════════════════════════════════════════════════════
        Add("sharp_stone", "Scharfer Stein",
            "Einen Stein gezielt absplittern für scharfe Kanten.",
            KnowledgeCategory.Tool, "🪨", 0.15f,
            requires: new(){ "stone" });

        Add("tools", "Steinwerkzeug",
            "Systematisches Herstellen von Werkzeug aus Stein.",
            KnowledgeCategory.Tool, "🪨", 0.2f,
            materials: new(){ new(ResourceType.Stone, 2f) },
            requires: new(){ "sharp_stone" });

        Add("spear", "Speer",
            "Stein an Holz gebunden — Nahkampf und Jagd.",
            KnowledgeCategory.Tool, "🏹", 0.25f,
            materials: new(){ new(ResourceType.Stone, 1f), new(ResourceType.Wood, 2f) },
            requires: new(){ "tools", "rope_making" });

        Add("bow", "Bogen & Pfeil",
            "Biegbarer Ast + Sehne + Pfeil — Jagd auf Distanz.",
            KnowledgeCategory.Tool, "🎯", 0.35f,
            materials: new(){ new(ResourceType.Wood, 2f) },
            requires: new(){ "spear", "rope_making" });

        Add("axe", "Steinaxt",
            "Schwerer Stein, gebunden an Holzgriff — fällt Bäume.",
            KnowledgeCategory.Tool, "🪓", 0.35f,
            materials: new(){ new(ResourceType.Stone, 2f), new(ResourceType.Wood, 1f) },
            requires: new(){ "tools", "rope_making" });

        Add("shovel", "Grabstock / Steinschaufel",
            "Flacher Stein am Stock — gräbt die Erde.",
            KnowledgeCategory.Tool, "⛏", 0.3f,
            materials: new(){ new(ResourceType.Stone, 2f), new(ResourceType.Wood, 1f) },
            requires: new(){ "tools" });

        Add("pot", "Tonkrug",
            "Geformter Lehm, gebrannt im Feuer.",
            KnowledgeCategory.Tool, "🏺", 0.4f,
            materials: new(){ new(ResourceType.Stone, 1f) },
            requires: new(){ "fire", "clay" },
            unlocks: new(){ "food_storage", "cooking_advanced" });

        // ════════════════════════════════════════════════════════════════
        // CRAFTING & PROCESSING
        // ════════════════════════════════════════════════════════════════
        Add("lumber", "Holzverarbeitung",
            "Baumstämme zu Balken und Brettern bearbeiten.",
            KnowledgeCategory.Skill, "🪚", 0.3f,
            requires: new(){ "axe" },
            unlocks: new(){ "hut", "wooden_shelter" });

        Add("pottery", "Töpferei",
            "Ton formen und im Feuer härten.",
            KnowledgeCategory.Skill, "🏺", 0.3f,
            requires: new(){ "clay", "fire" },
            unlocks: new(){ "pot", "campfire_pot_hook" });

        Add("preservation", "Konservierung",
            "Nahrung durch Trocknen, Räuchern und Salzen haltbar machen.",
            KnowledgeCategory.Skill, "🧂", 0.3f,
            requires: new(){ "cooking", "food_gathering" });

        Add("charcoal", "Holzkohle",
            "Holz zu Kohle brennen — intensivere, gleichmäßigere Hitze.",
            KnowledgeCategory.Tool, "⬛", 0.3f,
            requires: new(){ "fire", "lumber" });

        // ════════════════════════════════════════════════════════════════
        // MEDICINE
        // ════════════════════════════════════════════════════════════════
        Add("medicine", "Heilkunde",
            "Bestimmte Blätter und Beeren lindern Schmerzen und Wunden.",
            KnowledgeCategory.Skill, "🌿", 0.2f,
            requires: new(){ "foraging" });

        Add("herbal_medicine", "Kräutermedizin",
            "Heilkräuter über Feuer zubereiten — wirksamer als rohe Anwendung.",
            KnowledgeCategory.Skill, "🌿", 0.35f,
            requires: new(){ "medicine", "cooking" });

        Add("medicine_advanced", "Fortgeschrittene Medizin",
            "Systematische Heilkunde mit Werkzeug und Wissen.",
            KnowledgeCategory.Concept, "💊", 0.55f,
            requires: new(){ "herbal_medicine", "pottery" });

        // ════════════════════════════════════════════════════════════════
        // BUILDINGS (further)
        // ════════════════════════════════════════════════════════════════
        Add("wall", "Mauer",
            "Steinmauer schützt die Gemeinschaft.",
            KnowledgeCategory.Building, "🧱", 0.45f,
            materials: new(){ new(ResourceType.Stone, 10f) },
            requires: new(){ "tools" });

        Add("storehouse", "Vorratslager",
            "Schutz vor Verderb — Essen und Materialien haltbar aufbewahrt.",
            KnowledgeCategory.Building, "🏛", 0.4f,
            materials: new(){ new(ResourceType.Wood, 6f), new(ResourceType.Stone, 2f) },
            requires: new(){ "hut", "food_gathering" });

        Add("well", "Brunnen",
            "Gegraben bis zum Grundwasser — stetiger Wasservorrat.",
            KnowledgeCategory.Building, "💧", 0.4f,
            materials: new(){ new(ResourceType.Stone, 6f) },
            requires: new(){ "shovel", "pottery" });

        // ════════════════════════════════════════════════════════════════
        // AGRICULTURE
        // ════════════════════════════════════════════════════════════════
        Add("agriculture", "Ackerbau",
            "Samen in Erde — Nahrung wächst von selbst.",
            KnowledgeCategory.Skill, "🌾", 0.3f,
            requires: new(){ "shovel", "foraging" });

        Add("farming", "Feldwirtschaft",
            "Systematischer Ackerbau mit Werkzeug und Bewässerung.",
            KnowledgeCategory.Skill, "🌾", 0.4f,
            requires: new(){ "agriculture", "shovel" });

        Add("irrigation", "Bewässerung",
            "Wasser zu Feldern leiten.",
            KnowledgeCategory.Skill, "💦", 0.4f,
            requires: new(){ "farming", "shovel" });

        // ════════════════════════════════════════════════════════════════
        // CONCEPTS & SOCIETY
        // ════════════════════════════════════════════════════════════════
        Add("writing", "Schrift",
            "Zeichen auf Stein — Wissen überlebt den Tod.",
            KnowledgeCategory.Concept, "📜", 0.5f,
            materials: new(){ new(ResourceType.Stone, 1f) },
            requires: new(){ "language" },
            unlocks: new(){ "astronomy" });

        Add("astronomy", "Astronomie",
            "Die Sterne zeigen Zeit und Richtung.",
            KnowledgeCategory.Concept, "⭐", 0.5f,
            requires: new(){ "writing" });

        Add("food_storage", "Vorratshaltung",
            "Nahrung in Töpfen für Wintermonate aufbewahren.",
            KnowledgeCategory.Concept, "🏛", 0.4f,
            requires: new(){ "food_gathering", "pottery" });

        Add("healer", "Heilerrolle",
            "Spezialisierte Person für Heilkunde im Stamm.",
            KnowledgeCategory.Concept, "🩺", 0.4f,
            requires: new(){ "herbal_medicine" });

        Add("village", "Dorf",
            "Feste Siedlung mit Häusern und Vorräten.",
            KnowledgeCategory.Building, "🏘", 0.5f);

        Add("specialization", "Arbeitsteilung",
            "Jeder macht was er am besten kann.",
            KnowledgeCategory.Concept, "⚙", 0.45f);

        Add("knowledge", "Wissensbegriff",
            "Das Konzept des Lernens selbst — Wissen kann weitergegeben werden.",
            KnowledgeCategory.Concept, "📖", 0.3f);

        Add("surplus", "Überschuss",
            "Mehr erzeugen als man braucht — Basis für Handel.",
            KnowledgeCategory.Concept, "📦", 0.45f);

        // ════════════════════════════════════════════════════════════════
        // METALLURGY (späte Ära)
        // ════════════════════════════════════════════════════════════════
        Add("metalwork", "Metallarbeit",
            "Feuer verwandelt Erz in etwas Härteres.",
            KnowledgeCategory.Concept, "⚒", 0.6f,
            requires: new(){ "fire", "tools", "charcoal" },
            materials: new(){ new(ResourceType.Stone, 3f) });

        Add("metalworking", "Metallverarbeitung",
            "Erze erhitzen und zu Werkzeug formen.",
            KnowledgeCategory.Concept, "⚒", 0.65f,
            materials: new(){ new(ResourceType.Stone, 3f), new(ResourceType.Resin, 1f) },
            requires: new(){ "metalwork" });
    }

    private static void Add(string id, string name, string desc,
        KnowledgeCategory cat, string icon, float minDepth,
        List<MaterialCost> materials = null,
        List<string> requires = null,
        List<string> unlocks = null)
    {
        if (All.ContainsKey(id))
        {
            GD.PrintErr($"[KnowledgeCatalog] Duplicate id: {id}");
            return;
        }
        All[id] = new KnowledgeDefinition(id, name, desc, cat, icon, minDepth,
                                           materials, unlocks, requires);
    }

    public static KnowledgeDefinition Get(string id)
        => All.TryGetValue(id, out var def) ? def : null;

    public static List<KnowledgeDefinition> GetByCategory(KnowledgeCategory cat)
        => All.Values.Where(d => d.Category == cat).ToList();
}
