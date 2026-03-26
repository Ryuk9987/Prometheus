#nullable disable
using Godot;

/// <summary>
/// Simple debug overlay — press TAB to toggle.
/// Shows knowledge spread across all NPCs in real time.
/// </summary>
public partial class DebugOverlay : CanvasLayer
{
    private RichTextLabel _label;
    private bool _visible = true;

    public override void _Ready()
    {
        _label = new RichTextLabel();
        _label.BbcodeEnabled = true;
        _label.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
        _label.Position = new Vector2(10, 10);
        _label.Size = new Vector2(420, 500);
        _label.AddThemeColorOverride("default_color", new Color(1, 1, 1, 0.9f));
        AddChild(_label);
    }

    public override void _Process(double delta)
    {
        if (Input.IsActionJustPressed("ui_focus_next")) // TAB
        {
            _visible = !_visible;
            _label.Visible = _visible;
        }

        if (!_visible || GameManager.Instance == null) return;

        var sb = new System.Text.StringBuilder();
        int tasks = TaskManager.Instance?.Tasks.Count ?? 0;
        sb.AppendLine("[b]PROMETHEUS — Debug[/b]");
        sb.AppendLine($"[color=gray]NPCs: {GameManager.Instance.AllNpcs.Count} | Tasks: {tasks} | TAB = toggle[/color]");
        sb.AppendLine("─────────────────────────────");

        foreach (var npc in GameManager.Instance.AllNpcs)
        {
            // Needs indicators
            string hungerCol = npc.Needs.IsStarving ? "red" : npc.Needs.IsHungry ? "orange" : "green";
            string thirstCol = npc.Needs.Thirst >= 0.95f ? "red" : npc.Needs.IsThirsty ? "orange" : "green";
            string beliefCol = npc.Belief.Belief >= 0.3f ? "cyan" : "gray";

            string role = npc.Cooperation.Role;
            string task = npc.Cooperation.HasTask ? "⚔" : "";
            sb.Append($"[color=yellow]{npc.NpcName}[/color][color=gray]({role}){task}[/color] ");
            sb.Append($"[color={hungerCol}]H:{npc.Needs.Hunger:F1}[/color] ");
            sb.Append($"[color={thirstCol}]T:{npc.Needs.Thirst:F1}[/color] ");
            sb.Append($"[color={beliefCol}]B:{npc.Belief.Belief:F1}[/color] ");

            foreach (var k in npc.Knowledge.Knowledge.Values)
            {
                string kCol = k.Depth > 0.7f ? "green" : k.Depth > 0.3f ? "orange" : "red";
                sb.Append($"[color={kCol}]{k.Id}({k.Depth:F1})[/color] ");
            }
            sb.AppendLine();
        }

        _label.Text = sb.ToString();
    }
}
