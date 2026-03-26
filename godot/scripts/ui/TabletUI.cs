#nullable disable
using Godot;

/// <summary>
/// The player UI for interacting with the Oracle Tablet.
/// Press F near the tablet to open. Shows blueprint selector + draw mode toggle.
/// </summary>
public partial class TabletUI : CanvasLayer
{
    private Panel         _panel;
    private Label         _titleLabel;
    private Label         _statusLabel;
    private VBoxContainer _blueprintList;
    private Button        _drawModeBtn;
    private Button        _clearBtn;
    private bool          _open = false;

    // For draw mode — forward mouse input to canvas
    private bool _drawMode = false;

    public override void _Ready()
    {
        BuildUI();
        _panel.Visible = false;
    }

    public override void _Input(InputEvent @event)
    {
        // F = open/close tablet
        if (@event is InputEventKey key && key.Pressed && !key.Echo)
        {
            if (key.Keycode == Key.F)
            {
                if (OracleTablet.Instance == null) return;
                _open = !_open;
                _panel.Visible = _open;
                _drawMode = false;
                return;
            }
            if (key.Keycode == Key.Escape && _open)
            {
                _open = false;
                _panel.Visible = false;
                return;
            }
        }

        // Forward mouse to canvas in draw mode
        if (_open && _drawMode && OracleTablet.Instance != null)
        {
            // Map screen pos to canvas area (approx — refine with actual rect)
            if (@event is InputEventMouseButton or InputEventMouseMotion)
            {
                var canvas = GetCanvasNode();
                if (canvas != null)
                {
                    Vector2 mousePos = _panel.GetLocalMousePosition();
                    canvas.HandleInput(@event, mousePos);
                }
            }
        }
    }

    private void BuildUI()
    {
        _panel = new Panel();
        _panel.SetAnchorsPreset(Control.LayoutPreset.CenterRight);
        _panel.Position = new Vector2(-380, -300);
        _panel.Size     = new Vector2(360, 600);

        var margin = new MarginContainer();
        margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_top",    14);
        margin.AddThemeConstantOverride("margin_left",   14);
        margin.AddThemeConstantOverride("margin_right",  14);
        margin.AddThemeConstantOverride("margin_bottom", 14);

        var vbox = new VBoxContainer();
        margin.AddChild(vbox);
        _panel.AddChild(margin);

        _titleLabel = new Label();
        _titleLabel.Text = "🪨 Das Orakel-Tablet";
        _titleLabel.AddThemeFontSizeOverride("font_size", 20);
        _titleLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.8f, 1f));
        vbox.AddChild(_titleLabel);

        _statusLabel = new Label();
        _statusLabel.Text = "Wähle eine Blaupause oder zeichne frei.";
        _statusLabel.AutowrapMode = TextServer.AutowrapMode.Word;
        _statusLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.9f, 0.7f));
        vbox.AddChild(_statusLabel);

        vbox.AddChild(new HSeparator());

        // Mode buttons
        var modeRow = new HBoxContainer();
        var bpBtn = new Button();
        bpBtn.Text = "📐 Blaupause";
        bpBtn.Pressed += () => SwitchToBlueprint();
        modeRow.AddChild(bpBtn);

        _drawModeBtn = new Button();
        _drawModeBtn.Text = "✏ Freihand";
        _drawModeBtn.Pressed += () => SwitchToDrawMode();
        modeRow.AddChild(_drawModeBtn);

        _clearBtn = new Button();
        _clearBtn.Text = "✕ Leeren";
        _clearBtn.Pressed += () => {
            OracleTablet.Instance?.ClearTablet();
            _drawMode = false;
            _statusLabel.Text = "Tablet geleert.";
        };
        modeRow.AddChild(_clearBtn);
        vbox.AddChild(modeRow);

        vbox.AddChild(new HSeparator());

        // Blueprint list
        var bpLabel = new Label();
        bpLabel.Text = "Blaupausen:";
        bpLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
        vbox.AddChild(bpLabel);

        var scroll = new ScrollContainer();
        scroll.CustomMinimumSize = new Vector2(0, 320);
        _blueprintList = new VBoxContainer();
        scroll.AddChild(_blueprintList);
        vbox.AddChild(scroll);

        PopulateBlueprints();

        var hint = new Label();
        hint.Text = "[F] Schließen  [ESC] Schließen";
        hint.AddThemeColorOverride("font_color", new Color(0.4f, 0.4f, 0.4f));
        hint.AddThemeFontSizeOverride("font_size", 13);
        vbox.AddChild(hint);

        AddChild(_panel);
    }

    private void PopulateBlueprints()
    {
        foreach (var idea in OracleManager.Ideas.Values)
        {
            var btn = new Button();
            btn.Text        = idea.DisplayName;
            btn.TooltipText = idea.Flavor;
            string capturedId = idea.Id;
            btn.Pressed += () => SelectBlueprint(capturedId, idea.DisplayName, idea.Flavor);
            _blueprintList.AddChild(btn);
        }
    }

    private void SelectBlueprint(string id, string name, string flavor)
    {
        OracleTablet.Instance?.SetBlueprint(id);
        _drawMode = false;
        _statusLabel.Text = $"📐 {name}\n\"{flavor}\"\n\nNPC's in der Nähe werden lernen.";
    }

    private void SwitchToBlueprint()
    {
        _drawMode = false;
        _statusLabel.Text = "Wähle eine Blaupause aus der Liste.";
    }

    private void SwitchToDrawMode()
    {
        OracleTablet.Instance?.SetDrawMode();
        _drawMode = true;
        _statusLabel.Text = "✏ Freihand aktiv — zeichne auf das Tablet.";
    }

    private TabletCanvas GetCanvasNode()
    {
        return OracleTablet.Instance?
            .GetNodeOrNull<SubViewport>("SubViewport")?
            .GetNodeOrNull<TabletCanvas>("TabletCanvas");
    }
}
