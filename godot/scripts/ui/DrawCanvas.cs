#nullable disable
using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// The Blueprint Canvas.
/// Left panel: place & drag stamps. Right panel: draw freehand on top.
/// Stamps are physical objects from nature (Branch, Stone, Fire…)
/// </summary>
public partial class DrawCanvas : Control
{
    // ── Zoom / Pan ───────────────────────────────────────────────────────────
    public float  ZoomLevel  { get; private set; } = 1f;
    private Vector2 _panOffset       = Vector2.Zero;
    private bool    _panning         = false;
    private Vector2 _panStart;
    private Vector2 _panOffsetStart;

    // ── Stamps ───────────────────────────────────────────────────────────────
    private readonly List<PlacedStamp> _placed    = new();
    private PlacedStamp  _dragging   = null;
    private Vector2      _dragOffset = Vector2.Zero;
    private OracleStamp  _pendingStamp = null;  // stamp selected in sidebar, waiting for placement

    // ── Freehand strokes ─────────────────────────────────────────────────────
    private readonly List<DrawnStroke> _strokes = new();
    private DrawnStroke  _currentStroke;
    private bool         _drawingStroke = false;
    private Vector2      _shapeStart;

    // ── Tool ─────────────────────────────────────────────────────────────────
    private DrawTool _tool   = DrawTool.Freehand;
    private Color    _color  = new Color(0.4f, 0.85f, 1f);
    private float    _width  = 3f;

    // ── Editor ref ───────────────────────────────────────────────────────────
    public BlueprintEditor Editor { get; set; }

