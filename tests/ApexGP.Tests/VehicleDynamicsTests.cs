using System.Numerics;
using ApexGP.Core;

namespace ApexGP.Tests;

public static class VehicleDynamicsTests
{
    public static void IdlesAtStandstill()
    {
        var v = TestCars.PrototypeGtB();
        TestCars.Settle(v, 3.0);
        Checks.Check(!v.Diverged, "diverged during idle");
        double rpm = v.EngineRpm;
        Checks.Check(rpm >= 700 && rpm <= 950, $"idle rpm {rpm:F0} outside [700, 950]");
        Checks.Check(v.SpeedKmh < 0.5, $"car creeps at {v.SpeedKmh:F2} km/h");
    }

    public static void AcceleratesTo100Kmh()
    {
        var v = TestCars.PrototypeGtB();
        TestCars.Settle(v);
        v.Throttle = 1.0;
        double t = TestCars.TimeTo(v, kmh => kmh >= 100.0, maxSeconds: 20.0);
        Checks.Check(t > 0, "did not reach 100 km/h in 20 s");
        Checks.Check(t >= 6.0 && t <= 9.5, $"0-100 km/h in {t:F2} s, outside [6.0, 9.5]");
        Console.WriteLine($"      (0-100 km/h: {t:F2} s)");
        Checks.Check(Math.Abs(v.Position.X) < 2.0, $"lateral drift {v.Position.X:F2} m");
        Checks.Check(!v.Diverged, "diverged during acceleration");
    }

    public static void BrakesFrom100Kmh()
    {
        var v = TestCars.PrototypeGtB();
        TestCars.Settle(v);
        v.Throttle = 1.0;
        double tAcc = TestCars.TimeTo(v, kmh => kmh >= 99.5, maxSeconds: 20.0);
        Checks.Check(tAcc > 0, "did not reach 100 km/h");
        TestCars.Simulate(v, 0.1);
        Vector3 startPos = v.Position;
        double startHeading = HeadingOf(v);
        double startKmh = v.SpeedKmh;
        v.Throttle = 0.0;
        v.Brake = 1.0;
        double dt = 1.0 / 240.0, t = 0;
        while (v.SpeedKmh > 0.5 && t < 8.0)
        {
            v.Step(dt);
            t += dt;
            if (Math.Abs(t - Math.Round(t * 4) / 4.0) < dt / 2)
                Console.WriteLine($"      brake@{t:F2}s: {v.SpeedKmh:F1} km/h, rearK={v.Wheels[2].SlipRatio:F2}, fK={v.Wheels[0].SlipRatio:F2}, loads=({v.Wheels[0].ContactLoad:F0}/{v.Wheels[2].ContactLoad:F0})");
        }
        double dist = (v.Position - startPos).Length();
        double dHead = AngleDiff(HeadingOf(v), startHeading);
        Checks.Check(v.SpeedKmh <= 0.6, $"still moving at {v.SpeedKmh:F2} km/h after full braking");
        Checks.Check(dist <= 55.0, $"braking distance {dist:F1} m > 55 m");
        Checks.Check(t <= 5.0, $"stopping time {t:F2} s > 5 s");
        Checks.Check(Math.Abs(dHead) < 10, $"car rotated {dHead:F1}° while braking straight");
        Checks.Check(!v.Diverged, "diverged during braking");
    }

    public static void SteadyStateTopSpeed()
    {
        var v = TestCars.PrototypeGtB();
        TestCars.Settle(v);
        v.Throttle = 1.0;
        TestCars.Simulate(v, 40.0);
        double kmh = v.SpeedKmh;
        Checks.Check(kmh >= 150 && kmh <= 213, $"top speed {kmh:F1} km/h outside [150, 213]");
        Checks.Check(v.EngineRpm <= v.Config.Drivetrain.RedlineRpm * 1.05, $"rpm {v.EngineRpm:F0} above redline");
        Checks.Check(!v.Diverged, "diverged at top speed");
    }

    public static void CorneringSteersAndStaysSane()
    {
        var v = TestCars.PrototypeGtB();
        TestCars.Settle(v);
        v.Throttle = 1.0;
        double t0 = TestCars.TimeTo(v, kmh => kmh >= 58.0, maxSeconds: 15.0);
        Checks.Check(t0 > 0, "did not reach 58 km/h");
        TestCars.Simulate(v, 0.2);
        v.Throttle = 0.35;
        v.SteerInput = 0.35;
        double startHeading = HeadingOf(v);
        double dt = 1.0 / 240.0, t = 0, maxAy = 0;
        while (t < 6.0)
        {
            v.Step(dt);
            t += dt;
            double ay = Math.Abs(v.SpeedKmh / 3.6 * v.AngularVelocity.Y);
            if (ay > maxAy) maxAy = ay;
        }
        double dHead = AngleDiff(HeadingOf(v), startHeading);
        Checks.Check(Math.Abs(dHead) >= 25, $"only {Math.Abs(dHead):F1}° of heading change — car didn't turn");
        Checks.Check(Math.Abs(dHead) <= 400, $"heading changed {dHead:F1}° — spun out");
        Checks.Check(maxAy <= 1.5 * 9.81, $"lateral accel {maxAy / 9.81:F2} g exceeds 1.5 g");
        Checks.Check(v.SpeedKmh > 30, $"speed dropped to {v.SpeedKmh:F1} km/h");
        Checks.Check(!v.Diverged, "diverged while cornering");
    }

