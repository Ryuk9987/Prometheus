#nullable disable
using Godot;
using System.Collections.Generic;

/// <summary>
/// Renders onto the SubViewport that becomes the tablet screen texture.
/// Two modes:
///   Blueprint — shows a predefined schematic with glyph icons
///   Draw      — player draws freehand with mouse
/// </summary>
public partial class TabletCanvas : Control
{
    private TabletMode _mode = TabletMode.Blueprint;
    private string     _blueprintId = "";

    // Draw mode
    private readonly List<List<Vector2>> _strokes = new();
    private List<Vector2> _currentStroke;
    private bool          _drawing = false;

    // Blueprint glyphs (simple shape descriptions rendered procedurally)
    private static readonly Dictionary<string, string> BlueprintGlyphs = new()
    {
        { "fire",        "🔥" },
        { "tools",       "🪨" },
        { "shelter",     "🏚" },
        { "hunting",     "🏹" },
        { "agriculture", "🌾" },
        { "language",    "💬" },
        { "writing",     "📜" },
        { "medicine",    "🌿" },
        { "astronomy",   "⭐" },
        { "metalwork",   "⚒" },
    };

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
    }

    public void ShowBlueprint(string ideaId)
    {
        _mode        = TabletMode.Blueprint;
        _blueprintId = ideaId;
        _strokes.Clear();
        QueueRedraw();
    }

    public void SetDrawMode()
    {
        _mode = TabletMode.Draw;
        _blueprintId = "";
        QueueRedraw();
    }

    public void Clear()
    {
        _strokes.Clear();
        _blueprintId = "";
        QueueRedraw();
    }

    public override void _Draw()
    {
        // Background
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(0.04f, 0.06f, 0.18f));

        // Grid lines
        var gridColor = new Color(0.15f, 0.25f, 0.5f, 0.5f);
        for (float x = 0; x < Size.X; x += 32) DrawLine(new Vector2(x, 0), new Vector2(x, Size.Y), gridColor, 1f);
        for (float y = 0; y < Size.Y; y += 32) DrawLine(new Vector2(0, y), new Vector2(Size.X, y), gridColor, 1f);

        if (_mode == TabletMode.Blueprint && !string.IsNullOrEmpty(_blueprintId))
            DrawBlueprint();
        else
            DrawStrokes();
    }

    private void DrawBlueprint()
    {
        var center = Size / 2f;

        // Outer circle
        DrawArc(center, 100f, 0, Mathf.Tau, 64, new Color(0.3f, 0.6f, 1f, 0.6f), 2f);

        // Cross-hairs
        var lineCol = new Color(0.2f, 0.5f, 0.9f, 0.4f);
        DrawLine(new Vector2(center.X, 0), new Vector2(center.X, Size.Y), lineCol, 1f);
        DrawLine(new Vector2(0, center.Y), new Vector2(Size.X, center.Y), lineCol, 1f);

        // Glyph
        string glyph = BlueprintGlyphs.TryGetValue(_blueprintId, out var g) ? g : "?";
        DrawString(ThemeDB.FallbackFont, center - new Vector2(40, -20), glyph,
            HorizontalAlignment.Center, -1, 72, new Color(0.7f, 0.9f, 1f));

        // Idea name
        if (OracleManager.Ideas.TryGetValue(_blueprintId, out var idea))
            DrawString(ThemeDB.FallbackFont, new Vector2(center.X - 100, Size.Y - 30),
                idea.DisplayName, HorizontalAlignment.Left, 200, 20, new Color(0.5f, 0.8f, 1f));

        // Corner markers
        var corners = new[] {
            new Vector2(20, 20), new Vector2(Size.X-20, 20),
            new Vector2(20, Size.Y-20), new Vector2(Size.X-20, Size.Y-20)
        };
        foreach (var c in corners)
        {
            DrawLine(c, c + new Vector2(12, 0), new Color(0.4f, 0.7f, 1f), 2f);
            DrawLine(c, c + new Vector2(0, 12), new Color(0.4f, 0.7f, 1f), 2f);
        }
    }

    private void DrawStrokes()
    {
        DrawString(ThemeDB.FallbackFont, new Vector2(10, 24), "✏ Freihand",
            HorizontalAlignment.Left, -1, 18, new Color(0.5f, 0.8f, 1f, 0.7f));

        var strokeCol = new Color(0.6f, 0.9f, 1f);
        foreach (var stroke in _strokes)
        {
            for (int i = 1; i < stroke.Count; i++)
                DrawLine(stroke[i - 1], stroke[i], strokeCol, 3f);
        }
        if (_currentStroke != null)
            for (int i = 1; i < _currentStroke.Count; i++)
                DrawLine(_currentStroke[i - 1], _currentStroke[i], new Color(1f, 1f, 0.6f), 3f);
    }

    // Draw input — only active when tablet UI is open
    public void HandleInput(InputEvent @event, Vector2 localPos)
    {
        if (_mode != TabletMode.Draw) return;

        if (@event is InputEventMouseButton mb)
        {
            if (mb.ButtonIndex == MouseButton.Left)
            {
                if (mb.Pressed)
                {
                    _currentStroke = new List<Vector2> { localPos };
                    _drawing = true;
                }
                else if (_drawing)
                {
                    _strokes.Add(_currentStroke);
                    _currentStroke = null;
                    _drawing = false;
                    QueueRedraw();
                }
            }
        }
        else if (@event is InputEventMouseMotion mm && _drawing)
        {
            _currentStroke.Add(localPos);
            QueueRedraw();
        }
    }
}
