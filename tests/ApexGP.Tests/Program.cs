using System.Numerics;
using ApexGP.Core;

namespace ApexGP.Tests;

public static class Program
{
    private static int _passed, _failed;
    private static readonly List<string> _failures = new();

    public static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "debug")
        {
            DebugScenario(args.Length > 1 ? args[1] : "settle");
            return 0;
        }

        Run("tire.peakLateral", TireModelTests.PeakLateralSlipInPlausibleRange);
        Run("tire.peakLongitudinal", TireModelTests.PeakLongitudinalSlipInPlausibleRange);
        Run("tire.oddSymmetry", TireModelTests.OddSymmetry);
        Run("tire.loadSensitivity", TireModelTests.LoadSensitivityReducesPerNewton);
        Run("tire.brakingSign", TireModelTests.BrakingSlipGivesNegativeForce);

        Run("vehicle.idlesAtStandstill", VehicleDynamicsTests.IdlesAtStandstill);
        Run("vehicle.acceleratesTo100", VehicleDynamicsTests.AcceleratesTo100Kmh);
        Run("vehicle.brakesFrom100", VehicleDynamicsTests.BrakesFrom100Kmh);
        Run("vehicle.steadyStateTopSpeed", VehicleDynamicsTests.SteadyStateTopSpeed);
        Run("vehicle.cornering", VehicleDynamicsTests.CorneringSteersAndStaysSane);
        Run("vehicle.mixed120sStable", VehicleDynamicsTests.MixedRun120sStable);
        Run("vehicle.weightTransfer", VehicleDynamicsTests.WeightTransferUnderAcceleration);

        Console.WriteLine($"\n=== {_passed} passed, {_failed} failed ===");
        foreach (var f in _failures) Console.WriteLine("FAIL: " + f);
        return _failed == 0 ? 0 : 1;
    }

    private static void Run(string name, Action test)
    {
        try
        {
            test();
            _passed++;
            Console.WriteLine($"PASS  {name}");
        }
        catch (Exception ex)
        {
            _failed++;
            _failures.Add($"{name}: {ex.Message}");
            Console.WriteLine($"FAIL  {name}: {ex.Message}");
        }
    }

    // Diagnostic: step a scenario and dump state every 0.5 s.
    // Modes: settle (default) | wot | corner
    private static void DebugScenario(string mode = "settle")
    {
        var v = TestCars.PrototypeGtB();
        double dt = 1.0 / 240.0;
        if (mode == "wot") { TestCars.Settle(v); v.Throttle = 1.0; }
        if (mode == "corner")
        {
            TestCars.Settle(v);
            v.Throttle = 1.0;
            while (v.SpeedKmh < 58.0) v.Step(dt);
            TestCars.Simulate(v, 0.2);
            v.Throttle = 0.35;
            v.SteerInput = 0.35;
        }
        Console.WriteLine("   t  kmh  gear clutch      rpm   axleW   spin[FL FR RL RR]      kR    FxR     TdR    yawW   X      Z");
        ClutchState pc = v.Drivetrain.Clutch; int pg = v.Gear;
        for (int i = 0; i <= 1200; i++)
        {
            if (i > 0) v.Step(dt);
            if (v.Drivetrain.Clutch != pc || v.Gear != pg)
            {
                var w = v.Wheels;
                Console.WriteLine($"  ^{i * dt,5:F3} {v.SpeedKmh,5:F1} gear={pg}->{v.Gear} clutch={pc}->{v.Drivetrain.Clutch} rpm={v.EngineRpm,6:F0} axle={v.Drivetrain.AxleOmega,6:F2} spinR={w[2].SpinRate,6:F2} k={w[2].SlipRatio,6:F2} Fx={w[2].TireFx,6:F0} Td={w[2].DriveTorque,6:F0}");
                pc = v.Drivetrain.Clutch; pg = v.Gear;
            }
            if (i % 120 == 0)
            {
                var w = v.Wheels;
                Console.WriteLine(
                    $"{i * dt,5:F1} {v.SpeedKmh,5:F1} {v.Gear,4} {v.Drivetrain.Clutch} {v.EngineRpm,8:F0} {v.Drivetrain.AxleOmega,7:F2} " +
                    $"[{w[0].SpinRate,6:F1} {w[1].SpinRate,6:F1} {w[2].SpinRate,6:F1} {w[3].SpinRate,6:F1}] " +
                    $"{w[2].SlipRatio,5:F2} {w[2].TireFx,6:F0} {w[2].DriveTorque,6:F0} {v.AngularVelocity.Y,6:F2} {v.Position.X,6:F2} {v.Position.Z,7:F2}");
            }
        }
    }
}
