#nullable disable
using Godot;

/// <summary>
/// If the Oracle Tablet has content and the NPC is a believer,
/// the NPC will periodically walk to the tablet to receive knowledge.
/// Priority: below Survival, above Cooperation.
/// </summary>
public partial class TabletSeekBehavior : Node
{
    private NpcEntity _owner;
    private double    _checkTimer   = 0;
    private double    _seekCooldown = 0;
    private bool      _seeking      = false;
    private const double CheckInterval  = 5.0;
    private const double SeekCooldown   = 20.0; // don't revisit too often
    private const float  MoveSpeed      = 0.9f;
    private const float  ReadRange      = 5.5f;

    public override void _Ready()
    {
        _owner = GetParent<NpcEntity>();
        // Stagger check timer so not all NPCs check at same frame
        _checkTimer = GD.RandRange(0.0, CheckInterval);
    }

    /// <summary>Force the NPC to seek the tablet immediately (called on flash).</summary>
    public void ForceSeek()
    {
        _seeking      = true;
        _seekCooldown = 0;
        _checkTimer   = CheckInterval; // skip wait
    }

    /// <summary>Returns true if this behavior is active (moving to / reading tablet).</summary>
    public bool Tick(double delta)
    {
        if (OracleTablet.Instance == null || !OracleTablet.Instance.HasContent) return false;
        if (!_owner.Belief.CanHearOracle) return false;

        _seekCooldown -= delta;
        if (_seekCooldown > 0 && !_seeking) return false;

        _checkTimer += delta;
        if (!_seeking && _checkTimer < CheckInterval) return false;

        // Decide to seek
        if (!_seeking)
        {
            _checkTimer = 0;
            // Curiosity check — more curious = more likely to investigate
            if (GD.Randf() > _owner.Personality.Curiosity * 0.8f) return false;
            _seeking = true;
        }

        // Move toward tablet
        var tabletPos = OracleTablet.Instance.GlobalPosition;
        var dir = tabletPos - _owner.GlobalPosition;
        dir.Y = 0;
        float dist = dir.Length();

        if (dist > ReadRange)
        {
            _owner.GlobalPosition += dir.Normalized() * MoveSpeed * (float)delta;
            return true;
        }

        // In range — tablet does the knowledge transfer passively via OracleTablet._Process
        // Just stand here for a moment
        _seekCooldown = SeekCooldown;
        _seeking = false;
        return false;
    }
}