    private const float NpcHeightPx = 60f;
    private static readonly Dictionary<string, string> GlyphOverrides = new()
    {
        {"fire","🔥"},{"leaf","🍃"},{"animal","🦌"},{"sun","☀"},
        {"hut","⌂"},{"wheel","⊚"},
    };

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        FocusMode   = FocusModeEnum.All;
    }

    // ── Public API ───────────────────────────────────────────────────────────
    public void SetTool(DrawTool t)             { _tool = t; }
    public void SetColor(Color c)               { _color = c; }
    public void SetWidth(float w)               { _width = w; }
    public List<DrawnStroke>  GetStrokes()      => new(_strokes);
    public List<PlacedStamp>  GetPlacedStamps() => new(_placed);

    public void SelectStampForPlacement(OracleStamp stamp)
    {
        _pendingStamp = stamp;
        Editor?._UpdateStatus($"📍 Klicke auf Canvas um '{stamp.Label}' zu platzieren.");
    }

    public void Zoom(float delta)
    {
        ZoomLevel = Mathf.Clamp(ZoomLevel + delta, 0.15f, 6f);
        QueueRedraw();
    }
    public void ResetZoom() { ZoomLevel = 1f; _panOffset = Vector2.Zero; QueueRedraw(); }

    public void Undo()
    {
        // First undo strokes, then stamps
        if (_strokes.Count > 0)     { _strokes.RemoveAt(_strokes.Count - 1); QueueRedraw(); return; }
        if (_placed.Count > 0)      { _placed.RemoveAt(_placed.Count - 1);   QueueRedraw(); }
    }

    public void Clear()
    {
        _strokes.Clear();
        _placed.Clear();
        _pendingStamp = null;
        QueueRedraw();
    }

    // ── Input ────────────────────────────────────────────────────────────────
    public void ForwardInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb)
        {
            var local = mb.Position;

            if (mb.ButtonIndex == MouseButton.WheelUp)
                { Zoom(+0.15f); Editor?.UpdateZoomLabel(); return; }
            if (mb.ButtonIndex == MouseButton.WheelDown)
                { Zoom(-0.15f); Editor?.UpdateZoomLabel(); return; }

            if (mb.ButtonIndex == MouseButton.Middle)
            {
                _panning = mb.Pressed;
                _panStart = local; _panOffsetStart = _panOffset;
                return;
            }

            if (mb.ButtonIndex == MouseButton.Left)
            {
                if (mb.Pressed)
                {
                    // Place pending stamp
                    if (_pendingStamp != null)
                    {
                        var worldPos = ScreenToWorld(local);
                        _placed.Add(new PlacedStamp { StampId = _pendingStamp.Id, Position = worldPos, Scale = 1f });
                        _pendingStamp = null;
                        Editor?._UpdateStatus("Stamp platziert. Wähle weiteres Objekt oder zeichne.");
                        QueueRedraw();
                        return;
                    }

                    // Check hit on existing stamp (drag)
                    var hit = HitTestStamp(local);
                    if (hit != null)
                    {
                        _dragging   = hit;
                        _dragOffset = ScreenToWorld(local) - hit.Position;
                        return;
                    }

                    // Freehand / shape draw
                    _drawingStroke = true;
                    _shapeStart    = ScreenToWorld(local);
                    _currentStroke = new DrawnStroke(_tool, _color, _width);
                    _currentStroke.Points.Add(_shapeStart);
                }
                else
                {
                    _dragging = null;
                    if (_drawingStroke)
                    {
                        _drawingStroke = false;
                        if (_currentStroke != null) { _strokes.Add(_currentStroke); _currentStroke = null; }
                        QueueRedraw();
                    }
                }
            }

            if (mb.ButtonIndex == MouseButton.Right && mb.Pressed)
            {
                // Right click = delete stamp under cursor
                var hit = HitTestStamp(mb.Position);
                if (hit != null) { _placed.Remove(hit); QueueRedraw(); }
            }
        }
        else if (@event is InputEventMouseMotion mm)
        {
            var local = mm.Position;
            if (_panning) { _panOffset = _panOffsetStart + (local - _panStart); QueueRedraw(); return; }

            if (_dragging != null)
            {
                _dragging.Position = ScreenToWorld(local) - _dragOffset;
                QueueRedraw();
                return;
            }

            if (_drawingStroke && _currentStroke != null)
            {
                var wp = ScreenToWorld(local);
                if (_tool == DrawTool.Freehand) _currentStroke.Points.Add(wp);
                else
                {
                    if (_currentStroke.Points.Count > 1) _currentStroke.Points.RemoveAt(_currentStroke.Points.Count - 1);
                    _currentStroke.Points.Add(wp);
                }
                QueueRedraw();
            }
        }
    }

    // ── Coordinate helpers ────────────────────────────────────────────────────
    private Vector2 WorldToScreen(Vector2 w) => w * ZoomLevel + Size / 2f + _panOffset;
    private Vector2 ScreenToWorld(Vector2 s) => (s - Size / 2f - _panOffset) / ZoomLevel;

    private PlacedStamp HitTestStamp(Vector2 screenPos)
    {
        for (int i = _placed.Count - 1; i >= 0; i--)
        {
            var ps   = _placed[i];
            var def  = StampLibrary.Get(ps.StampId);
            if (def == null) continue;
            var sp   = WorldToScreen(ps.Position);
            var sz   = def.DrawSize * ps.Scale * ZoomLevel;
            var rect = new Rect2(sp - sz / 2f, sz);
            if (rect.HasPoint(screenPos)) return ps;
        }
        return null;
    }

    // ── Draw ──────────────────────────────────────────────────────────────────
    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(0.04f, 0.06f, 0.16f));
        DrawGrid();

        // Placed stamps
        foreach (var ps in _placed)
            DrawStamp(ps);

        // Freehand strokes
        foreach (var s in _strokes) DrawStroke(s);
        if (_currentStroke != null)  DrawStroke(_currentStroke);

        // Pending stamp preview under mouse
        if (_pendingStamp != null)
        {
            var mouse = GetLocalMousePosition();
            DrawStampAt(_pendingStamp, mouse, 0.5f);
        }

        DrawNpcReference();

        DrawString(ThemeDB.FallbackFont, new Vector2(10, Size.Y - 12),
            $"Zoom {(int)(ZoomLevel*100)}%  |  Scroll=Zoom  MMB=Pan  RMB=Stamp löschen  Strg+Z=Rückgängig",
            HorizontalAlignment.Left, -1, 11, new Color(0.35f,0.4f,0.6f,0.8f));
    }

    private void DrawGrid()
    {
        float gs     = 32f * ZoomLevel;
        var   origin = Size / 2f + _panOffset;
        var   gc     = new Color(0.12f, 0.2f, 0.38f, 0.45f);
        for (float x = origin.X % gs; x < Size.X; x += gs)
            DrawLine(new Vector2(x,0), new Vector2(x, Size.Y), gc, 1f);
        for (float y = origin.Y % gs; y < Size.Y; y += gs)
            DrawLine(new Vector2(0,y), new Vector2(Size.X, y), gc, 1f);
        DrawLine(new Vector2(origin.X,0), new Vector2(origin.X, Size.Y), new Color(0.2f,0.35f,0.6f,0.4f), 1.5f);
        DrawLine(new Vector2(0,origin.Y), new Vector2(Size.X, origin.Y), new Color(0.2f,0.35f,0.6f,0.4f), 1.5f);
    }

    private void DrawStamp(PlacedStamp ps)
    {
        var def = StampLibrary.Get(ps.StampId);
        if (def == null) return;
        DrawStampAt(def, WorldToScreen(ps.Position), ps.Scale);
    }

    private void DrawStampAt(OracleStamp def, Vector2 screenPos, float alpha)
    {
        var sz      = def.DrawSize * ZoomLevel;
        var col     = new Color(def.Color, alpha);
        var rect    = new Rect2(screenPos - sz / 2f, sz);

        // Background highlight when hovered
        if (_dragging == null && HitTestStamp(screenPos) != null)
            DrawRect(new Rect2(rect.Position - new Vector2(2,2), rect.Size + new Vector2(4,4)),
                new Color(1f,1f,1f,0.12f));

        // Draw shape based on stamp type
        string glyph = GlyphOverrides.TryGetValue(def.Id, out var g) ? g : def.Glyph;

        switch (def.Id)
        {
            case "branch":
            case "bone":
            case "rope":
                // Horizontal bar with end nubs
                DrawLine(rect.Position + new Vector2(0, sz.Y/2),
                         rect.Position + new Vector2(sz.X, sz.Y/2), col, 3f * ZoomLevel);
                DrawCircle(rect.Position + new Vector2(4, sz.Y/2), 3*ZoomLevel, col);
                DrawCircle(rect.Position + new Vector2(sz.X-4, sz.Y/2), 3*ZoomLevel, col);
                break;
            case "stone":
                DrawArc(screenPos, sz.X/2.2f, 0, Mathf.Tau, 20, col, 2.5f * ZoomLevel);
                break;
            case "water":
                for (float wx = 0; wx < sz.X; wx += sz.X/3f)
                    DrawArc(rect.Position + new Vector2(wx + sz.X/6, sz.Y/2),
                        sz.X/6, 0, Mathf.Pi, 12, col, 2f * ZoomLevel);
                break;
            case "mountain":
                DrawLine(rect.Position + new Vector2(sz.X/2,0), rect.Position + new Vector2(0,sz.Y), col, 2.5f*ZoomLevel);
                DrawLine(rect.Position + new Vector2(sz.X/2,0), rect.Position + new Vector2(sz.X,sz.Y), col, 2.5f*ZoomLevel);
                DrawLine(rect.Position + new Vector2(0,sz.Y), rect.Position + new Vector2(sz.X,sz.Y), col, 2.5f*ZoomLevel);
                break;
            case "spear":
                DrawLine(screenPos + new Vector2(0,-sz.Y/2), screenPos + new Vector2(0,sz.Y/2), col, 2.5f*ZoomLevel);
                DrawLine(screenPos + new Vector2(0,-sz.Y/2), screenPos + new Vector2(-6*ZoomLevel, -sz.Y/2+12*ZoomLevel), col, 2f*ZoomLevel);
                DrawLine(screenPos + new Vector2(0,-sz.Y/2), screenPos + new Vector2(6*ZoomLevel,  -sz.Y/2+12*ZoomLevel), col, 2f*ZoomLevel);
                break;
            default:
                // Glyph fallback
                DrawString(ThemeDB.FallbackFont, screenPos - new Vector2(sz.X*0.35f, -sz.Y*0.3f),
                    glyph, HorizontalAlignment.Left, -1,
                    (int)(Mathf.Min(sz.X, sz.Y) * 0.9f), col);
                break;
        }

        // Label below stamp
        DrawString(ThemeDB.FallbackFont, screenPos + new Vector2(-20, sz.Y/2 + 14*ZoomLevel),
            def.Label, HorizontalAlignment.Left, -1, (int)(10*ZoomLevel),
            new Color(col, alpha * 0.7f));
    }

    private void DrawStroke(DrawnStroke s)
    {
        if (s.Points.Count < 1) return;
        switch (s.Tool)
        {
            case DrawTool.Freehand:
                for (int i = 1; i < s.Points.Count; i++)
                    DrawLine(WorldToScreen(s.Points[i-1]), WorldToScreen(s.Points[i]), s.Color, s.Width * ZoomLevel);
                break;
            case DrawTool.Line:
                if (s.Points.Count >= 2)
                    DrawLine(WorldToScreen(s.Points[0]), WorldToScreen(s.Points[^1]), s.Color, s.Width * ZoomLevel);
                break;
            case DrawTool.Rectangle:
                if (s.Points.Count >= 2)
                {
                    var p0 = WorldToScreen(s.Points[0]); var p1 = WorldToScreen(s.Points[^1]);
                    DrawRect(new Rect2(p0, p1-p0), s.Color, false, s.Width * ZoomLevel);
                }
                break;
            case DrawTool.Circle:
                if (s.Points.Count >= 2)
                    DrawArc(WorldToScreen(s.Points[0]),
                        WorldToScreen(s.Points[0]).DistanceTo(WorldToScreen(s.Points[^1])),
                        0, Mathf.Tau, 64, s.Color, s.Width * ZoomLevel);
                break;
            case DrawTool.Triangle:
                if (s.Points.Count >= 2)
                {
                    var p0 = WorldToScreen(s.Points[0]); var p1 = WorldToScreen(s.Points[^1]);
                    var top = new Vector2((p0.X+p1.X)*0.5f, p0.Y);
                    DrawLine(top, new Vector2(p0.X,p1.Y), s.Color, s.Width * ZoomLevel);
                    DrawLine(new Vector2(p0.X,p1.Y), p1, s.Color, s.Width * ZoomLevel);
                    DrawLine(p1, top, s.Color, s.Width * ZoomLevel);
                }
                break;
        }
    }

    private void DrawNpcReference()
    {
        float px = NpcHeightPx * ZoomLevel;
        float cx = 22f, yb = Size.Y - 50f, yt = yb - px;
        var col = new Color(1f, 0.8f, 0.4f, 0.65f);
        DrawArc(new Vector2(cx, yt+7f), 7f, 0, Mathf.Tau, 24, col, 1.5f);
        DrawLine(new Vector2(cx, yt+14f), new Vector2(cx, yt+px*0.55f), col, 1.5f);
        DrawLine(new Vector2(cx-8f, yt+px*0.3f), new Vector2(cx+8f, yt+px*0.3f), col, 1.5f);
        DrawLine(new Vector2(cx, yt+px*0.55f), new Vector2(cx-7f, yb), col, 1.5f);
        DrawLine(new Vector2(cx, yt+px*0.55f), new Vector2(cx+7f, yb), col, 1.5f);
        DrawLine(new Vector2(cx+14f, yt), new Vector2(cx+20f, yt), col, 1.5f);
        DrawLine(new Vector2(cx+17f, yt), new Vector2(cx+17f, yb), col, 1.5f);
        DrawLine(new Vector2(cx+14f, yb), new Vector2(cx+20f, yb), col, 1.5f);
        DrawString(ThemeDB.FallbackFont, new Vector2(cx+24f, yt+px*0.5f+6f),
            "NPC", HorizontalAlignment.Left, -1, 11, new Color(col, 0.7f));
    }
}
