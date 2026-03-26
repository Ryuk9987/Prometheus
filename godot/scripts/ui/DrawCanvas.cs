#nullable disable
using Godot;
using System.Collections.Generic;

/// <summary>
/// The actual drawing surface inside the Blueprint Editor.
/// Supports freehand, line, rectangle, circle, triangle.
/// Zoom/pan with scroll wheel + middle mouse.
/// Shows NPC size reference in bottom-left.
/// </summary>
public partial class DrawCanvas : Control
{
    // ── Zoom / Pan ───────────────────────────────────────────────────────────
    public float ZoomLevel { get; private set; } = 1f;
    private Vector2 _panOffset   = Vector2.Zero;
    private bool    _panning     = false;
    private Vector2 _panStart;
    private Vector2 _panOffsetStart;

    // ── Tool state ───────────────────────────────────────────────────────────
    private DrawTool _tool       = DrawTool.Freehand;
    private Color    _color      = new Color(0.4f, 0.85f, 1f);
    private float    _width      = 3f;

    // ── Strokes ──────────────────────────────────────────────────────────────
    private readonly List<DrawnStroke> _strokes = new();
    private DrawnStroke _currentStroke;
    private bool        _drawing = false;
    private Vector2     _shapeStart;

    // ── Blueprint stamp ──────────────────────────────────────────────────────
    private string _stampId   = "";
    private string _stampName = "";

    private readonly BlueprintEditor _editor;

    // NPC reference size in world units → pixels at zoom 1
    private const float NpcHeightPx = 60f;

    private static readonly Dictionary<string, string> Glyphs = new()
    {
        {"fire","🔥"},{"tools","🪨"},{"shelter","🏚"},{"hunting","🏹"},
        {"agriculture","🌾"},{"language","💬"},{"writing","📜"},
        {"medicine","🌿"},{"astronomy","⭐"},{"metalwork","⚒"},
    };

