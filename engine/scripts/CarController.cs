using System.Collections.Generic;
using Godot;
using ApexGP.Core;

namespace ApexGP.Game;

/// <summary>
/// Thin Godot wrapper around the ApexGP.Core Vehicle. Reads player input,
/// steps the sim on a fixed 1/120 s grid, and pushes state into visual nodes.
/// Contains zero game logic — the truth lives in Core.
/// </summary>
public partial class CarController : Node3D
{
    private Vehicle _vehicle = null!;
    private Vehicle? _injected;
    private readonly Node3D[] _wheels = new Node3D[4];
    private readonly float[] _wheelRollAngle = new float[4];
    private MeshInstance3D? _bodyMesh;

    /// <summary>When true, an external RaceController steps this vehicle.</summary>
    public bool ExternalControl { get; set; }

    public CarController WithVehicle(Vehicle v)
    {
        _injected = v;
        return this;
    }

    public CarController WithPaint(Color c)
    {
        _paint = c;
        if (_bodyMesh != null)
            _bodyMesh.MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = c,
                Roughness = 0.35f,
                Metallic = 0.4f,
            };
        return this;
    }

    private Node3D _brakeLights = null!;
    private readonly List<StandardMaterial3D> _brakeLightMats = new();
    private double _brakeGlow;
    private Color _paint = new(0.78f, 0.16f, 0.12f);

    public Vehicle Sim => _vehicle;

    public void SetSurface(ITrackSurface surface) => _vehicle.Surface = surface;

    /// <summary>Public entry so the shell can feed inputs before the race steps.</summary>
    public void PollInput() => ReadInput();

    /// <summary>0..1. Set directly for tests; otherwise read from input.</summary>
    public double ThrottleInput { get; set; }
    public double BrakeInput { get; set; }
    /// <summary>-1..1, positive = left.</summary>
    public double SteerInput { get; set; }

    public bool UseKeyboard { get; set; } = true;

    private double _smokeT;
    private int _smokePhase;
    private bool _smokeMode;
    private AudioStreamPlayer? _engineSound;

    /// <summary>Procedural engine audio: a synthesized firing-order loop, pitch-tracked to RPM.</summary>
    private void BuildEngineAudio()
    {
        // 4-cylinder firing loop: ~6 pulses per rev, 480 samples @ 11025 Hz = 43 ms loop
        const int SampleRate = 11025;
        const int Samples = 480;
        var data = new byte[Samples * 2];
        for (int i = 0; i < Samples; i++)
        {
            double ph = (double)i / Samples * 6.0 * System.Math.PI;
            double v = 0.72 * System.Math.Sin(ph) + 0.28 * System.Math.Sin(3 * ph) + 0.14 * System.Math.Sin(5 * ph);
            short s16 = (short)(System.Math.Clamp(v, -1, 1) * 12000);
            data[i * 2] = (byte)(s16 & 0xFF);
            data[i * 2 + 1] = (byte)((s16 >> 8) & 0xFF);
        }
        _engineSound = new AudioStreamPlayer
        {
            Stream = new AudioStreamWav
            {
                Format = AudioStreamWav.FormatEnum.Format16Bits,
                MixRate = SampleRate,
                Stereo = false,
                LoopMode = AudioStreamWav.LoopModeEnum.Forward,
                LoopBegin = 0,
                LoopEnd = Samples,
                Data = data,
            },
            VolumeDb = -14f,
        };
        AddChild(_engineSound);
        _engineSound.Play();
    }

    private void UpdateEngineAudio()
    {
        if (_engineSound == null) return;
        bool playerCar = UseKeyboard; // only the player car sings
        _engineSound.StreamPaused = !playerCar;
        if (!playerCar) return;
        double rpm = Sim.EngineRpm;
        double t = System.Math.Clamp(rpm / Sim.Config.Drivetrain.RedlineRpm, 0, 1.2);
        _engineSound.PitchScale = (float)(0.55 + t * 2.6);
        _engineSound.VolumeDb = (float)(-16 + ThrottleInput * 9 + t * 6);
    }

    public override void _Ready()
    {
        _vehicle = _injected ?? new Vehicle(VehicleConfig.PrototypeGtB());
        _vehicle.Position = new System.Numerics.Vector3(0f, 1.0f, 0f);
        BuildBody();
        BuildWheels();
        BuildEngineAudio();
        WithPaint(_paint);
        _smokeMode = UseKeyboard && OS.GetEnvironment("APEXGP_SMOKE") == "1";
        if (_smokeMode)
        {
            UseKeyboard = false;
            GD.Print("APEXGP_SMOKE begin");
        }
    }

    private void BuildBody()
    {
        // low-poly GT-style coupe: nose, cabin, spoiler, lights
        _bodyMesh = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(1.72f, 0.42f, 4.2f) },
            Position = new Vector3(0f, 0.62f, 0.1f),
        };
        _bodyMesh.MaterialOverride = new StandardMaterial3D
        {
            AlbedoColor = _paint,
            Roughness = 0.35f,
            Metallic = 0.4f,
        };
        AddChild(_bodyMesh);

        var nose = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(1.55f, 0.22f, 1.0f) },
            Position = new Vector3(0f, 0.42f, -1.75f),
        };
        nose.MaterialOverride = new StandardMaterial3D { AlbedoColor = _paint, Roughness = 0.35f, Metallic = 0.4f };
        AddChild(nose);

        var cabin = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(1.46f, 0.40f, 1.9f) },
            Position = new Vector3(0f, 1.02f, 0.30f),
        };
        cabin.MaterialOverride = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.10f, 0.12f, 0.16f),
            Roughness = 0.2f,
            Metallic = 0.2f,
        };
        AddChild(cabin);

        // rear wing on struts
        var wing = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(1.6f, 0.07f, 0.42f) },
            Position = new Vector3(0f, 1.05f, 1.85f),
        };
        wing.MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.12f, 0.12f, 0.13f), Roughness = 0.6f };
        AddChild(wing);
        foreach (float sx in new[] { -0.6f, 0.6f })
        {
            var strut = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(0.08f, 0.26f, 0.2f) },
                Position = new Vector3(sx, 0.88f, 1.85f),
            };
            strut.MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.12f, 0.12f, 0.13f) };
            AddChild(strut);
        }

        // brake lights: emissive quads at the rear, lit under braking
        _brakeLights = new Node3D();
        foreach (float sx in new[] { -0.6f, 0.6f })
        {
            var light = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(0.4f, 0.12f, 0.05f) },
                Position = new Vector3(sx, 0.72f, 2.2f),
            };
            _brakeLightMats.Add(new StandardMaterial3D
            {
                AlbedoColor = new Color(0.35f, 0.02f, 0.02f),
                EmissionEnabled = true,
                Emission = new Color(0.0f, 0.0f, 0.0f),
            });
            light.MaterialOverride = _brakeLightMats[^1];
            _brakeLights.AddChild(light);
        }
        AddChild(_brakeLights);
    }

    private void BuildWheels()
    {
        var cfg = _vehicle.Config;
        for (int i = 0; i < 4; i++)
        {
            var pivot = new Node3D();
            var mesh = new MeshInstance3D
            {
                Mesh = new CylinderMesh
                {
                    TopRadius = (float)cfg.Wheels[i].Radius,
                    BottomRadius = (float)cfg.Wheels[i].Radius,
                    Height = 0.24f,
                    RadialSegments = 12,
                },
            };
            // cylinder is Y-aligned; rotate to align axle with X (wheel axle)
            mesh.RotationDegrees = new Vector3(0f, 0f, 90f);
            mesh.MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.08f, 0.08f, 0.08f),
                Roughness = 0.9f,
            };
            pivot.AddChild(mesh);
            _wheels[i] = pivot;
            AddChild(pivot);
            PlaceWheel(i);
        }
    }

    private void PlaceWheel(int i)
    {
        var wc = _vehicle.Config.Wheels[i];
        var w = _vehicle.Wheels[i];
        _wheels[i].Position = new Vector3(
            (float)wc.MountLocal.X,
            (float)(wc.MountLocal.Y - (_vehicle.Config.Suspension.RestLength - w.SuspensionCompression)),
            (float)wc.MountLocal.Z);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_smokeMode)
        {
            _smokeT += delta;
            switch (_smokePhase)
            {
                case 0: // wait for the green flag
                    ThrottleInput = 0; BrakeInput = 0;
                    if (_smokeT >= 1.4) _smokePhase = 1;
                    break;
                case 1: // full throttle for 3 s of racing
                    ThrottleInput = 1.0; BrakeInput = 0;
                    if (_smokeT >= 4.6)
                    {
                        GD.Print($"APEXGP_SMOKE accel={Sim.SpeedKmh:F2}");
                        _smokePhase = 2;
                    }
                    break;
                case 2: // full brake
                    ThrottleInput = 0; BrakeInput = 1;
                    if (_smokeT >= 5.6)
                    {
                        GD.Print($"APEXGP_SMOKE brake={Sim.SpeedKmh:F2}");
                        _smokePhase = 3;
                    }
                    break;
            }
        }

        ReadInput();
        var sim = _vehicle;

        if (ExternalControl)
        {
            // The RaceController steps this car and feeds its inputs; we just render.
            sim.Throttle = ThrottleInput;
            sim.Brake = BrakeInput;
            sim.SteerInput = SteerInput;
            ApplySimToScene(delta);
            return;
        }

        sim.Throttle = ThrottleInput;
        sim.Brake = BrakeInput;
        sim.SteerInput = SteerInput;

        const double StepDt = 1.0 / 120.0;
        _accum += System.Math.Min(delta, 0.1);
        int guard = 0;
        while (_accum >= StepDt && guard < 8)
        {
            sim.Step(StepDt);
            _accum -= StepDt;
            guard++;
        }

        ApplySimToScene(delta);
        UpdateBrakeLights(delta);
        UpdateEngineAudio();
        UpdateRumble();
    }

    private void UpdateBrakeLights(double delta)
    {
        if (_brakeLightMats.Count == 0) return;
        _brakeGlow = System.Math.Clamp(_brakeGlow + (BrakeInput > 0.1 ? delta * 6 : -delta * 6), 0, 1);
        foreach (var m in _brakeLightMats)
            m.Emission = new Color((float)(_brakeGlow * 0.9), 0.02f, 0.02f);
    }

    /// <summary>Gamepad rumble tied to tire slip (v0.2 FFB stand-in).</summary>
    private void UpdateRumble()
    {
        if (!UseKeyboard) return; // keyboard-only players don't own a pad
        float slip = 0f;
        foreach (var w in Sim.Wheels)
            slip = System.Math.Max(slip, (float)System.Math.Abs(w.SlipRatio) - 0.15f);
        float strong = System.Math.Clamp(slip * 0.8f, 0f, 0.8f);
        Input.StartJoyVibration(0, strong * 0.6f, strong, 0.12f);
    }

    private double _accum;

    private void ReadInput()
    {
        if (!UseKeyboard)
            return;

        double thr = 0, brk = 0, steer = 0;
        float rTrig = Input.GetJoyAxis(0, JoyAxis.TriggerRight);
        float lTrig = Input.GetJoyAxis(0, JoyAxis.TriggerLeft);
        if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up) || rTrig > 0.4f)
            thr = 1;
        if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down) || lTrig > 0.4f)
            brk = 1;
        if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left))
            steer += 1;
        if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right))
            steer -= 1;
        float axisX = Input.GetJoyAxis(0, JoyAxis.LeftX);
        if (System.Math.Abs(axisX) > 0.15)
            steer -= axisX; // stick right = steer right (negative)

        ThrottleInput = thr;
        BrakeInput = brk;
        SteerInput = System.Math.Clamp(steer, -1, 1);
    }

    private void ApplySimToScene(double delta)
    {
        var sim = _vehicle;
        GlobalTransform = new Transform3D(
            new Basis(new Godot.Quaternion(sim.Orientation.X, sim.Orientation.Y, sim.Orientation.Z, sim.Orientation.W)),
            new Godot.Vector3(sim.Position.X, sim.Position.Y, sim.Position.Z));

        // wheels: steer (front) around Y, roll around local X
        for (int i = 0; i < 4; i++)
        {
            var w = sim.Wheels[i];
            PlaceWheel(i);
            _wheelRollAngle[i] = Mathf.Wrap(_wheelRollAngle[i] + (float)w.SpinRate * (float)delta, 0f, Mathf.Tau);
            _wheels[i].Rotation = new Vector3(0, (float)w.SteerAngleRad, _wheelRollAngle[i]);
        }
    }
}
