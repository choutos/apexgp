using Godot;

namespace ApexGP.Game;

/// <summary>
/// Game shell root: builds the world (ground, sky, light), spawns the player car,
/// drives the chase camera and the HUD. All sim state lives in ApexGP.Core.
/// </summary>
public partial class Main : Node3D
{
    private CarController _car = null!;
    private Camera3D _camera = null!;
    private Label _speedLabel = null!;
    private Label _gearLabel = null!;
    private Label _rpmLabel = null!;

    public override void _Ready()
    {
        BuildWorld();
        _car = new CarController();
        AddChild(_car);

        _camera = new Camera3D
        {
            Fov = 68f,
            Current = true,
        };
        AddChild(_camera);

        BuildHud();
    }

    private void BuildWorld()
    {
        // Sky
        var env = new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = new Color(0.35f, 0.52f, 0.72f),
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color(0.55f, 0.6f, 0.68f),
                AmbientLightEnergy = 0.55f,
                FogEnabled = true,
                FogLightColor = new Color(0.62f, 0.71f, 0.82f),
                FogDensity = 0.0012f,
            },
        };
        AddChild(env);

        // Sun
        var sun = new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-42f, 35f, 0f),
            LightEnergy = 1.15f,
            ShadowEnabled = true,
        };
        AddChild(sun);

        // Ground (flat world; tracks arrive in M1)
        var ground = new MeshInstance3D
        {
            Mesh = new PlaneMesh
            {
                Size = new Vector2(400f, 400f),
                SubdivideDepth = 1,
                SubdivideWidth = 1,
            },
        };
        var groundMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.30f, 0.40f, 0.28f),
            Roughness = 0.95f,
        };
        ground.MaterialOverride = groundMat;
        AddChild(ground);
    }

    private void BuildHud()
    {
        var layer = new CanvasLayer();
        AddChild(layer);

        var font = ThemeDB.FallbackFont;
        _speedLabel = new Label
        {
            Position = new Vector2(24f, 24f),
            LabelSettings = new LabelSettings
            {
                Font = font,
                FontSize = 42,
                FontColor = Colors.White,
                OutlineSize = 6,
                OutlineColor = new Color(0.05f, 0.1f, 0.22f),
            },
        };
        layer.AddChild(_speedLabel);

        _gearLabel = new Label
        {
            Position = new Vector2(24f, 72f),
            LabelSettings = new LabelSettings
            {
                Font = font,
                FontSize = 24,
                FontColor = new Color(0.85f, 0.9f, 1f),
                OutlineSize = 4,
                OutlineColor = new Color(0.05f, 0.1f, 0.22f),
            },
        };
        layer.AddChild(_gearLabel);

        _rpmLabel = new Label
        {
            Position = new Vector2(24f, 104f),
            LabelSettings = new LabelSettings
            {
                Font = font,
                FontSize = 18,
                FontColor = new Color(1f, 0.75f, 0.3f),
                OutlineSize = 4,
                OutlineColor = new Color(0.05f, 0.1f, 0.22f),
            },
        };
        layer.AddChild(_rpmLabel);
    }

    public override void _Process(double delta)
    {
        // Chase camera: behind and above the car, smoothed.
        var carPos = _car.GlobalPosition;
        var back = -_car.GlobalTransform.Basis.Z; // car forward is -Z
        var target = carPos - back * 5.2f + Vector3.Up * 2.3f;
        var k = 1f - Mathf.Exp((float)-(6.0 * delta));
        _camera.GlobalPosition = _camera.GlobalPosition.Lerp(target, k);
        _camera.LookAt(carPos + Vector3.Up * 0.9f, Vector3.Up);

        var sim = _car.Sim;
        _speedLabel.Text = $"{sim.SpeedKmh:F0} km/h";
        _gearLabel.Text = sim.Gear >= 1 ? $"Gear {sim.Gear}   {sim.Drivetrain.Clutch}" : "N";
        var rpm = sim.EngineRpm / sim.Config.Drivetrain.RedlineRpm;
        var bar = new string('█', (int)(rpm * 20)).PadRight(20, '·');
        _rpmLabel.Text = $"{bar} {sim.EngineRpm:F0} rpm";
    }
}
