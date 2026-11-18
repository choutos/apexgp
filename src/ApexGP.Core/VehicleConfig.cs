using System.Numerics;

namespace ApexGP.Core;

public sealed class SuspensionConfig
{
    public double RestLength = 0.25;        // m
    public double MaxTravel = 0.12;         // m
    public double SpringStiffness = 45000;  // N/m
    public double DamperRate = 3800;        // Ns/m
    public double AntiRollStiffness = 12000;// N/m per m of cross-axle difference
    public double BumpStiffness = 160000;   // N/m beyond MaxTravel
}

public sealed class WheelConfig
{
    public Vector3 MountLocal;              // hub mount in body frame (origin = CoM)
    public double Radius = 0.30;            // m
    public double Inertia = 1.20;           // kg m^2
    public double MaxBrakeTorque = 1000;    // Nm
    public bool IsFront;
    public bool IsDriven;
}

public sealed class DrivetrainConfig
{
    public double[] GearRatios = { 3.4, 2.2, 1.6, 1.22, 1.0 };
    public double FinalDrive = 4.9;
    public double Efficiency = 0.92;
    public double EngineInertia = 0.16;     // kg m^2
    public double IdleRpm = 800;
    public double RedlineRpm = 7800;
    public double ShiftUpRpm = 6900;
    public double ShiftDownRpm = 2600;
    public double ShiftTime = 0.20;         // s of torque cut
    public double ClutchCapacity = 240;     // Nm, engine-side
    public double LaunchFloorRpm = 3000;    // engine floor while slipping under load
    public (double Rpm, double Torque)[] TorqueCurve =
    {
        (800, 70), (1500, 125), (2500, 165), (4000, 188), (5200, 185), (6500, 165), (7800, 120)
    };
    public double FrictionBase = 35;        // Nm
    public double FrictionSlope = 0.015;    // Nm per rad/s
}

public sealed class VehicleConfig
{
    public string Name = "Prototype GT-b";
    public double MassKg = 1250;
    public double CdA = 0.68;               // m^2 drag-area product
    public double InertiaPitch = 2193;      // about X (right axis)
    public double InertiaYaw = 2335;        // about Y (up axis)
    public double InertiaRoll = 502;        // about Z (back axis)
    public double TireMuBase = 1.05;
    public double MaxSteerAngleDeg = 32;
    public bool AbsEnabled = true;
    public bool TractionControlEnabled = true;
    public SuspensionConfig Suspension = new();
    public DrivetrainConfig Drivetrain = new();
    public WheelConfig[] Wheels = Array.Empty<WheelConfig>();

    public static VehicleConfig PrototypeGtB() => new()
    {
        Wheels = new[]
        {
            new WheelConfig { MountLocal = new Vector3(-0.75f, -0.10f, -1.25f), IsFront = true,  MaxBrakeTorque = 1400 },
            new WheelConfig { MountLocal = new Vector3( 0.75f, -0.10f, -1.25f), IsFront = true,  MaxBrakeTorque = 1400 },
            new WheelConfig { MountLocal = new Vector3(-0.75f, -0.10f,  1.25f), IsDriven = true, MaxBrakeTorque = 950 },
            new WheelConfig { MountLocal = new Vector3( 0.75f, -0.10f,  1.25f), IsDriven = true, MaxBrakeTorque = 950 },
        }
    };
}
