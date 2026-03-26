#nullable disable
using Godot;
using System.Collections.Generic;

/// <summary>
/// Full-screen Blueprint Editor.
/// Left: canvas with stamp placement + freehand drawing.
/// Right: tools, zoom, stamp palette (era-locked), colors.
/// </summary>
public partial class BlueprintEditor : CanvasLayer
{
    private Panel          _root;
    public  DrawCanvas     _drawCanvas;
    private Label          _zoomLabel;
    public  Label          _statusLabel;
    private bool           _open = false;
    private Panel          _saveDialog = null;

    public override void _Ready()
    {
        BuildLayout();
        _root.Visible = false;
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey k && k.Pressed && !k.Echo)
        {
            if (k.Keycode == Key.F && !_open)  { Open(); return; }
            if (k.Keycode == Key.F && _open)   { AskSaveAndClose(); return; }
            if (k.Keycode == Key.G && _open)   { _drawCanvas?.EnterGrabMode(); return; }
            if (k.Keycode == Key.Z && _open && k.CtrlPressed) { _drawCanvas?.Undo(); return; }

            if (k.Keycode == Key.Escape && _open)
            {
                // First let canvas handle ESC (cancel stamp / grab)
                if (_drawCanvas != null && _drawCanvas.ForwardInput(@event))
                    return; // canvas consumed it
                // Nothing active — ask save
                AskSaveAndClose();
                return;
            }
        }
        if (_open) _drawCanvas?.ForwardInput(@event);
    }

    private void AskSaveAndClose()
    {
        // If canvas is empty, close directly
        bool hasContent = (_drawCanvas?.GetPlacedStamps()?.Count > 0)
                       || (_drawCanvas?.GetStrokes()?.Count > 0);
        if (!hasContent) { Close(); return; }

        // Show save dialog
        if (_saveDialog != null) return; // already showing

        _saveDialog = new Panel();
        _saveDialog.SetAnchorsPreset(Control.LayoutPreset.Center);
        _saveDialog.Size = new Vector2(320, 120);
        _saveDialog.Position = new Vector2(-160, -60);

        var style = new StyleBoxFlat();
        style.BgColor      = new Color(0.1f, 0.12f, 0.2f, 0.98f);
        style.BorderColor  = new Color(0.4f, 0.7f, 1f);
        style.BorderWidthBottom = style.BorderWidthTop =
        style.BorderWidthLeft   = style.BorderWidthRight = 2;
        _saveDialog.AddThemeStyleboxOverride("panel", style);

        var vbox = new VBoxContainer();
        vbox.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        var margin = new MarginContainer();
        margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left",   16);
        margin.AddThemeConstantOverride("margin_right",  16);
        margin.AddThemeConstantOverride("margin_top",    14);
        margin.AddThemeConstantOverride("margin_bottom", 14);

        var lbl = new Label();
        lbl.Text = "Zeichnung ans Orakel-Tablet senden?";
        lbl.AutowrapMode = TextServer.AutowrapMode.Word;
        lbl.AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 1f));
        vbox.AddChild(lbl);

        vbox.AddChild(new HSeparator());

        var btnRow = new HBoxContainer();
        var saveBtn = new Button(); saveBtn.Text = "✓ Senden & Schließen";
        saveBtn.Pressed += () => { CloseSaveDialog(); Close(); };
        btnRow.AddChild(saveBtn);

        var discardBtn = new Button(); discardBtn.Text = "✕ Verwerfen";
        discardBtn.Pressed += () => { CloseSaveDialog(); CloseWithoutSave(); };
        btnRow.AddChild(discardBtn);

        var cancelBtn = new Button(); cancelBtn.Text = "Abbrechen";
        cancelBtn.Pressed += () => CloseSaveDialog();
        btnRow.AddChild(cancelBtn);

        vbox.AddChild(btnRow);
        margin.AddChild(vbox);
        _saveDialog.AddChild(margin);
        _root.AddChild(_saveDialog);
    }

    private void CloseSaveDialog()
    {
        _saveDialog?.QueueFree();
        _saveDialog = null;
    }

    private void CloseWithoutSave()
    {
        _drawCanvas?.Clear();
        OracleTablet.Instance?.ClearTablet();
        _open = false;
        _root.Visible = false;
    }

    private void ToggleOpen() { if (_open) Close(); else Open(); }

    private void Open()
    {
        _open = true;
        _root.Visible = true;
        _statusLabel.Text = "Wähle Objekte aus der Palette und platziere sie auf der Canvas.";
        if (OracleTablet.Instance != null && !OracleTablet.Instance.IsConnected(
            OracleTablet.SignalName.Interpretation,
            Callable.From<string,string,string>(OnInterpretation)))
            OracleTablet.Instance.Connect(OracleTablet.SignalName.Interpretation,
                Callable.From<string,string,string>(OnInterpretation));
    }

    private void Close()
    {
        _open = false;
        _root.Visible = false;
        if (_drawCanvas != null)
            OracleTablet.Instance?.SetComposition(_drawCanvas.GetPlacedStamps(), _drawCanvas.GetStrokes());
    }

    public void _UpdateStatus(string text) { if (_statusLabel != null) _statusLabel.Text = text; }

    private void OnInterpretation(string npc, string idea, string reason)
    {
        _statusLabel.Text = $"💭 {npc}: \"{idea}\"";
    }

    // ═══════════════════════════════════════════════════════════════════════
    private void BuildLayout()
    {
        _root = new Panel();
        _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        var bg = new StyleBoxFlat(); bg.BgColor = new Color(0.05f,0.07f,0.12f,0.96f);
        _root.AddThemeStyleboxOverride("panel", bg);

        // Top bar
        var topBar = new HBoxContainer();
        topBar.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        topBar.CustomMinimumSize = new Vector2(0, 40);
        topBar.AddChild(MakeLabel("🪨  Blueprint Editor", 18, new Color(0.5f,0.85f,1f)));
        topBar.AddChild(MakeSpacer());
        _statusLabel = MakeLabel("", 13, new Color(0.7f,0.9f,0.7f));
        _statusLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _statusLabel.AutowrapMode = TextServer.AutowrapMode.Word;
        topBar.AddChild(_statusLabel);
        topBar.AddChild(MakeSpacer());
        topBar.AddChild(MakeButton("✕ Schließen", Close));
        _root.AddChild(topBar);

        // Main HBox — fills the rest below the top bar
        var mainMargin = new MarginContainer();
        mainMargin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        mainMargin.AddThemeConstantOverride("margin_top", 44);
        _root.AddChild(mainMargin);

        var hbox = new HBoxContainer();
        hbox.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        hbox.AddThemeConstantOverride("separation", 0);
        mainMargin.AddChild(hbox);

        // Canvas
        _drawCanvas = new DrawCanvas();
        _drawCanvas.Editor = this;
        _drawCanvas.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _drawCanvas.SizeFlagsVertical   = Control.SizeFlags.ExpandFill;
        hbox.AddChild(_drawCanvas);

        // Right panel
        var rightPanel = new Panel();
        rightPanel.CustomMinimumSize = new Vector2(180, 0);
        rightPanel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        var rStyle = new StyleBoxFlat(); rStyle.BgColor = new Color(0.07f,0.09f,0.15f,1f);
        rightPanel.AddThemeStyleboxOverride("panel", rStyle);

        // scroll → margin → vbox  (correct hierarchy, no double-parent)
        var scroll = new ScrollContainer();
        scroll.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;

        var margin = new MarginContainer();
        margin.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        foreach (var side in new[]{"left","right","top","bottom"})
            margin.AddThemeConstantOverride($"margin_{side}", 8);

        var vbox = new VBoxContainer();
        vbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        vbox.AddThemeConstantOverride("separation", 4);

        margin.AddChild(vbox);
        scroll.AddChild(margin);
        rightPanel.AddChild(scroll);
        hbox.AddChild(rightPanel);

        // ── Drawing tools ──────────────────────────────────────────
        vbox.AddChild(MakeLabel("✏  Zeichenwerkzeuge", 13, new Color(0.5f,0.8f,1f)));
        vbox.AddChild(new HSeparator());
        var toolGrid = new GridContainer(); toolGrid.Columns = 2;
        AddToolBtn(toolGrid, "✏ Freihand",  DrawTool.Freehand);
        AddToolBtn(toolGrid, "╱ Linie",     DrawTool.Line);
        AddToolBtn(toolGrid, "□ Rechteck",  DrawTool.Rectangle);
        AddToolBtn(toolGrid, "○ Kreis",     DrawTool.Circle);
        AddToolBtn(toolGrid, "△ Dreieck",   DrawTool.Triangle);
        vbox.AddChild(toolGrid);

        var grabBtn = MakeButton("🖐 Greifen (G)", () => _drawCanvas?.EnterGrabMode());
        grabBtn.TooltipText = "Stamps verschieben & drehen (R)";
        vbox.AddChild(grabBtn);

        // ── Zoom ──────────────────────────────────────────────────
        vbox.AddChild(new HSeparator());
        vbox.AddChild(MakeLabel("🔍  Zoom", 12, new Color(0.55f,0.55f,0.55f)));
        var zRow = new HBoxContainer();
        zRow.AddChild(MakeButton("−", () => { _drawCanvas.Zoom(-0.25f); UpdateZoomLabel(); }));
        _zoomLabel = MakeLabel("100%", 12, new Color(0.9f,0.9f,0.9f));
        _zoomLabel.CustomMinimumSize = new Vector2(44, 0);
        _zoomLabel.HorizontalAlignment = HorizontalAlignment.Center;
        zRow.AddChild(_zoomLabel);
        zRow.AddChild(MakeButton("+", () => { _drawCanvas.Zoom(+0.25f); UpdateZoomLabel(); }));
        zRow.AddChild(MakeButton("⟳", () => { _drawCanvas.ResetZoom(); UpdateZoomLabel(); }));
        vbox.AddChild(zRow);

        // ── Colors ────────────────────────────────────────────────
        vbox.AddChild(new HSeparator());
        vbox.AddChild(MakeLabel("🎨  Farbe", 12, new Color(0.55f,0.55f,0.55f)));
        var cRow = new HBoxContainer();
        foreach (var c in new[]{
            new Color(0.4f,0.85f,1f), new Color(1f,0.9f,0.3f), new Color(0.3f,1f,0.5f),
            new Color(1f,0.4f,0.4f),  new Color(1f,1f,1f),     new Color(0.5f,0.5f,0.5f) })
        {
            var cr = new ColorRect(); cr.Color = c; cr.CustomMinimumSize = new Vector2(24,24);
            var cap = c; cr.GuiInput += (e) => { if (e is InputEventMouseButton mb && mb.Pressed) _drawCanvas.SetColor(cap); };
            cRow.AddChild(cr);
        }
        vbox.AddChild(cRow);

        // ── Widths ────────────────────────────────────────────────
        vbox.AddChild(new HSeparator());
        vbox.AddChild(MakeLabel("📏  Stärke", 12, new Color(0.55f,0.55f,0.55f)));
        var wRow = new HBoxContainer();
        foreach (float w in new[]{1f,2f,4f,7f,12f})
        { var cap=w; wRow.AddChild(MakeButton($"{w:F0}", () => _drawCanvas.SetWidth(cap))); }
        vbox.AddChild(wRow);

        // ── Stone Age Stamp Palette ───────────────────────────────
        vbox.AddChild(new HSeparator());
        vbox.AddChild(MakeLabel("🌿  Natur-Objekte (Steinzeit)", 13, new Color(0.5f,0.8f,1f)));
        vbox.AddChild(MakeLabel("Klicke → auf Canvas platzieren", 11, new Color(0.5f,0.5f,0.5f)));
        vbox.AddChild(MakeLabel("RMB auf Canvas = löschen", 11, new Color(0.5f,0.5f,0.5f)));

        var stampGrid = new GridContainer(); stampGrid.Columns = 2;
        foreach (var stamp in StampLibrary.GetAvailable(StampEra.Stone))
        {
            var btn = MakeButton($"{stamp.Glyph} {stamp.Label}", null);
            btn.TooltipText = $"Platziere '{stamp.Label}' auf der Canvas";
            var cap = stamp;
            btn.Pressed += () => _drawCanvas.SelectStampForPlacement(cap);
            stampGrid.AddChild(btn);
        }
        vbox.AddChild(stampGrid);

        // ── Edit ──────────────────────────────────────────────────
        vbox.AddChild(new HSeparator());
        var editRow = new HBoxContainer();
        var undoBtn = MakeButton("↩ Rückgängig", () => _drawCanvas?.Undo());
        undoBtn.TooltipText = "Strg+Z";
        editRow.AddChild(undoBtn);
        editRow.AddChild(MakeButton("✕ Leeren", () => {
            _drawCanvas.Clear();
            OracleTablet.Instance?.ClearTablet();
            _statusLabel.Text = "Canvas geleert.";
        }));
        vbox.AddChild(editRow);

        AddChild(_root);
    }

    private void AddToolBtn(GridContainer grid, string text, DrawTool tool)
    {
        var btn = MakeButton(text, null);
        btn.Pressed += () => { _drawCanvas.SetTool(tool); _statusLabel.Text = $"Werkzeug: {text.Trim()}"; };
        grid.AddChild(btn);
    }

    public void UpdateZoomLabel() { _zoomLabel.Text = $"{(int)(_drawCanvas.ZoomLevel*100)}%"; }

    private static Label MakeLabel(string text, int size, Color color)
    { var l = new Label(); l.Text=text; l.AddThemeFontSizeOverride("font_size",size); l.AddThemeColorOverride("font_color",color); return l; }
    private static Button MakeButton(string text, System.Action onPress)
    { var b = new Button(); b.Text=text; if (onPress!=null) b.Pressed+=onPress; return b; }
    private static Control MakeSpacer()
    { var s = new Control(); s.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; return s; }
}
