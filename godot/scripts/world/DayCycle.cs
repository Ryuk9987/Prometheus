#nullable disable
using Godot;

/// <summary>
/// 24-hour day/night cycle.
/// Controls sun direction, sky color, ambient light.
/// Emits signals for dawn/dusk so NPCs can react.
/// Default: 1 real minute = 1 game hour (configurable).
/// </summary>
public partial class DayCycle : Node
{
    public static DayCycle Instance { get; private set; }

    [Export] public float MinutesPerHour   { get; set; } = 10f;  // 10 real min = 1 game hour → 1 day = 4h real
    [Export] public float StartHour        { get; set; } = 6f;   // start at dawn

    public float   Hour        { get; private set; }
    public bool    IsDay       => Hour >= 6f && Hour < 20f;
    public bool    IsNight     => !IsDay;
    public string  TimeString  => $"{(int)Hour:D2}:{(int)((Hour % 1f) * 60):D2}";

    [Signal] public delegate void DawnEventHandler();
    [Signal] public delegate void DuskEventHandler();
    [Signal] public delegate void HourChangedEventHandler(float hour);

    private DirectionalLight3D _sun;
    private WorldEnvironment   _env;
    private float              _lastHour;
    private bool               _wasDayLastFrame = true;

    public override void _Ready()
    {
        Instance = this;
        Hour     = StartHour;
        _lastHour = Hour;
        _sun = GetTree().Root.FindChild("Sun", true, false) as DirectionalLight3D;
        _env = GetTree().Root.FindChild("WorldEnvironment", true, false) as WorldEnvironment;
        GD.Print($"[DayCycle] Started. {MinutesPerHour} real minutes = 1 game hour (1 day = {MinutesPerHour * 24 / 60f:F1}h real time).");
    }

    public override void _Process(double delta)
    {
        float gameHoursPerSecond = 1f / (MinutesPerHour * 60f);
        Hour = (Hour + (float)delta * gameHoursPerSecond * 24f) % 24f;

        // Hour changed signal
        if ((int)Hour != (int)_lastHour)
            EmitSignal(SignalName.HourChanged, Hour);
        _lastHour = Hour;

        // Day/night transitions
        bool isDay = IsDay;
        if (isDay && !_wasDayLastFrame)  EmitSignal(SignalName.Dawn);
        if (!isDay && _wasDayLastFrame)  EmitSignal(SignalName.Dusk);
        _wasDayLastFrame = isDay;

        UpdateLighting();
    }

    private void UpdateLighting()
    {
        // Sun angle: 0° at noon, rises east, sets west
        float sunAngle = ((Hour - 6f) / 12f) * Mathf.Pi; // 0..π over day
        float elevation = Mathf.Sin(sunAngle);            // 0→1→0

        if (_sun != null)
        {
            float pitch = Mathf.Lerp(-10f, -170f, Hour / 24f);
            _sun.RotationDegrees = new Vector3(pitch, -30f, 0f);
            _sun.LightEnergy = IsDay ? Mathf.Clamp(elevation * 1.4f, 0.05f, 1.4f) : 0.02f;

            // Color: warm dawn/dusk, white noon, dark night
            _sun.LightColor = Hour switch {
                _ when Hour < 7f  => new Color(1f,   0.6f, 0.3f),  // dawn
                _ when Hour < 10f => new Color(1f,   0.85f,0.7f),  // morning
                _ when Hour < 17f => new Color(1f,   0.98f,0.95f), // noon
                _ when Hour < 20f => new Color(1f,   0.65f,0.3f),  // dusk
                _                 => new Color(0.3f, 0.4f, 0.7f),  // night
            };
        }

        if (_env?.Environment != null)
        {
            var env = _env.Environment;
            // Sky colors
            float t = Mathf.Clamp((Hour - 6f) / 14f, 0f, 1f);
            float nt = IsNight ? 1f : 0f;

            if (env.Sky?.SkyMaterial is ProceduralSkyMaterial sky)
            {
                sky.SkyTopColor      = IsDay
                    ? new Color(0.2f + elevation*0.1f, 0.4f + elevation*0.15f, 0.8f + elevation*0.1f)
                    : new Color(0.02f, 0.03f, 0.08f);
                sky.SkyHorizonColor  = IsDay
                    ? new Color(0.6f + (1f-elevation)*0.2f, 0.7f, 0.85f)
                    : new Color(0.05f, 0.06f, 0.12f);
                sky.GroundBottomColor = new Color(0.15f, 0.12f, 0.08f);
            }

            env.AmbientLightEnergy = IsDay
                ? Mathf.Clamp(0.2f + elevation * 0.4f, 0.1f, 0.6f)
                : 0.05f;
        }
    }
}
