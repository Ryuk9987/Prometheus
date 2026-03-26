#nullable disable
using Godot;
using System.Collections.Generic;

public enum DrawTool { Freehand, Line, Rectangle, Circle, Triangle }

/// <summary>
/// Full-screen Blueprint Editor — opens with F near the Oracle Tablet.
/// Features: freehand, shapes, zoom/pan, NPC size reference, predefined stamps.
/// </summary>
public partial class BlueprintEditor : CanvasLayer
{
    // ── Layout ──────────────────────────────────────────────────────────────
    private Panel          _root;
    private DrawCanvas     _drawCanvas;
    private Panel          _toolPanel;
    private Label          _zoomLabel;
    private Label          _statusLabel;
    private bool           _open = false;

    // ── State ────────────────────────────────────────────────────────────────
    private DrawTool       _activeTool   = DrawTool.Freehand;
    private Color          _penColor     = new Color(0.4f, 0.85f, 1f);
    private float          _penWidth     = 3f;

    public override void _Ready()
    {
        BuildLayout();
        _root.Visible = false;
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey k && k.Pressed && !k.Echo)
        {
            if (k.Keycode == Key.F)               { ToggleOpen(); return; }
            if (k.Keycode == Key.Escape && _open) { Close(); return; }
            if (_open && k.Keycode == Key.Z && k.CtrlPressed)
            {
                _drawCanvas?.Undo();
                return;
            }
        }
        if (_open) _drawCanvas?.ForwardInput(@event);
    }

    private void ToggleOpen() { if (_open) Close(); else Open(); }
    private void Open()
    {
        _open = true;
        _root.Visible = true;
        if (_statusLabel != null)
            _statusLabel.Text = "Zeichne etwas — NPCs interpretieren es wenn du schließt. [Strg+Z = Rückgängig]";
        // Subscribe to interpretation events
        if (OracleTablet.Instance != null && !OracleTablet.Instance.IsConnected(
            OracleTablet.SignalName.Interpretation,
            Callable.From<string,string,string>(OnInterpretation)))
        {
            OracleTablet.Instance.Connect(
                OracleTablet.SignalName.Interpretation,
                Callable.From<string,string,string>(OnInterpretation));
        }
    }
    private void Close()
    {
        _open = false;
        _root.Visible = false;
        // Submit current drawing to OracleTablet
        OracleTablet.Instance?.SetDrawing(_drawCanvas.GetStrokes());
    }

    private void OnInterpretation(string npcName, string ideaLabel, string reasoning)
    {
        if (_statusLabel != null)
            _statusLabel.Text = $"💭 {npcName}: \"{ideaLabel}\"\n{reasoning}";
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  UI BUILD
    // ═══════════════════════════════════════════════════════════════════════
    private void BuildLayout()
    {
        _root = new Panel();
        _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        // Semi-transparent dark background
        var bg = new StyleBoxFlat();
        bg.BgColor = new Color(0.05f, 0.07f, 0.12f, 0.95f);
        _root.AddThemeStyleboxOverride("panel", bg);

        // ── Top bar ────────────────────────────────────────────────────
        var topBar = new HBoxContainer();
        topBar.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        topBar.CustomMinimumSize = new Vector2(0, 40);
        topBar.Position = new Vector2(0, 0);

        var titleLbl = MakeLabel("🪨  Blueprint Editor", 18, new Color(0.5f, 0.85f, 1f));
        topBar.AddChild(titleLbl);
        topBar.AddChild(MakeSpacer());

        _statusLabel = MakeLabel("Werkzeug: Freihand", 14, new Color(0.7f, 0.9f, 0.7f));
        topBar.AddChild(_statusLabel);
        topBar.AddChild(MakeSpacer());

        var closeBtn = MakeButton("✕ Schließen", Close);
        topBar.AddChild(closeBtn);
        _root.AddChild(topBar);

        // ── Main area (canvas + right panel) ──────────────────────────
        var hbox = new HBoxContainer();
        hbox.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        hbox.AddThemeConstantOverride("separation", 0);
        hbox.Position = new Vector2(0, 44);
        hbox.Size     = new Vector2(0, -44);
        _root.AddChild(hbox);

        // Draw canvas (left, expands)
        _drawCanvas = new DrawCanvas();
        _drawCanvas.Editor = this;
        _drawCanvas.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _drawCanvas.SizeFlagsVertical   = Control.SizeFlags.ExpandFill;
        hbox.AddChild(_drawCanvas);

        // Right tool panel (fixed 240px)
        _toolPanel = new Panel();
        _toolPanel.CustomMinimumSize = new Vector2(240, 0);
        _toolPanel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        var panelStyle = new StyleBoxFlat();
        panelStyle.BgColor = new Color(0.08f, 0.1f, 0.16f, 1f);
        _toolPanel.AddThemeStyleboxOverride("panel", panelStyle);

        var toolVBox = new VBoxContainer();
        toolVBox.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        toolVBox.AddThemeConstantOverride("separation", 4);
        var toolMargin = new MarginContainer();
        toolMargin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        toolMargin.AddThemeConstantOverride("margin_left",   8);
        toolMargin.AddThemeConstantOverride("margin_right",  8);
        toolMargin.AddThemeConstantOverride("margin_top",    8);
        toolMargin.AddThemeConstantOverride("margin_bottom", 8);
        toolMargin.AddChild(toolVBox);
        _toolPanel.AddChild(toolMargin);
        hbox.AddChild(_toolPanel);

        // ── Tools section ─────────────────────────────────────────────
        toolVBox.AddChild(MakeLabel("✏  Werkzeuge", 14, new Color(0.5f, 0.8f, 1f)));
        toolVBox.AddChild(new HSeparator());

        var toolGrid = new GridContainer();
        toolGrid.Columns = 2;
        AddToolButton(toolGrid, "✏ Freihand",  DrawTool.Freehand);
        AddToolButton(toolGrid, "╱ Linie",     DrawTool.Line);
        AddToolButton(toolGrid, "□ Rechteck",  DrawTool.Rectangle);
        AddToolButton(toolGrid, "○ Kreis",     DrawTool.Circle);
        AddToolButton(toolGrid, "△ Dreieck",   DrawTool.Triangle);
        toolVBox.AddChild(toolGrid);

        // ── Zoom ─────────────────────────────────────────────────────
        toolVBox.AddChild(new HSeparator());
        toolVBox.AddChild(MakeLabel("🔍  Zoom", 13, new Color(0.6f, 0.6f, 0.6f)));
        var zoomRow = new HBoxContainer();
        zoomRow.AddChild(MakeButton("−", () => { _drawCanvas.Zoom(-0.25f); UpdateZoomLabel(); }));
        _zoomLabel = MakeLabel("100%", 13, new Color(0.9f, 0.9f, 0.9f));
        _zoomLabel.CustomMinimumSize = new Vector2(50, 0);
        _zoomLabel.HorizontalAlignment = HorizontalAlignment.Center;
        zoomRow.AddChild(_zoomLabel);
        zoomRow.AddChild(MakeButton("+", () => { _drawCanvas.Zoom(+0.25f); UpdateZoomLabel(); }));
        zoomRow.AddChild(MakeButton("⟳", () => { _drawCanvas.ResetZoom(); UpdateZoomLabel(); }));
        toolVBox.AddChild(zoomRow);

        // ── Color picks ───────────────────────────────────────────────
        toolVBox.AddChild(new HSeparator());
        toolVBox.AddChild(MakeLabel("🎨  Farbe", 13, new Color(0.6f, 0.6f, 0.6f)));
        var colorRow = new HBoxContainer();
        var colors = new[] {
            new Color(0.4f,0.85f,1f), new Color(1f,0.9f,0.3f),
            new Color(0.3f,1f,0.5f),  new Color(1f,0.4f,0.4f),
            new Color(1f,1f,1f),      new Color(0.5f,0.5f,0.5f)
        };
        foreach (var c in colors)
        {
            var cb = new ColorRect();
            cb.Color = c;
            cb.CustomMinimumSize = new Vector2(26, 26);
            var cap = c;
            cb.GuiInput += (e) => { if (e is InputEventMouseButton mb && mb.Pressed) SetColor(cap); };
            colorRow.AddChild(cb);
        }
        toolVBox.AddChild(colorRow);

        // ── Strichbreite ─────────────────────────────────────────────
        toolVBox.AddChild(new HSeparator());
        toolVBox.AddChild(MakeLabel("📏  Breite", 13, new Color(0.6f,0.6f,0.6f)));
        var widthRow = new HBoxContainer();
        foreach (float w in new[] { 1f, 2f, 4f, 7f, 12f })
        {
            var wb = MakeButton($"{w:F0}px", () => { _penWidth = w; });
            widthRow.AddChild(wb);
        }
        toolVBox.AddChild(widthRow);

        // ── Blaupausen ───────────────────────────────────────────────
        toolVBox.AddChild(new HSeparator());
        toolVBox.AddChild(MakeLabel("📐  Blaupausen", 14, new Color(0.5f, 0.8f, 1f)));
        var scroll = new ScrollContainer();
        scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        var bpList = new VBoxContainer();
        foreach (var idea in OracleManager.Ideas.Values)
        {
            var btn = MakeButton(idea.DisplayName, null);
            string capturedId   = idea.Id;
            string capturedName = idea.DisplayName;
            btn.Pressed += () => {
                OracleTablet.Instance?.SetBlueprint(capturedId);
                _drawCanvas.StampBlueprint(capturedId, capturedName);
                _statusLabel.Text = $"Blaupause: {capturedName}";
            };
            bpList.AddChild(btn);
        }
        scroll.AddChild(bpList);
        toolVBox.AddChild(scroll);

        // ── Undo / Clear ────────────────────────────────────────────
        toolVBox.AddChild(new HSeparator());
        var editRow = new HBoxContainer();
        var undoBtn = MakeButton("↩ Rückgängig", () => {
            _drawCanvas?.Undo();
            _statusLabel.Text = "Letzter Strich entfernt.";
        });
        undoBtn.TooltipText = "Strg+Z";
        editRow.AddChild(undoBtn);
        editRow.AddChild(MakeButton("✕ Löschen", () => {
            _drawCanvas.Clear();
            OracleTablet.Instance?.ClearTablet();
            _statusLabel.Text = "Canvas geleert.";
        }));
        toolVBox.AddChild(editRow);

        AddChild(_root);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────
    private void AddToolButton(GridContainer grid, string text, DrawTool tool)
    {
        var btn = MakeButton(text, null);
        btn.Pressed += () => {
            _activeTool = tool;
            _drawCanvas.SetTool(tool);
            _statusLabel.Text = $"Werkzeug: {text.Trim()}";
            OracleTablet.Instance?.SetDrawMode();
        };
        grid.AddChild(btn);
    }

    private void SetColor(Color c)
    {
        _penColor = c;
        _drawCanvas.SetColor(c);
    }

    public void UpdateZoomLabel()
    {
        _zoomLabel.Text = $"{(int)(_drawCanvas.ZoomLevel * 100)}%";
    }

    private static Label MakeLabel(string text, int size, Color color)
    {
        var l = new Label();
        l.Text = text;
        l.AddThemeFontSizeOverride("font_size", size);
        l.AddThemeColorOverride("font_color", color);
        return l;
    }

    private static Button MakeButton(string text, System.Action onPress)
    {
        var b = new Button();
        b.Text = text;
        if (onPress != null) b.Pressed += onPress;
        return b;
    }

    private static Control MakeSpacer()
    {
        var s = new Control();
        s.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        return s;
    }
}
