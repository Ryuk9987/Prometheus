#nullable disable
using Godot;
using System.Collections.Generic;
using System.Linq;

public enum CanvasMode { Stamp, Grab, Draw }

/// <summary>
/// Blueprint canvas with:
/// - Stamp placement: preview follows mouse exactly, Shift = place again
/// - Grab mode (G): drag + rotate (R key while grabbed) placed stamps
/// - Freehand / shape drawing
/// - Zoom (scroll) + Pan (MMB)
/// </summary>
public partial class DrawCanvas : Control
{
    // ── Zoom / Pan ────────────────────────────────────────────────────────
    public float    ZoomLevel    { get; private set; } = 1f;
    private Vector2 _panOffset   = Vector2.Zero;
    private bool    _panning     = false;
    private Vector2 _panStart;
    private Vector2 _panOffsetStart;

    // ── Mode ──────────────────────────────────────────────────────────────
    private CanvasMode   _mode          = CanvasMode.Draw;
    private OracleStamp  _pendingStamp  = null;  // stamp attached to cursor

    // ── Placed stamps ─────────────────────────────────────────────────────
    private readonly List<PlacedStamp> _placed = new();

    // ── Grab state ────────────────────────────────────────────────────────
    private PlacedStamp _grabbed        = null;
    private Vector2     _grabOffset     = Vector2.Zero;
    private bool        _rotating       = false;
    private Vector2     _rotateOrigin;

    // ── Draw strokes ──────────────────────────────────────────────────────
    private readonly List<DrawnStroke> _strokes = new();
    private DrawnStroke _currentStroke  = null;
    private bool        _drawingStroke  = false;

    // ── Draw tool ─────────────────────────────────────────────────────────
    private DrawTool _tool  = DrawTool.Freehand;
    private Color    _color = new Color(0.4f, 0.85f, 1f);
    private float    _width = 3f;

    public BlueprintEditor Editor { get; set; }
    private const float NpcHeightPx = 60f;

    // ── Public API ────────────────────────────────────────────────────────
    public void SetTool(DrawTool t)  { _tool = t; _mode = CanvasMode.Draw; }
    public void SetColor(Color c)    { _color = c; }
    public void SetWidth(float w)    { _width = w; }
    public List<DrawnStroke>  GetStrokes()      => new(_strokes);
    public List<PlacedStamp>  GetPlacedStamps() => new(_placed);

    public void SelectStampForPlacement(OracleStamp stamp)
    {
        _pendingStamp = stamp;
        _mode = CanvasMode.Stamp;
        _grabbed = null;
        Editor?._UpdateStatus($"📍 '{stamp.Label}' — Linksklick = platzieren  |  Shift+Klick = mehrfach  |  ESC = abbrechen");
    }

    public void EnterGrabMode()
    {
        _pendingStamp = null;
        _mode = CanvasMode.Grab;
        Editor?._UpdateStatus("🖐 Grab-Modus: Linksklick = greifen & ziehen  |  R = drehen  |  ESC = beenden");
    }

    public void Zoom(float delta)
    {
        ZoomLevel = Mathf.Clamp(ZoomLevel + delta, 0.1f, 8f);
        QueueRedraw();
    }
    public void ResetZoom() { ZoomLevel = 1f; _panOffset = Vector2.Zero; QueueRedraw(); }

    public void Undo()
    {
        if (_strokes.Count > 0)     { _strokes.RemoveAt(_strokes.Count-1); QueueRedraw(); return; }
        if (_placed.Count > 0)      { _placed.RemoveAt(_placed.Count-1);   QueueRedraw(); }
    }

    public void Clear()
    {
        _strokes.Clear(); _placed.Clear();
        _pendingStamp = null; _grabbed = null;
        _mode = CanvasMode.Draw;
        QueueRedraw();
    }

