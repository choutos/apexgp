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
    private readonly Node3D[] _wheels = new Node3D[4];
    private readonly float[] _wheelRollAngle = new float[4];

    public Vehicle Sim => _vehicle;

    /// <summary>0..1. Set directly for tests; otherwise read from input.</summary>
    public double ThrottleInput { get; set; }
    public double BrakeInput { get; set; }
    /// <summary>-1..1, positive = left.</summary>
    public double SteerInput { get; set; }

    public bool UseKeyboard { get; set; } = true;

    private double _smokeT;
    private int _smokePhase;
    private bool _smokeMode;

    public override void _Ready()
    {
        _vehicle = new Vehicle(VehicleConfig.PrototypeGtB());
        _vehicle.Position = new System.Numerics.Vector3(0f, 1.0f, 0f);
        BuildBody();
        BuildWheels();
        _smokeMode = OS.GetEnvironment("APEXGP_SMOKE") == "1";
        if (_smokeMode)
        {
            UseKeyboard = false;
            GD.Print("APEXGP_SMOKE begin");
        }
    }

    private void BuildBody()
    {
        var body = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(1.72f, 0.5f, 4.2f) },
            Position = new Vector3(0f, 0.62f, 0.1f),
        };
        body.MaterialOverride = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.78f, 0.16f, 0.12f),
            Roughness = 0.35f,
            Metallic = 0.4f,
        };
        AddChild(body);

        var cabin = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(1.5f, 0.42f, 1.9f) },
            Position = new Vector3(0f, 1.05f, 0.25f),
        };
        cabin.MaterialOverride = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.1f, 0.12f, 0.16f),
            Roughness = 0.25f,
        };
        AddChild(cabin);
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
            if (_smokePhase == 0)
            {
                ThrottleInput = 1.0; BrakeInput = 0;
                if (_smokeT >= 1.5)
                {
                    GD.Print($"APEXGP_SMOKE accel={Sim.SpeedKmh:F2} thr={ThrottleInput:F2} y={Sim.Position.Y:F3} gear={Sim.Gear}");
                    _smokePhase = 1;
                }
                else if (System.Math.Abs(_smokeT - System.Math.Round(_smokeT * 2) / 2.0) < delta / 2)
                {
                    GD.Print($"APEXGP_SMOKE t={_smokeT:F2} kmh={Sim.SpeedKmh:F2} thr={ThrottleInput:F2} rpm={Sim.EngineRpm:F0} clutch={Sim.Drivetrain.Clutch} y={Sim.Position.Y:F3}");
                }
            }
            else
            {
                ThrottleInput = 0; BrakeInput = 1;
                if (_smokeT >= 3.6 && _smokePhase == 1)
                {
                    GD.Print($"APEXGP_SMOKE brake={Sim.SpeedKmh:F2} thr={ThrottleInput:F2} brk={BrakeInput:F2}");
                    _smokePhase = 2;
                }
                else if (_smokePhase == 2 && System.Math.Abs(_smokeT - System.Math.Round(_smokeT * 2) / 2.0) < delta / 2)
                {
                    GD.Print($"APEXGP_SMOKE t={_smokeT:F2} kmh={Sim.SpeedKmh:F2} thr={ThrottleInput:F2} brk={BrakeInput:F2} kB={Sim.Wheels[2].SlipRatio:F2}");
                }
            }
        }

        ReadInput();
        var sim = _vehicle;
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
