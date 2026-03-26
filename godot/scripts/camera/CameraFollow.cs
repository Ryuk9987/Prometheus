#nullable disable
using Godot;

/// <summary>
/// Attaches to the CameraRig and smoothly follows a target NPC.
/// Called from DebugOverlay when player clicks an NPC name.
/// </summary>
public partial class CameraFollow : Node
{
    public static CameraFollow Instance { get; private set; }

    private Node3D     _cameraRig;
    private NpcEntity  _target;
    private bool       _following = false;

    [Export] public float FollowSpeed { get; set; } = 5f;

    public override void _Ready()
    {
        Instance   = this;
        _cameraRig = GetParent<Node3D>();
    }

    public void Follow(NpcEntity npc)
    {
        _target    = npc;
        _following = npc != null;
        GD.Print(_following
            ? $"[Camera] Now following: {npc.NpcName}"
            : "[Camera] Follow stopped.");
    }

    public void StopFollow() => Follow(null);

    public override void _Process(double delta)
    {
        if (!_following || _target == null || !IsInstanceValid(_target))
        {
            _following = false; return;
        }
        var targetPos = new Vector3(_target.GlobalPosition.X, 0, _target.GlobalPosition.Z);
        _cameraRig.GlobalPosition = _cameraRig.GlobalPosition.Lerp(
            targetPos, FollowSpeed * (float)delta);
    }
}
