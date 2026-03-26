#nullable disable
using Godot;

public partial class StrategyCameraController : Node3D
{
    [Export] public float PanSpeed   { get; set; } = 20f;
    [Export] public float ZoomSpeed  { get; set; } = 5f;
    [Export] public float MinZoom    { get; set; } = 5f;
    [Export] public float MaxZoom    { get; set; } = 80f;
    [Export] public float OrbitSpeed { get; set; } = 0.25f;

    private Camera3D _camera;
    private float    _zoomLevel = 30f;
    private bool     _orbiting  = false;
    private bool     _panning   = false;

    public override void _Ready()
    {
        _camera = GetNode<Camera3D>("Camera3D");
        UpdateCameraPosition();
    }

    public override void _Process(double delta)
    {
        var input = Vector3.Zero;
        if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up))    input.Z -= 1;
        if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down))  input.Z += 1;
        if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left))  input.X -= 1;
        if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right)) input.X += 1;

        if (input != Vector3.Zero)
        {
            CameraFollow.Instance?.StopFollow(); // WASD breaks follow
            Position += Transform.Basis * input.Normalized() * PanSpeed * (float)delta;
        }

        UpdateCameraPosition();
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb)
        {
            if (mb.ButtonIndex == MouseButton.WheelUp)
                _zoomLevel = Mathf.Clamp(_zoomLevel - ZoomSpeed, MinZoom, MaxZoom);
            else if (mb.ButtonIndex == MouseButton.WheelDown)
                _zoomLevel = Mathf.Clamp(_zoomLevel + ZoomSpeed, MinZoom, MaxZoom);
            else if (mb.ButtonIndex == MouseButton.Middle)
                _panning = mb.Pressed;
            else if (mb.ButtonIndex == MouseButton.Right)
                _orbiting = mb.Pressed;
        }

        if (@event is InputEventMouseMotion mm)
        {
            if (_orbiting)
            {
                CameraFollow.Instance?.StopFollow();
                RotateY(Mathf.DegToRad(-mm.Relative.X * OrbitSpeed));
            }
            if (_panning)
            {
                CameraFollow.Instance?.StopFollow();
                var panX = -mm.Relative.X * _zoomLevel * 0.001f * PanSpeed;
                var panZ = -mm.Relative.Y * _zoomLevel * 0.001f * PanSpeed;
                Position += Transform.Basis.X * panX + Transform.Basis.Z * panZ;
            }
        }
    }

    private void UpdateCameraPosition()
    {
        _camera.Position = new Vector3(0, _zoomLevel, _zoomLevel * 0.7f);
        _camera.LookAt(GlobalPosition, Vector3.Up);
    }
}
