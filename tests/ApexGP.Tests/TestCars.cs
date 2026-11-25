using System.Numerics;
using ApexGP.Core;

namespace ApexGP.Tests;

/// <summary>Shared fixtures and simulation helpers (dependency-free, no xunit).</summary>
public static class TestCars
{
    public static Vehicle PrototypeGtB()
    {
        // Spawn 1 m above the (implicit flat) ground, exactly like the game shell,
        // so suspension/pitch dynamics are measured in the real resting geometry.
        var v = new Vehicle(VehicleConfig.PrototypeGtB())
        {
            Position = new System.Numerics.Vector3(0, 1.0f, 0),
        };
        return v;
    }

    public static void Simulate(Vehicle v, double seconds, double dt = 1.0 / 240.0)
    {
        int n = (int)Math.Round(seconds / dt);
        for (int i = 0; i < n; i++) v.Step(dt);
    }

    public static void Settle(Vehicle v, double seconds = 1.5) => Simulate(v, seconds);

    /// <returns>Elapsed seconds when the condition first holds, or -1 if never.</returns>
    public static double TimeTo(Vehicle v, Func<double, bool> done, double maxSeconds, double dt = 1.0 / 240.0)
    {
        double t = 0;
        while (t < maxSeconds)
        {
            v.Step(dt);
            t += dt;
            if (done(v.SpeedKmh)) return t;
        }
        return -1;
    }
}

public static class Checks
{
    public static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    public static void CheckNear(double actual, double expected, double tolerance, string what)
    {
        Check(Math.Abs(actual - expected) <= tolerance,
              $"{what}: expected {expected:F3} ± {tolerance:F3}, got {actual:F3}");
    }

    public static void Throws<T>(Action action) where T : Exception
    {
        try
        {
            action();
        }
        catch (T)
        {
            return;
        }
        catch (Exception ex)
        {
            Check(false, $"expected {typeof(T).Name}, got {ex.GetType().Name}: {ex.Message}");
            return;
        }
        Check(false, $"expected {typeof(T).Name} but nothing was thrown");
    }
}
