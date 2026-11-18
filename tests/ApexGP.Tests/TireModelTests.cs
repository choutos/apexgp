using ApexGP.Core;

namespace ApexGP.Tests;

public static class TireModelTests
{
    public static void PeakLateralSlipInPlausibleRange()
    {
        const double mu = 1.05, load = 4000;
        double bestFy = 0, bestAngle = 0;
        for (double a = 0; a <= 30; a += 0.25)
        {
            double fy = Math.Abs(TireModel.Lateral(a, load, mu));
            if (fy > bestFy) { bestFy = fy; bestAngle = a; }
        }
        Checks.Check(bestAngle >= 3 && bestAngle <= 12, $"peak lateral slip {bestAngle:F1}° outside [3, 12]");
        Checks.Check(bestFy <= mu * load * 1.0001 + 1e-6, $"tire exceeds friction limit: {bestFy:F0} > {mu * load:F0}");
        Checks.Check(bestFy >= 0.85 * mu * load, $"lateral peak too low: {bestFy:F0}");
    }

    public static void PeakLongitudinalSlipInPlausibleRange()
    {
        const double mu = 1.05, load = 4000;
        double bestFx = 0, bestSlip = 0;
        for (double k = 0; k <= 0.6; k += 0.005)
        {
            double fx = Math.Abs(TireModel.Longitudinal(k, load, mu));
            if (fx > bestFx) { bestFx = fx; bestSlip = k; }
        }
        Checks.Check(bestSlip >= 0.05 && bestSlip <= 0.20, $"peak slip ratio {bestSlip:F3} outside [0.05, 0.20]");
        Checks.Check(bestFx <= mu * load * 1.0001 + 1e-6, $"tire exceeds friction limit: {bestFx:F0} > {mu * load:F0}");
    }

    public static void OddSymmetry()
    {
        double pos = TireModel.Lateral(5.0, 4000, 1.05);
        double neg = TireModel.Lateral(-5.0, 4000, 1.05);
        Checks.CheckNear(pos, -neg, 1e-12, "lateral asymmetry");
        double fp = TireModel.Longitudinal(0.05, 4000, 1.05);
        double fn = TireModel.Longitudinal(-0.05, 4000, 1.05);
        Checks.CheckNear(fp, -fn, 1e-12, "longitudinal asymmetry");
    }

    public static void LoadSensitivityReducesPerNewton()
    {
        double f4000 = TireModel.Lateral(5.0, 4000, 1.05);
        double f8000 = TireModel.Lateral(5.0, 8000, 1.05);
        Checks.Check(f8000 < 1.99 * f4000, $"load sensitivity missing: doubling load gives {f8000 / f4000:F3}x force");
        Checks.Check(f8000 > 1.5 * f4000, $"load sensitivity too aggressive: {f8000 / f4000:F3}x");
    }

    public static void BrakingSlipGivesNegativeForce()
    {
        double fx = TireModel.Longitudinal(-0.2, 4000, 1.05);
        Checks.Check(fx < 0, $"locked wheel must decelerate: Fx = {fx:F0}");
    }
}
