#nullable disable
using Godot;
using System.Collections.Generic;

public partial class InterpretationLog : CanvasLayer
{
	private VBoxContainer _log;
	private Panel         _panel;
	private bool          _visible   = true;
	private bool          _connected = false;
	private const int     MaxEntries = 10;

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
		_panel.Position = new Vector2(-420, -265);
		_panel.Size     = new Vector2(400, 250);

		var style = new StyleBoxFlat();
		style.BgColor = new Color(0.03f, 0.05f, 0.1f, 0.88f);
		_panel.AddThemeStyleboxOverride("panel", style);

		var margin = new MarginContainer();
		margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		margin.AddThemeConstantOverride("margin_left",   8);
		margin.AddThemeConstantOverride("margin_right",  8);
		margin.AddThemeConstantOverride("margin_top",    6);
		margin.AddThemeConstantOverride("margin_bottom", 6);

		var vbox = new VBoxContainer();

		var title = new Label();
		title.Text = "💭 NPC Interpretationen  [I]=toggle";
		title.AddThemeFontSizeOverride("font_size", 12);
		title.AddThemeColorOverride("font_color", new Color(0.5f, 0.8f, 1f));
		vbox.AddChild(title);
		vbox.AddChild(new HSeparator());

		_log = new VBoxContainer();
		_log.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		vbox.AddChild(_log);

		margin.AddChild(vbox);
		_panel.AddChild(margin);
		AddChild(_panel);

		SetProcess(true); // connect retry
	}

	// Retry connecting every frame until OracleTablet is ready
	public override void _Process(double delta)
	{
		if (_connected) return;
		if (OracleTablet.Instance == null) return;

		OracleTablet.Instance.Connect(
			OracleTablet.SignalName.Interpretation,
			Callable.From<string,string,string>(OnInterpretation));
		OracleTablet.Instance.Connect(
			OracleTablet.SignalName.KnowledgeTransferred,
			Callable.From<string,string,float>(OnKnowledgeTransferred));

		_connected = true;
		SetProcess(false); // no longer needed
		GD.Print("[InterpretationLog] Connected to OracleTablet.");
	}

	private void OnInterpretation(string npcName, string ideaLabel, string reasoning)
	{
		var short_r = reasoning != null && reasoning.Length > 70 ? reasoning[..70] + "…" : reasoning ?? "";
		AddEntry($"[color=yellow]{npcName}[/color] → [color=cyan]{ideaLabel}[/color]");
		AddEntry($"  [color=gray]{short_r}[/color]");
	}

	private void OnKnowledgeTransferred(string npcName, string ideaId, float depth)
	{
		string dot = depth > 0.6f ? "[color=green]●[/color]" : depth > 0.3f ? "[color=yellow]●[/color]" : "[color=red]●[/color]";
		AddEntry($"{dot} [color=white]{npcName}[/color] lernt [color=orange]{ideaId}[/color] ({depth:F2})");
	}

	private void AddEntry(string bbcode)
	{
		var lbl = new RichTextLabel();
		lbl.BbcodeEnabled = true;
		lbl.Text          = bbcode;
		lbl.CustomMinimumSize = new Vector2(0, 17);
		lbl.AddThemeFontSizeOverride("normal_font_size", 12);
		lbl.FitContent    = true;
		_log.AddChild(lbl);
		_entries.Enqueue(lbl);

		if (_entries.Count > MaxEntries)
			_entries.Dequeue().QueueFree();
	}
}