    // ── Ready ─────────────────────────────────────────────────────────────
    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        FocusMode   = FocusModeEnum.All;
    }

    // ── Input ─────────────────────────────────────────────────────────────
    public void ForwardInput(InputEvent @event)
    {
        if (@event is InputEventKey k && k.Pressed && !k.Echo)
        {
            if (k.Keycode == Key.Escape)
            {
                _pendingStamp = null; _grabbed = null;
                _mode = CanvasMode.Draw;
                Editor?._UpdateStatus("Modus: Zeichnen");
                QueueRedraw(); return;
            }
            // R = rotate grabbed stamp
            if (k.Keycode == Key.R && _grabbed != null)
            {
                _grabbed.Rotation += Mathf.Pi / 8f; // 22.5° steps
                QueueRedraw(); return;
            }
            if (k.Keycode == Key.Z && k.CtrlPressed) { Undo(); return; }
        }

        if (@event is InputEventMouseButton mb)
        {
            // ── Zoom ──────────────────────────────────────────────────
            if (mb.ButtonIndex == MouseButton.WheelUp)
                { Zoom(+0.15f); Editor?.UpdateZoomLabel(); return; }
            if (mb.ButtonIndex == MouseButton.WheelDown)
                { Zoom(-0.15f); Editor?.UpdateZoomLabel(); return; }

            // ── Pan ───────────────────────────────────────────────────
            if (mb.ButtonIndex == MouseButton.Middle)
            {
                _panning = mb.Pressed;
                _panStart = mb.Position; _panOffsetStart = _panOffset; return;
            }

            // ── Right click = delete stamp ─────────────────────────────
            if (mb.ButtonIndex == MouseButton.Right && mb.Pressed)
            {
                var hit = HitTest(mb.Position);
                if (hit != null) { _placed.Remove(hit); QueueRedraw(); }
                return;
            }

            // ── Left click ─────────────────────────────────────────────
            if (mb.ButtonIndex == MouseButton.Left)
            {
                if (mb.Pressed)
                {
                    switch (_mode)
                    {
                        case CanvasMode.Stamp:
                            PlaceStamp(mb.Position, mb.ShiftPressed);
                            return;

                        case CanvasMode.Grab:
                            var hit = HitTest(mb.Position);
                            if (hit != null)
                            {
                                _grabbed   = hit;
                                _grabOffset = ScreenToWorld(mb.Position) - hit.Position;
                            }
                            return;

                        case CanvasMode.Draw:
                            _drawingStroke = true;
                            _currentStroke = new DrawnStroke(_tool, _color, _width);
                            _currentStroke.Points.Add(ScreenToWorld(mb.Position));
                            return;
                    }
                }
                else // released
                {
                    _grabbed = null;
                    if (_drawingStroke)
                    {
                        _drawingStroke = false;
                        if (_currentStroke != null) { _strokes.Add(_currentStroke); _currentStroke = null; }
                        QueueRedraw();
                    }
                }
            }
        }

        if (@event is InputEventMouseMotion mm)
        {
            if (_panning)
            {
                _panOffset = _panOffsetStart + (mm.Position - _panStart);
                QueueRedraw(); return;
            }

            if (_grabbed != null)
            {
                _grabbed.Position = ScreenToWorld(mm.Position) - _grabOffset;
                QueueRedraw(); return;
            }

            if (_drawingStroke && _currentStroke != null)
            {
                var wp = ScreenToWorld(mm.Position);
                if (_tool == DrawTool.Freehand) _currentStroke.Points.Add(wp);
                else
                {
                    if (_currentStroke.Points.Count > 1) _currentStroke.Points.RemoveAt(_currentStroke.Points.Count-1);
                    _currentStroke.Points.Add(wp);
                }
                QueueRedraw(); return;
            }

            // Redraw for cursor preview
            if (_pendingStamp != null) QueueRedraw();
        }
    }

    private void PlaceStamp(Vector2 screenPos, bool shift)
    {
        if (_pendingStamp == null) return;
        var world = ScreenToWorld(screenPos);
        _placed.Add(new PlacedStamp { StampId = _pendingStamp.Id, Position = world, Scale = 1f });
        QueueRedraw();

        if (!shift)
        {
            _pendingStamp = null;
            _mode = CanvasMode.Draw;
            Editor?._UpdateStatus("Stamp platziert.");
        }
        // else keep pending for another placement
    }

    // ── Coordinate helpers ─────────────────────────────────────────────────
    private Vector2 WorldToScreen(Vector2 w) => w * ZoomLevel + Size / 2f + _panOffset;
    private Vector2 ScreenToWorld(Vector2 s) => (s - Size / 2f - _panOffset) / ZoomLevel;

    private PlacedStamp HitTest(Vector2 screenPos)
    {
        for (int i = _placed.Count-1; i >= 0; i--)
        {
            var ps  = _placed[i];
            var def = StampLibrary.Get(ps.StampId);
            if (def == null) continue;
            var sp  = WorldToScreen(ps.Position);
            var sz  = def.DrawSize * ps.Scale * ZoomLevel * 1.3f; // slightly larger hit box
            if (new Rect2(sp - sz/2f, sz).HasPoint(screenPos)) return ps;
        }
        return null;
    }

    // ── Draw ───────────────────────────────────────────────────────────────
    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(0.04f, 0.06f, 0.16f));
        DrawGrid();

        // Placed stamps
        foreach (var ps in _placed)
            DrawStampWorld(ps, ps == _grabbed ? 0.6f : 1f);

        // Freehand
        foreach (var s in _strokes) DrawStroke(s);
        if (_currentStroke != null) DrawStroke(_currentStroke);

        // Cursor-attached stamp preview
        if (_pendingStamp != null)
        {
            var mouse = GetLocalMousePosition();
            DrawStampAtScreen(_pendingStamp, mouse, 0.45f, 0f);
            // Cross-hair at exact placement point
            DrawLine(mouse + new Vector2(-10,0), mouse + new Vector2(10,0), new Color(1f,1f,0.3f,0.9f), 1.5f);
            DrawLine(mouse + new Vector2(0,-10), mouse + new Vector2(0,10), new Color(1f,1f,0.3f,0.9f), 1.5f);
        }

        // Grab mode: highlight hovered stamp
        if (_mode == CanvasMode.Grab)
        {
            var hover = HitTest(GetLocalMousePosition());
            if (hover != null)
            {
                var sp = WorldToScreen(hover.Position);
                var def = StampLibrary.Get(hover.StampId);
                if (def != null)
                {
                    var sz = def.DrawSize * hover.Scale * ZoomLevel * 1.4f;
                    DrawRect(new Rect2(sp - sz/2f, sz), new Color(1f,0.8f,0.2f,0.35f), true);
                    DrawRect(new Rect2(sp - sz/2f, sz), new Color(1f,0.8f,0.2f,0.8f), false, 1.5f);
                }
            }
        }

        DrawNpcReference();
        DrawHints();
    }

    private void DrawGrid()
    {
        float gs     = 32f * ZoomLevel;
        var   origin = Size/2f + _panOffset;
        var   gc     = new Color(0.12f,0.2f,0.38f,0.45f);
        for (float x = origin.X % gs; x < Size.X; x += gs)
            DrawLine(new Vector2(x,0), new Vector2(x,Size.Y), gc, 1f);
        for (float y = origin.Y % gs; y < Size.Y; y += gs)
            DrawLine(new Vector2(0,y), new Vector2(Size.X,y), gc, 1f);
        DrawLine(new Vector2(origin.X,0), new Vector2(origin.X,Size.Y), new Color(0.2f,0.35f,0.6f,0.4f), 1.5f);
        DrawLine(new Vector2(0,origin.Y), new Vector2(Size.X,origin.Y), new Color(0.2f,0.35f,0.6f,0.4f), 1.5f);
    }

    private void DrawStampWorld(PlacedStamp ps, float alpha)
    {
        var def = StampLibrary.Get(ps.StampId);
        if (def == null) return;
        DrawStampAtScreen(def, WorldToScreen(ps.Position), alpha, ps.Rotation);
    }

    private void DrawStampAtScreen(OracleStamp def, Vector2 center, float alpha, float rotation)
    {
        var sz  = def.DrawSize * ZoomLevel;
        var col = new Color(def.Color, alpha);

        // Apply rotation via Transform2D
        var xf = Transform2D.Identity.Rotated(rotation).Translated(center);
        DrawSetTransformMatrix(xf);

        var halfSz = sz / 2f;

        switch (def.Id)
        {
            case "branch": case "rope": case "bone":
                DrawLine(new Vector2(-sz.X/2,0), new Vector2(sz.X/2,0), col, 3f*ZoomLevel);
                DrawCircle(new Vector2(-sz.X/2+4,0), 3*ZoomLevel, col);
                DrawCircle(new Vector2(sz.X/2-4,0),  3*ZoomLevel, col);
                if (def.Id == "branch")
                {
                    DrawLine(new Vector2(-sz.X/4,-8*ZoomLevel), new Vector2(-sz.X/6,0), col, 1.5f*ZoomLevel);
                    DrawLine(new Vector2(sz.X/6,-8*ZoomLevel),  new Vector2(sz.X/4,0),  col, 1.5f*ZoomLevel);
                }
                break;
            case "stone":
                DrawArc(Vector2.Zero, sz.X/2.2f, 0, Mathf.Tau, 24, col, 2.5f*ZoomLevel);
                DrawArc(Vector2.Zero, sz.X/3f,   0, Mathf.Tau, 24, new Color(col,alpha*0.3f), 1f*ZoomLevel);
                break;
            case "water":
                for (int wi = 0; wi < 3; wi++)
                {
                    float ox = (wi - 1) * sz.X/3f;
                    DrawArc(new Vector2(ox, 0), sz.X/6f, Mathf.Pi, Mathf.Tau, 12, col, 2f*ZoomLevel);
                }
                break;
            case "mountain":
                DrawLine(new Vector2(0,-sz.Y/2),   new Vector2(-sz.X/2,sz.Y/2), col, 2.5f*ZoomLevel);
                DrawLine(new Vector2(0,-sz.Y/2),   new Vector2(sz.X/2,sz.Y/2),  col, 2.5f*ZoomLevel);
                DrawLine(new Vector2(-sz.X/2,sz.Y/2), new Vector2(sz.X/2,sz.Y/2), col, 2.5f*ZoomLevel);
                break;
            case "spear":
                DrawLine(new Vector2(0,-sz.Y/2), new Vector2(0,sz.Y/2), col, 2.5f*ZoomLevel);
                DrawLine(new Vector2(0,-sz.Y/2), new Vector2(-6*ZoomLevel,-sz.Y/2+12*ZoomLevel), col, 2f*ZoomLevel);
                DrawLine(new Vector2(0,-sz.Y/2), new Vector2(6*ZoomLevel,-sz.Y/2+12*ZoomLevel),  col, 2f*ZoomLevel);
                break;
            case "fire":
                DrawArc(new Vector2(0, sz.Y/4), sz.X/3f, Mathf.Pi, Mathf.Tau, 16, new Color(1f,0.5f,0.1f,alpha), 2.5f*ZoomLevel);
                DrawLine(new Vector2(0,-sz.Y/2), new Vector2(0,sz.Y/4), new Color(1f,0.8f,0.1f,alpha), 2f*ZoomLevel);
                DrawLine(new Vector2(-sz.X/4,-sz.Y/4), new Vector2(sz.X/4,-sz.Y/4), new Color(1f,0.5f,0.1f,alpha*0.7f), 1.5f*ZoomLevel);
                break;
            case "sun":
                DrawArc(Vector2.Zero, sz.X/3f, 0, Mathf.Tau, 24, col, 2.5f*ZoomLevel);
                for (int ri = 0; ri < 8; ri++)
                {
                    float a = ri * Mathf.Pi/4f;
                    var from = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * sz.X/3f;
                    var to   = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * sz.X/2f;
                    DrawLine(from, to, col, 1.5f*ZoomLevel);
                }
                break;
            case "animal":
                // Simple quadruped silhouette
                DrawArc(new Vector2(-sz.X/4,-sz.Y/4), sz.X/5f, 0, Mathf.Tau, 16, col, 2f*ZoomLevel); // head
                DrawLine(new Vector2(-sz.X/4,-sz.Y/4), new Vector2(sz.X/4,0), col, 2f*ZoomLevel);     // body
                DrawLine(new Vector2(-sz.X/4,0), new Vector2(-sz.X/4,sz.Y/2), col, 1.5f*ZoomLevel);   // front leg
                DrawLine(new Vector2(sz.X/4,0),  new Vector2(sz.X/4,sz.Y/2),  col, 1.5f*ZoomLevel);   // back leg
                break;
            default:
                // Glyph fallback
                DrawString(ThemeDB.FallbackFont,
                    new Vector2(-sz.X*0.35f, sz.Y*0.3f),
                    def.Glyph, HorizontalAlignment.Left, -1,
                    (int)(Mathf.Min(sz.X,sz.Y)*0.85f), col);
                break;
        }

        // Reset transform
        DrawSetTransformMatrix(Transform2D.Identity);

        // Label below (no rotation)
        DrawString(ThemeDB.FallbackFont,
            center + new Vector2(-18, sz.Y/2 + 13*ZoomLevel),
            def.Label, HorizontalAlignment.Left, -1,
            (int)(10*ZoomLevel), new Color(def.Color, alpha*0.75f));

        // Rotation indicator when grabbed
        if (_grabbed != null && _grabbed == FindPlacedByCenter(center))
            DrawString(ThemeDB.FallbackFont, center + new Vector2(-10, -sz.Y/2 - 14),
                "[R]=drehen", HorizontalAlignment.Left, -1, 11, new Color(1f,0.9f,0.3f,0.85f));
    }

    private PlacedStamp FindPlacedByCenter(Vector2 screen)
    {
        foreach (var ps in _placed)
            if (WorldToScreen(ps.Position).DistanceTo(screen) < 4f) return ps;
        return null;
    }

    private void DrawStroke(DrawnStroke s)
    {
        if (s.Points.Count < 1) return;
        switch (s.Tool)
        {
            case DrawTool.Freehand:
                for (int i = 1; i < s.Points.Count; i++)
                    DrawLine(WorldToScreen(s.Points[i-1]), WorldToScreen(s.Points[i]), s.Color, s.Width*ZoomLevel);
                break;
            case DrawTool.Line:
                if (s.Points.Count >= 2)
                    DrawLine(WorldToScreen(s.Points[0]), WorldToScreen(s.Points[^1]), s.Color, s.Width*ZoomLevel);
                break;
            case DrawTool.Rectangle:
                if (s.Points.Count >= 2)
                {
                    var p0 = WorldToScreen(s.Points[0]); var p1 = WorldToScreen(s.Points[^1]);
                    DrawRect(new Rect2(p0, p1-p0), s.Color, false, s.Width*ZoomLevel);
                }
                break;
            case DrawTool.Circle:
                if (s.Points.Count >= 2)
                    DrawArc(WorldToScreen(s.Points[0]),
                        WorldToScreen(s.Points[0]).DistanceTo(WorldToScreen(s.Points[^1])),
                        0, Mathf.Tau, 64, s.Color, s.Width*ZoomLevel);
                break;
            case DrawTool.Triangle:
                if (s.Points.Count >= 2)
                {
                    var p0 = WorldToScreen(s.Points[0]); var p1 = WorldToScreen(s.Points[^1]);
                    var top = new Vector2((p0.X+p1.X)*0.5f, p0.Y);
                    DrawLine(top, new Vector2(p0.X,p1.Y), s.Color, s.Width*ZoomLevel);
                    DrawLine(new Vector2(p0.X,p1.Y), p1, s.Color, s.Width*ZoomLevel);
                    DrawLine(p1, top, s.Color, s.Width*ZoomLevel);
                }
                break;
        }
    }

    private void DrawNpcReference()
    {
        float px = NpcHeightPx * ZoomLevel;
        float cx = 22f, yb = Size.Y-50f, yt = yb-px;
        var col = new Color(1f,0.8f,0.4f,0.6f);
        DrawArc(new Vector2(cx,yt+7f), 7f, 0, Mathf.Tau, 24, col, 1.5f);
        DrawLine(new Vector2(cx,yt+14f), new Vector2(cx,yt+px*0.55f), col, 1.5f);
        DrawLine(new Vector2(cx-8f,yt+px*0.3f), new Vector2(cx+8f,yt+px*0.3f), col, 1.5f);
        DrawLine(new Vector2(cx,yt+px*0.55f), new Vector2(cx-7f,yb), col, 1.5f);
        DrawLine(new Vector2(cx,yt+px*0.55f), new Vector2(cx+7f,yb), col, 1.5f);
        DrawLine(new Vector2(cx+14f,yt), new Vector2(cx+20f,yt), col, 1.5f);
        DrawLine(new Vector2(cx+17f,yt), new Vector2(cx+17f,yb), col, 1.5f);
        DrawLine(new Vector2(cx+14f,yb), new Vector2(cx+20f,yb), col, 1.5f);
        DrawString(ThemeDB.FallbackFont, new Vector2(cx+24f,yt+px*0.5f+6f),
            "NPC", HorizontalAlignment.Left, -1, 11, new Color(col,0.7f));
    }

    private void DrawHints()
    {
        string hint = _mode switch {
            CanvasMode.Stamp => "Shift+Klick = mehrfach platzieren  |  ESC = abbrechen",
            CanvasMode.Grab  => "Ziehen = verschieben  |  R = 22.5° drehen  |  ESC = beenden",
            _                => "Scroll=Zoom  MMB=Pan  Strg+Z=Rückgängig  RMB=Stamp löschen"
        };
        DrawString(ThemeDB.FallbackFont, new Vector2(10, Size.Y-12),
            $"Zoom {(int)(ZoomLevel*100)}%  |  {hint}",
            HorizontalAlignment.Left, -1, 11, new Color(0.35f,0.4f,0.6f,0.8f));
    }
}
