namespace ApexGP.Core;

/// <summary>
/// Pacejka Magic Formula tire model, normalized so the peak sits at a known
/// slip value. Odd-symmetric in slip. Combined slip via friction ellipse.
/// </summary>
public static class TireModel
{
    public const double LatB = 4.0, LatC = 1.30, LatE = 0.50;
    public const double LonB = 1.873, LonC = 1.65, LonE = 0.60;
    public const double LatPeakSlipDeg = 7.2;
    public const double LonPeakSlip = 0.11;

    public static double Magic(double x, double B, double C, double E)
    {
        double bx = B * x;
        return Math.Sin(C * Math.Atan(bx - E * (bx - Math.Atan(bx))));
    }

    /// <param name="slipAngleDeg">Positive when sliding toward the wheel's +side.</param>
    public static double Lateral(double slipAngleDeg, double loadN, double baseMu)
    {
        if (loadN <= 0) return 0;
        return LoadSensitiveMu(baseMu, loadN) * loadN * Magic(slipAngleDeg / LatPeakSlipDeg, LatB, LatC, LatE);
    }

    /// <param name="slipRatio">Positive when the wheel spins faster than the road.</param>
    public static double Longitudinal(double slipRatio, double loadN, double baseMu)
    {
        if (loadN <= 0) return 0;
        return LoadSensitiveMu(baseMu, loadN) * loadN * Magic(slipRatio / LonPeakSlip, LonB, LonC, LonE);
    }

    /// <summary>Mild load sensitivity: grip per Newton falls as load rises.</summary>
    public static double LoadSensitiveMu(double baseMu, double loadN)
    {
        double mu = baseMu * (1.0 - 0.000015 * (loadN - 4000.0));
        return Math.Clamp(mu, 0.7 * baseMu, baseMu);
    }

    public static (double Fx, double Fy) Combined(double slipRatio, double slipAngleDeg, double loadN, double baseMu)
    {
        if (loadN <= 0) return (0, 0);
        double fx = Longitudinal(slipRatio, loadN, baseMu);
        double fy = Lateral(slipAngleDeg, loadN, baseMu);
        double fmax = LoadSensitiveMu(baseMu, loadN) * loadN;
        double s = Math.Sqrt((fx / fmax) * (fx / fmax) + (fy / fmax) * (fy / fmax));
        if (s > 1.0)
        {
            double k = 1.0 / s;
            fx *= k;
            fy *= k;
        }
        return (fx, fy);
    }
}
