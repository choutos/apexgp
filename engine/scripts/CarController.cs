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
                AlbedoTexture = MakeLiveryTexture(),
                Roughness = 0.35f,
                Metallic = 0.4f,
            };
        return this;
    }

    private Node3D _brakeLights = null!;
    private readonly List<StandardMaterial3D> _brakeLightMats = new();
    private double _brakeGlow;
    private Color _paint = new(0.78f, 0.16f, 0.12f);

    /// <summary>Replay ghost: visuals only, transform driven by the replay sampler.</summary>
    public bool GhostMode { get; set; }
    private VehicleConfig _ghostConfig = null!;

    public void SetGhostTransform(Vector3 pos, Quaternion rot)
    {
        Position = pos;
        Basis = new Basis(rot);
    }

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
        // Softer 4-cylinder firing loop: fundamental + 2nd/3rd harmonics with a
        // rounded pulse envelope and a low thrum — less sawtooth, less rasp.
        // All components complete whole cycles per loop (no clicks).
        const int SampleRate = 11025;
        const int Samples = 480;
        var data = new byte[Samples * 2];
        for (int i = 0; i < Samples; i++)
        {
            double f = (double)i / Samples;                 // 0..1 per loop
            double ph = 2 * System.Math.PI * 6.0 * f;       // 6 firing events per rev
            double envelope = 0.62 + 0.38 * System.Math.Cos(2 * System.Math.PI * 3.0 * f);
            double v = 0.55 * System.Math.Sin(ph)
                     + 0.32 * System.Math.Sin(2 * ph + 0.6)
                     + 0.12 * System.Math.Sin(3 * ph)
                     + 0.18 * System.Math.Sin(ph / 2);      // 3 cycles/loop: low thrum
            v *= envelope;
            short s16 = (short)(System.Math.Clamp(v, -1, 1) * 9000);
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
            VolumeDb = -20f,
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
        _engineSound.PitchScale = (float)(0.5 + t * 1.9);
        _engineSound.VolumeDb = (float)(-20 + ThrottleInput * 8 + t * 4);
    }

    public override void _Ready()
    {
        _vehicle = _injected ?? new Vehicle(VehicleConfig.PrototypeGtB());
        _vehicle.Position = new System.Numerics.Vector3(0f, 1.0f, 0f);
        if (GhostMode)
        {
            // replay ghost / showroom car: visuals only. Settle the inert vehicle on
            // the flat ground so wheel geometry and ride height match a live car.
            _ghostConfig = VehicleConfig.PrototypeGtB();
            BuildBody();
            BuildWheels();
            for (int i = 0; i < 240; i++) _vehicle.Step(1.0 / 120.0);
            for (int i = 0; i < 4; i++) PlaceWheel(i);
            // place so the settled CoG sits discTop + restHeight
            Position = new Vector3(Position.X, 0.08f + (float)_vehicle.Position.Y, Position.Z);
            return;
        }
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
        var b = _vehicle.Config.Body;
        float halfLen = (float)(b.BodyLength / 2.0);

        _bodyMesh = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3((float)b.BodyWidth, (float)b.BodyHeight, (float)b.BodyLength) },
            Position = new Vector3(0f, 0.62f, 0.1f),
        };
        _bodyMesh.MaterialOverride = new StandardMaterial3D
        {
            AlbedoTexture = MakeLiveryTexture(),
            Roughness = 0.35f,
            Metallic = 0.4f,
        };
        AddChild(_bodyMesh);

        var nose = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3((float)(b.BodyWidth - 0.17), (float)b.NoseDrop, (float)b.NoseLength) },
            Position = new Vector3(0f, 0.42f, -halfLen + (float)(b.NoseLength / 2) - 0.15f),
        };
        nose.MaterialOverride = new StandardMaterial3D
        {
            AlbedoTexture = MakeLiveryTexture(),
            Roughness = 0.35f,
            Metallic = 0.4f,
        };
        AddChild(nose);

        var cabin = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3((float)(b.BodyWidth - 0.26), (float)b.CabinHeight, (float)b.CabinLength) },
            Position = new Vector3(0f, 1.02f, (float)b.CabinShift),
        };
        cabin.MaterialOverride = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.10f, 0.12f, 0.16f),
            Roughness = 0.2f,
            Metallic = 0.2f,
        };
        AddChild(cabin);

        // rear wing on struts
        var wingZ = halfLen - 0.25f;
        var wing = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3((float)b.SpoilerSpan, 0.07f, (float)b.SpoilerChord) },
            Position = new Vector3(0f, 1.05f, wingZ),
        };
        wing.MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.12f, 0.12f, 0.13f), Roughness = 0.6f };
        AddChild(wing);
        foreach (float sx in new[] { -(float)(b.SpoilerSpan / 2 - 0.2), (float)(b.SpoilerSpan / 2 - 0.2) })
        {
            var strut = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(0.08f, 0.26f, 0.2f) },
                Position = new Vector3(sx, 0.88f, wingZ),
            };
            strut.MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.12f, 0.12f, 0.13f) };
            AddChild(strut);
        }

        // brake lights: emissive quads at the rear, lit under braking
        _brakeLights = new Node3D();
        foreach (float sx in new[] { -(float)(b.BodyWidth / 2 - 0.26), (float)(b.BodyWidth / 2 - 0.26) })
        {
            var light = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(0.4f, 0.12f, 0.05f) },
                Position = new Vector3(sx, 0.72f, halfLen + 0.02f),
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

    /// <summary>
    /// Procedural livery: paint base + stripes + race number, painted per BoxMesh atlas cell
    /// (Godot maps each box face to a sub-region, so drawing per-cell guarantees every face
    /// shows the livery). Nearest-filtered for the PS1 look.
    /// </summary>
    private ImageTexture MakeLiveryTexture()
    {
        const int Size = 128;
        const int Cols = 3, Rows = 2;
        var img = Godot.Image.CreateEmpty(Size, Size, false, Godot.Image.Format.Rgba8);
        img.Fill(_paint);
        var b = _vehicle.Config.Body;
        var stripe = HexToColor(b.StripeHex, new Color(0.92f, 0.92f, 0.9f));
        int cw = Size / Cols, ch = Size / Rows;

        for (int cy = 0; cy < Rows; cy++)
            for (int cx = 0; cx < Cols; cx++)
            {
                int x0 = cx * cw, y0 = cy * ch;
                void VBand(double fx0, double fx1)   // fractions of the cell width
                {
                    int bx0 = x0 + (int)(fx0 * cw), bx1 = x0 + (int)(fx1 * cw);
                    for (int y = y0; y < y0 + ch; y++)
                        for (int x = bx0; x < bx1; x++)
                            if (x >= 0 && x < Size && y >= 0 && y < Size) img.SetPixel(x, y, stripe);
                }
                if (b.StripeStyle == 1) { VBand(0.30, 0.42); VBand(0.58, 0.70); }
                else if (b.StripeStyle == 2) { VBand(0.04, 0.14); VBand(0.86, 0.96); }

                if (b.CarNumber > 0)
                {
                    // roundel: white disc + dark ring, centred in the cell
                    var white = new Color(0.96f, 0.95f, 0.92f);
                    var dark = new Color(0.1f, 0.1f, 0.12f);
                    int ccx = x0 + cw / 2, ccy = y0 + ch / 2, r = ch / 5;
                    for (int y = ccy - r - 2; y <= ccy + r + 2; y++)
                        for (int x = ccx - r - 2; x <= ccx + r + 2; x++)
                        {
                            double d = System.Math.Sqrt((x - ccx) * (x - ccx) + (y - ccy) * (double)(y - ccy));
                            if (x < 0 || x >= Size || y < 0 || y >= Size) continue;
                            if (d < r) img.SetPixel(x, y, white);
                            else if (d < r + 3) img.SetPixel(x, y, dark);
                        }
                }
            }
        return ImageTexture.CreateFromImage(img);
    }

    private static Color HexToColor(string hex, Color fallback)
    {
        hex = hex.TrimStart('#');
        if (hex.Length != 6 || !System.Text.RegularExpressions.Regex.IsMatch(hex, "\\A[0-9a-fA-F]{6}\\z"))
            return fallback;
        return new Color(
            System.Convert.ToInt32(hex[..2], 16) / 255f,
            System.Convert.ToInt32(hex[2..4], 16) / 255f,
            System.Convert.ToInt32(hex[4..6], 16) / 255f, 1f);
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
            // hub cap slightly proud of both faces: makes wheel rotation readable
            var hub = new MeshInstance3D
            {
                Mesh = new CylinderMesh
                {
                    TopRadius = (float)(cfg.Wheels[i].Radius * 0.45),
                    BottomRadius = (float)(cfg.Wheels[i].Radius * 0.45),
                    Height = 0.26f,
                    RadialSegments = 8,
                },
                RotationDegrees = new Vector3(0f, 0f, 90f),
            };
            hub.MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.75f, 0.76f, 0.8f),
                Roughness = 0.3f,
                Metallic = 0.8f,
            };
            pivot.AddChild(hub);
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
        if (GhostMode) return; // transform driven externally by the replay sampler
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
                case 2: // full brake (phase extended for the buffed car's higher entry speed)
                    ThrottleInput = 0; BrakeInput = 1;
                    if (_smokeT >= 6.8)
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
            // spin around the wheel axle (X) after steering yaw (Y) — Godot Euler order
            // is YXZ, so (roll, steer, 0) rolls about the steered axle. The previous
            // (0, steer, roll) tumbled the wheel around the car's longitudinal axis.
            _wheelRollAngle[i] = Mathf.Wrap(_wheelRollAngle[i] + (float)w.SpinRate * (float)delta, 0f, Mathf.Tau);
            _wheels[i].Rotation = new Vector3(-_wheelRollAngle[i], (float)w.SteerAngleRad, 0f);
        }
    }
}
