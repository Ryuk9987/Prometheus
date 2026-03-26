#nullable disable
using Godot;
using System.Collections.Generic;

/// <summary>
/// Shared data types used by DrawCanvas, OracleTablet, DrawingAnalyzer, CompositionAnalyzer.
/// </summary>

public enum DrawTool { Freehand, Line, Rectangle, Circle, Triangle }

// ── Expanded resource types ───────────────────────────────────────────────
public enum ResourceType
{
    // Raw natural
    Food, Water, Wood, Stone,
    // Tree products (raw)
    LogHardwood, LogSoftwood, Bark, Branch, Leaf, Resin,
    // Processed
    Plank, Charcoal, Fiber,
    // Gathered
    BerryEdible, BerryPoison, MushroomEdible, MushroomPoison,
    // Animal
    Meat, Bone, Pelt, Fat,
}

public class DrawnStroke
{
    public DrawTool      Tool   { get; }
    public Color         Color  { get; }
    public float         Width  { get; }
    public List<Vector2> Points { get; } = new();

    public DrawnStroke(DrawTool tool, Color color, float width)
    { Tool = tool; Color = color; Width = width; }
}

public class PlacedStamp
{
    public string  StampId   { get; set; }
    public Vector2 Position  { get; set; }
    public float   Scale     { get; set; } = 1f;
    public float   Rotation  { get; set; } = 0f;  // radians
}