    public DrawCanvas(BlueprintEditor editor)
    {
        _editor = editor;
        FocusMode = FocusModeEnum.All;
    }

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
    }

    // ── Public API ───────────────────────────────────────────────────────────
    public void SetTool(DrawTool t)  { _tool = t; }
    public void SetColor(Color c)    { _color = c; }
    public void SetWidth(float w)    { _width = w; }

    public void Zoom(float delta)
    {
        ZoomLevel = Mathf.Clamp(ZoomLevel + delta, 0.1f, 8f);
        QueueRedraw();
    }
    public void ResetZoom() { ZoomLevel = 1f; _panOffset = Vector2.Zero; QueueRedraw(); }

    public void StampBlueprint(string id, string name)
    {
        _stampId   = id;
        _stampName = name;
        QueueRedraw();
    }

    public void Clear()
    {
        _strokes.Clear();
        _stampId   = "";
        _stampName = "";
        QueueRedraw();
    }

    // ── Input ────────────────────────────────────────────────────────────────
    public void ForwardInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb)
        {
            var local = ToLocal(mb.GlobalPosition);

            if (mb.ButtonIndex == MouseButton.WheelUp)   { Zoom(+0.15f); _editor.UpdateZoomLabel(); return; }
            if (mb.ButtonIndex == MouseButton.WheelDown)  { Zoom(-0.15f); _editor.UpdateZoomLabel(); return; }

            if (mb.ButtonIndex == MouseButton.Middle)
            {
                _panning = mb.Pressed;
                _panStart = local;
                _panOffsetStart = _panOffset;
                return;
            }

            if (mb.ButtonIndex == MouseButton.Left)
            {
                if (mb.Pressed)
                {
                    _drawing = true;
                    var wp = ScreenToWorld(local);
                    _shapeStart = wp;
                    _currentStroke = new DrawnStroke(_tool, _color, _width);
                    _currentStroke.Points.Add(wp);
                }
                else if (_drawing)
                {
                    _drawing = false;
                    if (_currentStroke != null)
                    {
                        _strokes.Add(_currentStroke);
                        _currentStroke = null;
                    }
                    QueueRedraw();
                }
            }
        }
        else if (@event is InputEventMouseMotion mm)
        {
            var local = ToLocal(mm.GlobalPosition);

            if (_panning)
            {
                _panOffset = _panOffsetStart + (local - _panStart);
                QueueRedraw();
                return;
            }

            if (_drawing && _currentStroke != null)
            {
                var wp = ScreenToWorld(local);
                if (_tool == DrawTool.Freehand)
                    _currentStroke.Points.Add(wp);
                else
                {
                    // For shapes: only keep start + current end
                    if (_currentStroke.Points.Count > 1)
                        _currentStroke.Points.RemoveAt(_currentStroke.Points.Count - 1);
                    _currentStroke.Points.Add(wp);
                }
                QueueRedraw();
            }
        }
    }

    // ── Coordinate helpers ───────────────────────────────────────────────────
    private Vector2 WorldToScreen(Vector2 world)
        => world * ZoomLevel + Size / 2f + _panOffset;

    private Vector2 ScreenToWorld(Vector2 screen)
        => (screen - Size / 2f - _panOffset) / ZoomLevel;

    // ── Draw ─────────────────────────────────────────────────────────────────
    public override void _Draw()
    {
        // Background
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(0.04f, 0.06f, 0.16f));

        // Grid
        DrawGrid();

        // Blueprint stamp (background guide)
        if (_stampId != "")
            DrawBlueprintStamp();

        // All committed strokes
        foreach (var s in _strokes)
            DrawStroke(s);

        // Current in-progress stroke
        if (_currentStroke != null)
            DrawStroke(_currentStroke);

        // NPC size reference (bottom-left)
        DrawNpcReference();

        // Zoom / pan hint
        DrawString(ThemeDB.FallbackFont,
            new Vector2(10, Size.Y - 12),
            $"Zoom {(int)(ZoomLevel*100)}%  |  Scroll=Zoom  MMB=Pan",
            HorizontalAlignment.Left, -1, 12, new Color(0.4f,0.4f,0.6f,0.8f));
    }

    private void DrawGrid()
    {
        float gridSize = 32f * ZoomLevel;
        var   origin   = Size / 2f + _panOffset;
        var   gridCol  = new Color(0.12f, 0.2f, 0.38f, 0.5f);

        float startX = origin.X % gridSize;
        for (float x = startX; x < Size.X; x += gridSize)
            DrawLine(new Vector2(x, 0), new Vector2(x, Size.Y), gridCol, 1f);
        float startY = origin.Y % gridSize;
        for (float y = startY; y < Size.Y; y += gridSize)
            DrawLine(new Vector2(0, y), new Vector2(Size.X, y), gridCol, 1f);

        // Axis lines
        DrawLine(new Vector2(origin.X, 0), new Vector2(origin.X, Size.Y), new Color(0.2f,0.35f,0.6f,0.5f), 1.5f);
        DrawLine(new Vector2(0, origin.Y), new Vector2(Size.X, origin.Y), new Color(0.2f,0.35f,0.6f,0.5f), 1.5f);
    }

    private void DrawBlueprintStamp()
    {
        var center = WorldToScreen(Vector2.Zero);
        string g = Glyphs.TryGetValue(_stampId, out var gv) ? gv : "?";
        float  r = 80f * ZoomLevel;

        DrawArc(center, r, 0, Mathf.Tau, 64, new Color(0.3f,0.6f,1f,0.25f), 2f);
        DrawString(ThemeDB.FallbackFont, center + new Vector2(-r * 0.35f, r * 0.25f),
            g, HorizontalAlignment.Center, -1, (int)(56 * ZoomLevel),
            new Color(0.5f, 0.8f, 1f, 0.4f));
        DrawString(ThemeDB.FallbackFont, center + new Vector2(0, r + 18f),
            _stampName, HorizontalAlignment.Center, -1, (int)(14 * ZoomLevel),
            new Color(0.4f, 0.7f, 1f, 0.5f));
    }

    private void DrawStroke(DrawnStroke s)
    {
        if (s.Points.Count < 1) return;
        var p0 = WorldToScreen(s.Points[0]);

        switch (s.Tool)
        {
            case DrawTool.Freehand:
                for (int i = 1; i < s.Points.Count; i++)
                    DrawLine(p0 = WorldToScreen(s.Points[i - 1]),
                             WorldToScreen(s.Points[i]), s.Color, s.Width * ZoomLevel);
                break;

            case DrawTool.Line:
                if (s.Points.Count >= 2)
                    DrawLine(p0, WorldToScreen(s.Points[^1]), s.Color, s.Width * ZoomLevel);
                break;

            case DrawTool.Rectangle:
                if (s.Points.Count >= 2)
                {
                    var p1 = WorldToScreen(s.Points[^1]);
                    DrawRect(new Rect2(p0, p1 - p0), s.Color, false, s.Width * ZoomLevel);
                }
                break;

            case DrawTool.Circle:
                if (s.Points.Count >= 2)
                {
                    var p1 = WorldToScreen(s.Points[^1]);
                    float rad = p0.DistanceTo(p1);
                    DrawArc(p0, rad, 0, Mathf.Tau, 64, s.Color, s.Width * ZoomLevel);
                }
                break;

            case DrawTool.Triangle:
                if (s.Points.Count >= 2)
                {
                    var p1  = WorldToScreen(s.Points[^1]);
                    var top = new Vector2((p0.X + p1.X) * 0.5f, p0.Y);
                    var bl  = new Vector2(p0.X, p1.Y);
                    var br  = new Vector2(p1.X, p1.Y);
                    DrawLine(top, bl,  s.Color, s.Width * ZoomLevel);
                    DrawLine(bl,  br,  s.Color, s.Width * ZoomLevel);
                    DrawLine(br,  top, s.Color, s.Width * ZoomLevel);
                }
                break;
        }
    }

    private void DrawNpcReference()
    {
        float px = NpcHeightPx * ZoomLevel;
        var   x  = 18f;
        var   yb = Size.Y - 50f;
        var   yt = yb - px;

        // Stick figure
        var col = new Color(1f, 0.8f, 0.4f, 0.7f);
        float cx = x + 8f;
        // head
        DrawArc(new Vector2(cx, yt + 7f), 7f, 0, Mathf.Tau, 24, col, 1.5f);
        // body
        DrawLine(new Vector2(cx, yt + 14f), new Vector2(cx, yt + px * 0.55f), col, 1.5f);
        // arms
        DrawLine(new Vector2(cx - 8f, yt + px * 0.3f), new Vector2(cx + 8f, yt + px * 0.3f), col, 1.5f);
        // legs
        DrawLine(new Vector2(cx, yt + px * 0.55f), new Vector2(cx - 7f, yb), col, 1.5f);
        DrawLine(new Vector2(cx, yt + px * 0.55f), new Vector2(cx + 7f, yb), col, 1.5f);

        // Height bracket
        DrawLine(new Vector2(x + 20f, yt), new Vector2(x + 26f, yt), col, 1.5f);
        DrawLine(new Vector2(x + 23f, yt), new Vector2(x + 23f, yb), col, 1.5f);
        DrawLine(new Vector2(x + 20f, yb), new Vector2(x + 26f, yb), col, 1.5f);

        DrawString(ThemeDB.FallbackFont, new Vector2(x + 30f, yt + px * 0.5f + 6f),
            "NPC", HorizontalAlignment.Left, -1, 12, new Color(1f, 0.8f, 0.4f, 0.7f));
    }
}

// ── Data ────────────────────────────────────────────────────────────────────
public class DrawnStroke
{
    public DrawTool        Tool   { get; }
    public Color           Color  { get; }
    public float           Width  { get; }
    public List<Vector2>   Points { get; } = new();

    public DrawnStroke(DrawTool tool, Color color, float width)
    { Tool = tool; Color = color; Width = width; }
}
