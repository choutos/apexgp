using System.Collections.Generic;
using System.Linq;
using Godot;
using ApexGP.Core;

namespace ApexGP.Game;

/// <summary>
/// Game shell root: builds the world (ground, sky, light), spawns the player car,
/// drives the chase camera and the HUD. All sim state lives in ApexGP.Core.
/// </summary>
public partial class Main : Node3D
{
    private CarController _player = null!;
    private readonly List<CarController> _carNodes = new();
    private RaceController _race = null!;
    private Camera3D _camera = null!;
    private Label _speedLabel = null!;
    private Label _gearLabel = null!;
    private Label _rpmLabel = null!;
    private Label _lapLabel = null!;
    private Label _posLabel = null!;
    private Label _messageLabel = null!;
    private string _lastMessage = "";

    public override void _Ready()
    {
        BuildWorld();

        // Grid: player P1, then two AI cars
        var aiFast = new RacingLineFollower(Track) { SpeedFactor = 0.94 };
        var aiSlow = new RacingLineFollower(Track) { SpeedFactor = 0.88 };
        _race = new RaceController(Track, laps: 2);
        var p = _race.AddRacer("YOU", VehicleConfig.PrototypeGtB(), isPlayer: true, ai: null, lateralOffset: 3.0);
        var a1 = _race.AddRacer("K. Tanaka", VehicleConfig.PrototypeGtB(), isPlayer: false, ai: aiFast, lateralOffset: -3.0);
        var a2 = _race.AddRacer("M. Kowalski", VehicleConfig.PrototypeGtB(), isPlayer: false, ai: aiSlow, lateralOffset: 3.0);

        Spawn(p, new Color(0.78f, 0.16f, 0.12f));
        Spawn(a1, new Color(0.16f, 0.38f, 0.82f));
        Spawn(a2, new Color(0.88f, 0.88f, 0.86f));

        _camera = new Camera3D { Fov = 68f, Current = true };
        AddChild(_camera);

        BuildHud();
    }

    private void Spawn(Racer r, Color paint)
    {
        var node = new CarController().WithVehicle(r.Car).WithPaint(paint);
        node.ExternalControl = true;
        node.UseKeyboard = r.IsPlayer; // set before _Ready so input/smoke hooks bind correctly
        AddChild(node);
        if (r.IsPlayer)
            _player = node;
        _carNodes.Add(node);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_race.Phase is RacePhase.Formation or RacePhase.Racing)
        {
            _player.PollInput();
            _race.Step(delta, (_player.ThrottleInput, _player.BrakeInput, _player.SteerInput));
        }
        else
        {
            // freeze inputs after the flag
            _player.ThrottleInput = 0;
            _player.BrakeInput = 0.6;
            _race.Step(delta, (_player.ThrottleInput, _player.BrakeInput, 0));
        }
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
                FogDensity = 0.0009f,
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

        // Grass everywhere; the track ribbon sits on top
        var ground = new MeshInstance3D
        {
            Mesh = new PlaneMesh
            {
                Size = new Vector2(900f, 900f),
            },
        };
        ground.MaterialOverride = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.30f, 0.40f, 0.28f),
            Roughness = 0.95f,
        };
        ground.Position = new Vector3(150f, 0f, -120f);
        AddChild(ground);

        // Track (Core geometry → visuals) and register it as the driving surface
        Track = TrackCatalog.SunriseLoop();
        AddChild(TrackMesh.Build(Track));
    }

    public TrackSurface Track = null!;

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

        _lapLabel = new Label
        {
            Position = new Vector2(24f, 132f),
            LabelSettings = new LabelSettings
            {
                Font = font,
                FontSize = 20,
                FontColor = new Color(0.9f, 0.95f, 1f),
                OutlineSize = 4,
                OutlineColor = new Color(0.05f, 0.1f, 0.22f),
            },
        };
        layer.AddChild(_lapLabel);

        _posLabel = new Label
        {
            Position = new Vector2(24f, 162f),
            LabelSettings = new LabelSettings
            {
                Font = font,
                FontSize = 34,
                FontColor = new Color(1f, 0.95f, 0.6f),
                OutlineSize = 6,
                OutlineColor = new Color(0.05f, 0.1f, 0.22f),
            },
        };
        layer.AddChild(_posLabel);

        _messageLabel = new Label
        {
            // centered top
            AnchorLeft = 0f, AnchorRight = 1f, AnchorTop = 0.14f, AnchorBottom = 0.14f,
            HorizontalAlignment = HorizontalAlignment.Center,
            LabelSettings = new LabelSettings
            {
                Font = font,
                FontSize = 52,
                FontColor = new Color(1f, 0.9f, 0.25f),
                OutlineSize = 10,
                OutlineColor = new Color(0.05f, 0.08f, 0.2f),
            },
        };
        layer.AddChild(_messageLabel);
    }

    public override void _Process(double delta)
    {
        // Chase camera: behind and above the player car, smoothed.
        var carPos = _player.GlobalPosition;
        var back = -_player.GlobalTransform.Basis.Z; // car forward is -Z
        var target = carPos - back * 5.2f + Vector3.Up * 2.3f;
        var k = 1f - Mathf.Exp((float)-(6.0 * delta));
        _camera.GlobalPosition = _camera.GlobalPosition.Lerp(target, k);
        _camera.LookAt(carPos + Vector3.Up * 0.9f, Vector3.Up);

        var sim = _player.Sim;
        var racer = _race.Racers.First(r => r.IsPlayer);
        _speedLabel.Text = $"{sim.SpeedKmh:F0} km/h";
        _gearLabel.Text = sim.Gear >= 1 ? $"Gear {sim.Gear}   {sim.Drivetrain.Clutch}" : "N";
        var rpm = sim.EngineRpm / sim.Config.Drivetrain.RedlineRpm;
        var bar = new string('█', (int)(rpm * 20)).PadRight(20, '·');
        _rpmLabel.Text = $"{bar} {sim.EngineRpm:F0} rpm";
        _lapLabel.Text = racer.Laps == 0
            ? $"Lap 1 — {racer.Timer.CurrentLapTime:F2}s"
            : $"Lap {racer.Laps + 1} — {racer.Timer.CurrentLapTime:F2}s   (best {racer.Timer.BestLap:F2}s)";
        _posLabel.Text = $"P{racer.Position}/{_race.Racers.Count}";

        string msg = _race.Phase switch
        {
            RacePhase.Formation => "GET READY...",
            RacePhase.Racing when racer.Laps < _race.Laps => "",
            _ => racer.Position <= 3 ? $"FINISH — P{racer.Position}!" : "FINISH"
        };
        if (msg != _lastMessage)
        {
            _messageLabel.Text = msg;
            _lastMessage = msg;
        }
    }
}
