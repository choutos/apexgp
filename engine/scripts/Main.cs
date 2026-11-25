using System.Collections.Generic;
using System.Linq;
using Godot;
using ApexGP.Core;

namespace ApexGP.Game;

/// <summary>
/// Game shell root: owns the world (track visuals, sky), the race lifecycle, the
/// chase camera and the HUD. All sim/game state lives in ApexGP.Core.
/// </summary>
public partial class Main : Node3D
{
    private CarController _player = null!;
    private readonly List<CarController> _carNodes = new();
    private RaceController? _race;
    private EventDef? _activeEvent;
    private bool _resultsShown;
    private CareerUi? _careerUi;
    private CareerProfile _profile = null!;

    private LicenseRun? _licenceRun;
    private Vehicle? _licenceCar;
    private LicenseTestDef? _licenceDef;
    private double _licAccum;
    private double _raceTimerOffset;

    private Camera3D _camera = null!;
    private Label _speedLabel = null!;
    private Label _gearLabel = null!;
    private Label _rpmLabel = null!;
    private Label _lapLabel = null!;
    private Label _posLabel = null!;
    private Label _messageLabel = null!;
    private string _lastMessage = "";
    private readonly bool _smoke = false;

    public TrackSurface Track = null!;
    public SubViewport WorldViewport = null!;
    private bool _hudVisible = true;

    public override void _Ready()
    {
        // PS1-style: render the 3D world into a 480×270 SubViewport, upscaled with
        // nearest-neighbour filtering. HUD/career UI stays native-res above it.
        var vp = new SubViewport
        {
            Size = new Vector2I(480, 270),
            AudioListenerEnable3D = false,
        };
        vp.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
        var container = new SubViewportContainer
        {
            Stretch = true,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            AnchorLeft = 0f, AnchorTop = 0f, AnchorRight = 1f, AnchorBottom = 1f,
        };
        AddChild(container);
        container.AddChild(vp);
        WorldViewport = vp;

        BuildWorld(vp);
        BuildHud();
        BuildShowroom();
        _profile = CareerProfile.Load(CareerUi.UserProfilePath);

        // Race screenshot hook for visual CI (run with APEXGP_SHOT_RACE=/abs/path.png).
        _raceShotPath = OS.GetEnvironment("APEXGP_SHOT_RACE");
        if (_raceShotPath != "")
        {
            var ev = EventCalendar.ById("sunriseCup1");
            _profile = CareerProfile.New();
            _profile.Credits = 50000;
            Career.BuyCar(_profile, CarSpec.ById("circuit1600"));
            StartEvent(_profile, ev);
            _shotFrames = 260;
            _shotPath = _raceShotPath;
            GD.Print("APEXGP_SHOT: race mode armed");
            return;
        }

        if (OS.GetEnvironment("APEXGP_SMOKE") == "1")
        {
            // CI path: straight into a 3-car race, driven by the env autopilot
            var ev = EventCalendar.ById("sunriseCup1");
            _profile = CareerProfile.New();
            _profile.Credits = 50000;
            Career.BuyCar(_profile, CarSpec.ById("circuit1600"));
            StartEvent(_profile, ev);
            return;
        }

        _careerUi = new CareerUi();
        AddChild(_careerUi);
        _careerUi.SetReplayHandler(StartReplay);

        // Dealership screenshot hook for visual CI.
        _shotPath = OS.GetEnvironment("APEXGP_SHOT_DEALERSHIP");
        if (_shotPath != "")
        {
            _shotFrames = 45;
            _careerUi.ShowScreen(CareerUi.Screen.Dealership);
        }

        // pause overlay (Esc toggles; SceneTree.Paused stops physics — cars freeze)
        _pauseLabel = new Label
        {
            Text = "PAUSED — ESC to resume",
            Position = new Vector2(240, 130),
            Visible = false,
        };
        _pauseLabel.AddThemeFontSizeOverride("font_size", 20);
        var pauseLayer = new CanvasLayer { Layer = 5 };
        pauseLayer.AddChild(_pauseLabel);
        AddChild(pauseLayer);
    }

