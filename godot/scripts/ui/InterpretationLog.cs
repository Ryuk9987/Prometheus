#nullable disable
using Godot;
using System.Collections.Generic;

/// <summary>
/// Shows a live feed of NPC interpretations in the bottom-right corner.
/// "Arak sieht im Bild: Essen (Neugier zwingt ihn tiefer nachzudenken)"
/// </summary>
public partial class InterpretationLog : CanvasLayer
{
    private VBoxContainer _log;
    private Panel         _panel;
    private bool          _visible = true;
    private const int     MaxEntries = 8;

    private readonly Queue<RichTextLabel> _entries = new();

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey k && k.Pressed && !k.Echo && k.Keycode == Key.I)
        {
            _visible = !_visible;
            if (_panel != null) _panel.Visible = _visible;
        }
    }

    public override void _Ready()
    {
        _panel = new Panel();
        _panel.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
        _panel.Position = new Vector2(-420, -260);
        _panel.Size     = new Vector2(400, 240);

        var style = new StyleBoxFlat();
        style.BgColor = new Color(0.03f, 0.05f, 0.1f, 0.85f);
        _panel.AddThemeStyleboxOverride("panel", style);

        var margin = new MarginContainer();
        margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 8);
        margin.AddThemeConstantOverride("margin_right", 8);
        margin.AddThemeConstantOverride("margin_top", 6);
        margin.AddThemeConstantOverride("margin_bottom", 6);

        var vbox = new VBoxContainer();

        var title = new Label();
        title.Text = "💭 NPC Interpretationen";
        title.AddThemeFontSizeOverride("font_size", 13);
        title.AddThemeColorOverride("font_color", new Color(0.5f, 0.8f, 1f));
        vbox.AddChild(title);
        vbox.AddChild(new HSeparator());

        _log = new VBoxContainer();
        _log.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        vbox.AddChild(_log);

        margin.AddChild(vbox);
        _panel.AddChild(margin);
        AddChild(_panel);

        // Toggle hint
        var hint = new Label();
        hint.Text = "[I] ein/ausblenden";
        hint.AddThemeFontSizeOverride("font_size", 10);
        hint.AddThemeColorOverride("font_color", new Color(0.35f, 0.35f, 0.5f));
        hint.Position = new Vector2(_panel.Position.X, _panel.Position.Y - 16);
        // note: positioned relative to CanvasLayer, approximate
        _panel.AddChild(hint);

        // Connect to OracleTablet
        CallDeferred(MethodName.ConnectSignals);
    }

    private void ConnectSignals()
    {
        if (OracleTablet.Instance == null) return;
        OracleTablet.Instance.Connect(
            OracleTablet.SignalName.Interpretation,
            Callable.From<string,string,string>(OnInterpretation));
        OracleTablet.Instance.Connect(
            OracleTablet.SignalName.KnowledgeTransferred,
            Callable.From<string,string,float>(OnKnowledgeTransferred));
    }

    private void OnInterpretation(string npcName, string ideaLabel, string reasoning)
    {
        AddEntry($"[color=yellow]{npcName}[/color] sieht: [color=cyan]{ideaLabel}[/color]", true);
        // Show short reasoning snippet
        var short_r = reasoning.Length > 60 ? reasoning[..60] + "…" : reasoning;
        AddEntry($"  [color=gray]{short_r}[/color]", true);
    }

    private void OnKnowledgeTransferred(string npcName, string ideaId, float depth)
    {
        string depthStr = depth > 0.6f ? "🟢" : depth > 0.3f ? "🟡" : "🔴";
        AddEntry($"{depthStr} [color=white]{npcName}[/color] lernt [color=orange]{ideaId}[/color] ({depth:F2})", true);
    }

    private void AddEntry(string bbcode, bool useBbcode)
    {
        var lbl = new RichTextLabel();
        lbl.BbcodeEnabled = true;
        lbl.Text = bbcode;
        lbl.CustomMinimumSize = new Vector2(0, 18);
        lbl.AddThemeFontSizeOverride("normal_font_size", 12);
        lbl.FitContent = true;
        _log.AddChild(lbl);
        _entries.Enqueue(lbl);

        if (_entries.Count > MaxEntries)
        {
            var old = _entries.Dequeue();
            old.QueueFree();
        }
    }
}