    public static void MixedRun120sStable()
    {
        var v = TestCars.PrototypeGtB();
        TestCars.Settle(v);
        double dt = 1.0 / 240.0, t = 0;
        while (t < 120.0)
        {
            if (t < 10) { v.Throttle = 1.0; v.Brake = 0; v.SteerInput = 0; }
            else if (t < 14) { v.Throttle = 0; v.Brake = 0.6; }
            else if (t < 24) { v.Throttle = 1.0; v.Brake = 0; v.SteerInput = Math.Sin(t * 0.8) * 0.6; }
            else if (t < 29) { v.Throttle = 0; v.Brake = 1.0; }
            else if (t < 90) { v.Throttle = 0.5 + 0.5 * Math.Sin(t * 1.3); v.Brake = 0; v.SteerInput = Math.Sin(t * 0.5) * 0.6; }
            else { v.Throttle = 1.0; v.Brake = 0; v.SteerInput = 0; }

            v.Step(dt);
            t += dt;
            Checks.Check(!v.Diverged, $"diverged at t={t:F1}s");
            Checks.Check(v.SpeedKmh <= 225, $"speed {v.SpeedKmh:F1} km/h at t={t:F1}s");
            if (!double.IsFinite(v.Position.X) || !double.IsFinite(v.Position.Z))
                Checks.Check(false, $"position not finite at t={t:F1}s");
        }
    }

    public static void WeightTransferUnderAcceleration()
    {
        var v = TestCars.PrototypeGtB();
        TestCars.Settle(v);
        double front0 = v.Wheels[0].SuspensionCompression + v.Wheels[1].SuspensionCompression;
        double rear0 = v.Wheels[2].SuspensionCompression + v.Wheels[3].SuspensionCompression;
        Checks.CheckNear(front0, rear0, 0.02, "static front/rear compression mismatch");
        v.Throttle = 1.0;
        TestCars.Simulate(v, 2.0);   // sample mid-1st-gear plateau, before the 1→2 shift cuts torque
        double front = v.Wheels[0].SuspensionCompression + v.Wheels[1].SuspensionCompression;
        double rear = v.Wheels[2].SuspensionCompression + v.Wheels[2].SuspensionCompression;
        rear = v.Wheels[2].SuspensionCompression + v.Wheels[3].SuspensionCompression;
        Checks.Check(rear > front + 0.005, $"no squat under power: rear {rear:F4} vs front {front:F4}");
        Checks.Check(!v.Diverged, "diverged");
    }

    public static void BrakesFromLowSpeed()
    {
        // Replicate the Godot shell exactly: spawn, 1.5 s WOT at 1/120 s substeps, full brake.
        var v = TestCars.PrototypeGtB();
        for (int i = 0; i < 180; i++)
        {
            v.Throttle = 1.0;
            v.Step(1.0 / 120.0);
            v.Step(1.0 / 120.0);
        }
        double startKmh = v.SpeedKmh;
        v.Throttle = 0.0;
        v.Brake = 1.0;
        double dt = 1.0 / 120.0, t = 0;
        while (v.SpeedKmh > 0.5 && t < 5.0)
        {
            v.Step(dt);
            t += dt;
            if (Math.Abs(t - Math.Round(t * 2) / 2.0) < dt / 2)
                Console.WriteLine($"      brake@{t:F2}s: {v.SpeedKmh:F2} km/h, clutch={v.Drivetrain.Clutch}, rpm={v.EngineRpm:F0}, rearK={v.Wheels[2].SlipRatio:F2}");
        }
        Console.WriteLine($"      (brake from {startKmh:F1} km/h: {t:F2} s, dt=1/120)");
        Checks.Check(t <= startKmh / 24.0, $"low-speed braking too slow: {t:F2} s (start {startKmh:F1} km/h → need ≥24 km/h/s)");
    }

    private static double HeadingOf(Vehicle v)
    {
        Vector3 fwd = Vector3.Transform(new Vector3(0f, 0f, -1f), v.Orientation);
        return Math.Atan2(-fwd.X, -fwd.Z) * 180.0 / Math.PI;
    }

    private static double AngleDiff(double a, double b)
    {
        double d = a - b;
        while (d > 180) d -= 360;
        while (d < -180) d += 360;
        return d;
    }
}