    private Label _pauseLabel = null!;

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true } key) return;
        if (key.Keycode == Key.Escape)
        {
            if (_replayMode) { EndReplay(); return; }
            GetTree().Paused = !GetTree().Paused;
            _pauseLabel.Visible = GetTree().Paused;
        }
        if (key.Keycode == Key.R && _resultsShown && !_replayMode)
            StartReplay();
    }

    // ---------- race lifecycle ----------

    private void TeardownRace()
    {
        foreach (var n in _carNodes) n.QueueFree();
        _carNodes.Clear();
        _race = null;
        _activeEvent = null;
        _resultsShown = false;
        _licenceRun = null;
        _licenceCar = null;
        _licenceDef = null;
    }

    private Node3D? _trackVisuals;

    private void RebuildTrackVisuals(TrackSurface newTrack)
    {
        if (_trackVisuals != null && ReferenceEquals(Track, newTrack)) return;
        _trackVisuals?.QueueFree();
        _trackVisuals = null;
        Track = newTrack;
        _trackVisuals = TrackMesh.Build(Track);
        WorldViewport.AddChild(_trackVisuals);
        // licence runs and races both reference Track — they were torn down already
    }

    public void StartEvent(CareerProfile profile, EventDef ev)
    {
        TeardownRace();
        _activeEvent = ev;
        var eventTrack = TrackCatalog.ByName(ev.TrackName);
        RebuildTrackVisuals(eventTrack);
        _race = Career.StartEvent(profile, ev, Track, Career.ConfigFor(profile), playerIsAi: false);
        _replay = new ReplayRecorder();
        var paints = new[] { new Color(0.78f, 0.16f, 0.12f), new Color(0.16f, 0.38f, 0.82f),
                             new Color(0.88f, 0.88f, 0.86f), new Color(0.95f, 0.6f, 0.1f), new Color(0.2f, 0.7f, 0.3f) };
        for (int i = 0; i < _race.Racers.Count; i++)
        {
            var node = new CarController().WithVehicle(_race.Racers[i].Car).WithPaint(paints[i % paints.Length]);
            node.ExternalControl = true;
            node.UseKeyboard = _race.Racers[i].IsPlayer;
            WorldViewport.AddChild(node);
            _carNodes.Add(node);
            if (_race.Racers[i].IsPlayer)
                _player = node;
        }
        _hudVisible = true;
        SetHudVisible(true);
        _careerUi?.SetRaceVisible(false);
    }

    public void StartLicenseTest(LicenseTestDef def)
    {
        TeardownRace();
        var cfg = Career.ConfigFor(_profile);
        _licenceCar = new Vehicle(cfg);
        var s = Track.Samples[0];
        _licenceCar.Position = new System.Numerics.Vector3((float)(s.X + s.RightX * 3), 1.0f, (float)(s.Z + s.RightZ * 3));
        _licenceCar.Orientation = System.Numerics.Quaternion.CreateFromAxisAngle(
            System.Numerics.Vector3.UnitY, (float)System.Math.Atan2(-s.DirX, -s.DirZ));
        _licenceCar.Surface = Track;
        _licenceRun = new LicenseRun(Track, def);

        var node = new CarController().WithVehicle(_licenceCar).WithPaint(new Color(0.78f, 0.16f, 0.12f));
        node.ExternalControl = true;
        node.UseKeyboard = true;
        WorldViewport.AddChild(node);
        _carNodes.Add(node);
        _player = node; // the human drives the licence test
        _licenceDef = def;
        SetHudVisible(true);
        SetMessage("LICENCE TEST — through every gate in time!");
        _careerUi?.SetRaceVisible(false);
    }

    private void GrantLicence(LicenseTestDef def)
    {
        _profile.Licenses.Add(def.Id);
        _profile.Save(CareerUi.UserProfilePath);
        SetMessage($"{def.Name} EARNED!");
        TeardownRace();
        _careerUi?.ShowScreen(CareerUi.Screen.Licences);
        _careerUi?.SetRaceVisible(false);
    }

    public void SetRaceVisible(bool visible)
    {
        // menus overlay the 3D world; nothing to hide but HUD state
        SetHudVisible(visible);
    }

    private void SetHudVisible(bool visible)
    {
        _hudVisible = visible;
        if (_speedLabel == null) return;
        _speedLabel.Visible = visible;
        _gearLabel.Visible = visible;
        _rpmLabel.Visible = visible;
        _lapLabel.Visible = visible;
        _posLabel.Visible = visible;
    }

    private void SetMessage(string text)
    {
        _messageLabel.Text = text;
        _lastMessage = text;
    }

    // ---------- per-frame ----------

    public override void _PhysicsProcess(double delta)
    {
        if (_licenceRun != null && _licenceCar != null)
        {
            // human-driven licence test
            _player.PollInput();
            _licenceCar.Throttle = _player.ThrottleInput;
            _licenceCar.Brake = _player.BrakeInput;
            _licenceCar.SteerInput = _player.SteerInput;
            const double StepDt = 1.0 / 120.0;
            _licAccum += System.Math.Min(delta, 0.1);
            while (_licAccum >= StepDt)
            {
                _licenceCar.Step(StepDt);
                _licenceRun.Tick(_licenceCar, StepDt);
                _licAccum -= StepDt;
            }
            if (_licenceRun.AllPassed && _licenceDef != null)
                GrantLicence(_licenceDef);
            else if (_licenceRun.TimeExpired)
            {
                SetMessage("TIME UP — try again from the licence menu");
                _licenceRun = null;
                TeardownRace();
                _careerUi?.ShowScreen(CareerUi.Screen.Licences);
            }
            return;
        }

        if (_race == null) return;

        if (_race.Phase is RacePhase.Formation or RacePhase.Racing)
        {
            _player.PollInput();
            _race.Step(delta, (_player.ThrottleInput, _player.BrakeInput, _player.SteerInput));
            _replay?.Capture(_race, _race.Time);
        }
        else
        {
            _player.ThrottleInput = 0;
            _player.BrakeInput = 0.6;
            _race.Step(delta, (_player.ThrottleInput, _player.BrakeInput, 0));
        }

        if (_race.Phase == RacePhase.Finished && !_resultsShown && _activeEvent != null)
        {
            _resultsShown = true;
            var (prize, pos) = Career.CollectPrize(_profile, _activeEvent, _race);
            _profile.Save(CareerUi.UserProfilePath);
            _careerUi?.ShowResults(_activeEvent, _race, prize, pos, _replay != null);
        }
        UpdateReplayPlayback(delta);
    }

    private ReplayRecorder? _replay;
    private bool _replayMode;
    private double _replayTime;
    private readonly List<CarController> _ghosts = new();

    private void UpdateReplayPlayback(double delta)
    {
        if (!_replayMode) return;
        _replayTime += delta;
        var frames = _replay!.Sample(_replayTime);
        if (frames != null)
        {
            for (int i = 0; i < _ghosts.Count && i < frames.Length; i++)
            {
                var f = frames[i];
                _ghosts[i].SetGhostTransform(
                    new Vector3((float)f.X, (float)f.Y, (float)f.Z),
                    new Quaternion((float)f.Qx, (float)f.Qy, (float)f.Qz, (float)f.Qw));
            }
        }
        if (_replayTime >= _replay.Duration + 0.5)
            EndReplay();
    }

    private void StartReplay()
    {
        if (_replay == null) return;
        _replayMode = true;
        _replayTime = 0;
        _careerUi?.ShowReplayBanner();
        foreach (var n in _carNodes) n.Visible = false;
        if (_ghosts.Count == 0)
        {
            var paints = new[] { new Color(0.78f, 0.16f, 0.12f), new Color(0.16f, 0.38f, 0.82f),
                                 new Color(0.88f, 0.88f, 0.86f), new Color(0.95f, 0.6f, 0.1f), new Color(0.2f, 0.7f, 0.3f) };
            for (int i = 0; i < _race!.Racers.Count; i++)
            {
                var g = new CarController { GhostMode = true }.WithPaint(paints[i % paints.Length]);
                WorldViewport.AddChild(g);
                _ghosts.Add(g);
            }
        }
        foreach (var g in _ghosts) g.Visible = true;
    }

    private void EndReplay()
    {
        _replayMode = false;
        foreach (var g in _ghosts) g.Visible = false;
        foreach (var n in _carNodes) n.Visible = true;
        _careerUi?.HideReplayBanner();
    }

    public override void _Process(double delta)
    {
        UpdateShowroom(delta);
        CaptureScreenshotIfRequested();
        if (_carNodes.Count == 0 || _player == null) return;
        ChaseCamera(delta);

        if (!_hudVisible) return;
        var sim = _player.Sim;
        Racer? racer = null;
        if (_race != null) racer = _race.Racers.FirstOrDefault(r => r.IsPlayer);

        _speedLabel.Text = $"{sim.SpeedKmh:F0} km/h";
        _gearLabel.Text = sim.Gear >= 1 ? $"Gear {sim.Gear}" : "N";
        var rpm = sim.EngineRpm / sim.Config.Drivetrain.RedlineRpm;
        var bar = new string('█', (int)(rpm * 20)).PadRight(20, '·');
        _rpmLabel.Text = $"{bar} {sim.EngineRpm:F0} rpm";
        if (racer != null && _licenceRun == null)
        {
            _lapLabel.Text = racer.Laps == 0
                ? $"Lap 1 — {racer.Timer.CurrentLapTime:F2}s"
                : $"Lap {racer.Laps + 1} — {racer.Timer.CurrentLapTime:F2}s   (best {racer.Timer.BestLap:F2}s)";
            _posLabel.Text = $"P{racer.Position}/{_race!.Racers.Count}";

            string msg = _race.Phase switch
            {
                RacePhase.Formation => "GET READY...",
                RacePhase.Racing when racer.Laps < _race.Laps => "",
                _ => racer.Position <= 3 ? $"FINISH — P{racer.Position}!" : "FINISH"
            };
            if (msg != _lastMessage) SetMessage(msg);
        }
        else if (_licenceRun != null && _licenceRun.FinishTime < double.MaxValue)
        {
            _lapLabel.Text = $"Gates {_licenceRun.GatesPassed} — {_licenceRun.Elapsed:F1}s";
        }
    }

    private void ChaseCamera(double delta)
    {
        Node3D follow = _replayMode && _ghosts.Count > 0 ? _ghosts[0] : _player;
        var carPos = follow.GlobalPosition;
        var back = -follow.GlobalTransform.Basis.Z;
        var target = carPos - back * 5.2f + Vector3.Up * 2.3f;
        var k = 1f - Mathf.Exp((float)-(6.0 * delta));
        _camera.GlobalPosition = _camera.GlobalPosition.Lerp(target, k);
        _camera.LookAt(carPos + Vector3.Up * 0.9f, Vector3.Up);
    }

    // ---------- world ----------

    private void BuildWorld(Node world)
    {
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
        world.AddChild(env);

        var sun = new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-42f, 35f, 0f),
            LightEnergy = 1.15f,
            ShadowEnabled = false,
        };
        world.AddChild(sun);

        var ground = new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = new Vector2(900f, 900f) },
        };
        ground.MaterialOverride = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.30f, 0.40f, 0.28f),
            Roughness = 0.95f,
        };
        ground.Position = new Vector3(150f, 0f, -120f);
        world.AddChild(ground);

        Track = TrackCatalog.SunriseLoop();
        world.AddChild(TrackMesh.Build(Track));

        _camera = new Camera3D { Fov = 68f, Current = true };
        world.AddChild(_camera);
    }

    // ---------- dealership showroom ----------
    private SubViewportContainer? _showroomContainer;
    private Node3D _showroomTurntable = null!;   // rotates; car is its child
    private CarController? _showroomCar;

    private void BuildShowroom()
    {
        var vp = new SubViewport
        {
            Size = new Vector2I(420, 300),
            OwnWorld3D = true,
            AudioListenerEnable3D = false,
        };
        vp.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;

        vp.AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = new Color(0.055f, 0.065f, 0.09f),
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color(0.45f, 0.5f, 0.6f),
                AmbientLightEnergy = 0.4f,
            },
        });

        var key = new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-55f, 25f, 0f),
            LightEnergy = 1.3f,
        };
        vp.AddChild(key);
        var fill = new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-20f, -140f, 0f),
            LightEnergy = 0.5f,
            LightColor = new Color(0.7f, 0.78f, 0.95f),
        };
        vp.AddChild(fill);

        // turntable disc + car stage
        _showroomTurntable = new Node3D();
        var disc = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 2.6f, BottomRadius = 2.6f, Height = 0.08f, RadialSegments = 48 },
            Position = new Vector3(0, 0.04f, 0),
        };
        disc.MaterialOverride = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.32f, 0.34f, 0.4f),
            Roughness = 0.25f,
            Metallic = 0.6f,
        };
        _showroomTurntable.AddChild(disc);
        vp.AddChild(_showroomTurntable);

        // dark backdrop so the paint reads
        var back = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(14f, 6f, 0.3f) },
            Position = new Vector3(0f, 3f, -5.5f),
        };
        back.MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.10f, 0.11f, 0.15f), Roughness = 1f };
        vp.AddChild(back);

        var cam = new Camera3D
        {
            Position = new Vector3(3.6f, 1.7f, 4.6f),
            Fov = 42f,
            Current = true,
        };
        cam.LookAtFromPosition(cam.Position, new Vector3(0f, 0.62f, 0f), Vector3.Up);
        vp.AddChild(cam);

        _showroomContainer = new SubViewportContainer
        {
            AnchorLeft = 0.53f, AnchorTop = 0.30f, AnchorRight = 0.97f, AnchorBottom = 0.78f,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
        };
        _showroomContainer.AddChild(vp);
        _showroomContainer.Visible = false;
        AddChild(_showroomContainer);
    }

    /// <summary>Show a car on the dealership turntable (RRGGBB paint hex).</summary>
    public void ShowroomShow(string paintHex)
    {
        if (_showroomContainer == null) return;
        _showroomContainer.Visible = true;
        _showroomCar?.QueueFree();
        _showroomCar = new CarController { GhostMode = true }.WithPaint(HexToColor(paintHex));
        _showroomCar.Position = new Vector3(0f, 0.66f, 0f); // turntable top + resting chassis height
        _showroomTurntable.AddChild(_showroomCar);
    }

    public void ShowroomHide()
    {
        if (_showroomContainer != null) _showroomContainer.Visible = false;
    }

    private static Color HexToColor(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length != 6 || !System.Text.RegularExpressions.Regex.IsMatch(hex, "\\A[0-9a-fA-F]{6}\\z"))
            return new Color(0.78f, 0.16f, 0.12f);
        return new Color(
            System.Convert.ToInt32(hex[..2], 16) / 255f,
            System.Convert.ToInt32(hex[2..4], 16) / 255f,
            System.Convert.ToInt32(hex[4..6], 16) / 255f, 1f);
    }

    private void UpdateShowroom(double delta)
    {
        if (_showroomContainer is { Visible: true })
            _showroomTurntable.RotateY((float)(delta * 0.85));
    }

    private string _shotPath = "";
    private string _raceShotPath = "";
    private int _shotFrames = -1;

    private void CaptureScreenshotIfRequested()
    {
        if (_shotPath == "") return;
        if (_shotFrames > 0)
        {
            _shotFrames--;
            if (_shotFrames == 130) GD.Print("APEXGP_SHOT: halfway");
            return;
        }
        var img = GetViewport().GetTexture().GetImage();
        img.SavePng(_shotPath);
        GD.Print($"APEXGP_SHOT saved to {_shotPath}");
        _shotPath = "";
        GetTree().Quit();
    }

    private void BuildHud()
    {
        var layer = new CanvasLayer();
        AddChild(layer);
        var font = ThemeDB.FallbackFont;

        Label Mk(string text, Vector2 pos, int size, Color color)
        {
            return new Label
            {
                Position = pos,
                Text = text,
                LabelSettings = new LabelSettings
                {
                    Font = font, FontSize = size, FontColor = color,
                    OutlineSize = size / 5, OutlineColor = new Color(0.05f, 0.1f, 0.22f),
                },
            };
        }

        _speedLabel = Mk("", new Vector2(24f, 24f), 42, Colors.White);
        _gearLabel = Mk("", new Vector2(24f, 72f), 24, new Color(0.85f, 0.9f, 1f));
        _rpmLabel = Mk("", new Vector2(24f, 104f), 18, new Color(1f, 0.75f, 0.3f));
        _lapLabel = Mk("", new Vector2(24f, 132f), 20, new Color(0.9f, 0.95f, 1f));
        _posLabel = Mk("", new Vector2(24f, 162f), 34, new Color(1f, 0.95f, 0.6f));
        layer.AddChild(_speedLabel);
        layer.AddChild(_gearLabel);
        layer.AddChild(_rpmLabel);
        layer.AddChild(_lapLabel);
        layer.AddChild(_posLabel);

        _messageLabel = new Label
        {
            AnchorLeft = 0f, AnchorRight = 1f, AnchorTop = 0.14f, AnchorBottom = 0.14f,
            HorizontalAlignment = HorizontalAlignment.Center,
            LabelSettings = new LabelSettings
            {
                Font = font, FontSize = 52, FontColor = new Color(1f, 0.9f, 0.25f),
                OutlineSize = 10, OutlineColor = new Color(0.05f, 0.08f, 0.2f),
            },
        };
        layer.AddChild(_messageLabel);
    }
}
