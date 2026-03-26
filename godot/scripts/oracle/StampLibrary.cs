#nullable disable
using Godot;
using System.Collections.Generic;

public enum StampEra { Stone, Tribal, Ancient }

/// <summary>
/// A physical object stamp the player can place on the Oracle canvas.
/// These are things found in nature — not abstract ideas.
/// </summary>
public class OracleStamp
{
    public string   Id       { get; }
    public string   Glyph    { get; }  // emoji or unicode shape
    public string   Label    { get; }
    public Color    Color    { get; }
    public StampEra Era      { get; }
    public Vector2  DrawSize { get; }  // size in canvas units

    public OracleStamp(string id, string glyph, string label, Color color,
                       StampEra era, Vector2 size)
    { Id=id; Glyph=glyph; Label=label; Color=color; Era=era; DrawSize=size; }
}

/// <summary>
/// All available stamps, unlocked by era.
/// Era progression is driven by NPC knowledge accumulation.
/// </summary>
public static class StampLibrary
{
    public static readonly List<OracleStamp> All = new()
    {
        // ── Stone Age (always available) ─────────────────────────────────
        new("branch",   "𝄘",  "Ast",           new Color(0.5f,0.32f,0.1f),  StampEra.Stone,   new Vector2(48,16)),
        new("stone",    "⬟",  "Stein",         new Color(0.6f,0.6f,0.6f),   StampEra.Stone,   new Vector2(28,22)),
        new("leaf",     "🍃", "Blatt",         new Color(0.2f,0.7f,0.2f),   StampEra.Stone,   new Vector2(26,32)),
        new("berry",    "●",  "Beere",         new Color(0.7f,0.1f,0.4f),   StampEra.Stone,   new Vector2(18,18)),
        new("water",    "≋",  "Wasser",        new Color(0.2f,0.5f,1.0f),   StampEra.Stone,   new Vector2(40,20)),
        new("fire",     "🔥", "Feuer",         new Color(1.0f,0.5f,0.1f),   StampEra.Stone,   new Vector2(32,40)),
        new("animal",   "🦌", "Tier",          new Color(0.7f,0.55f,0.3f),  StampEra.Stone,   new Vector2(40,36)),
        new("bone",     "𝄘",  "Knochen",       new Color(0.9f,0.88f,0.75f), StampEra.Stone,   new Vector2(36,14)),
        new("pelt",     "▭",  "Fell",          new Color(0.6f,0.45f,0.3f),  StampEra.Stone,   new Vector2(40,28)),
        new("sun",      "☀",  "Sonne",         new Color(1.0f,0.9f,0.2f),   StampEra.Stone,   new Vector2(36,36)),
        new("rain",     "≀",  "Regen",         new Color(0.4f,0.6f,0.9f),   StampEra.Stone,   new Vector2(30,36)),
        new("mountain", "⋀",  "Berg",          new Color(0.5f,0.5f,0.5f),   StampEra.Stone,   new Vector2(50,40)),

        // ── Tribal (unlocked when shelter + hunting known) ────────────────
        new("spear",    "↑",  "Speer",         new Color(0.7f,0.6f,0.4f),   StampEra.Tribal,  new Vector2(12,52)),
        new("hut",      "⌂",  "Hütte",         new Color(0.55f,0.4f,0.25f), StampEra.Tribal,  new Vector2(48,44)),
        new("pot",      "⌓",  "Topf",          new Color(0.4f,0.35f,0.3f),  StampEra.Tribal,  new Vector2(32,28)),
        new("rope",     "~",  "Seil",          new Color(0.7f,0.6f,0.3f),   StampEra.Tribal,  new Vector2(52,12)),
        new("seed",     "⊙",  "Samen",         new Color(0.5f,0.7f,0.2f),   StampEra.Tribal,  new Vector2(18,18)),

        // ── Ancient (unlocked when writing + metalwork known) ────────────
        new("tablet",   "▬",  "Stein-Tafel",   new Color(0.55f,0.55f,0.5f), StampEra.Ancient, new Vector2(36,44)),
        new("wheel",    "⊚",  "Rad",           new Color(0.5f,0.4f,0.3f),   StampEra.Ancient, new Vector2(38,38)),
        new("anvil",    "⧖",  "Amboss",        new Color(0.4f,0.4f,0.45f),  StampEra.Ancient, new Vector2(38,30)),
    };

    public static List<OracleStamp> GetAvailable(StampEra maxEra)
    {
        var result = new List<OracleStamp>();
        foreach (var s in All)
            if (s.Era <= maxEra) result.Add(s);
        return result;
    }

    public static OracleStamp Get(string id)
    {
        foreach (var s in All)
            if (s.Id == id) return s;
        return null;
    }
}
