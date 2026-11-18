namespace ApexGP.Core;

public enum ClutchState { Slipping, Locked }
public enum DriveType { Rwd, Fwd, Awd }

public sealed class Drivetrain
{
    public readonly DrivetrainConfig Cfg;
    public DriveType Type = DriveType.Rwd;

    public int Gear = 1;                  // 1-based; 0 = neutral
    public double EngineOmega;            // rad/s
    public double AxleOmega;              // rad/s, driven axle (locked state)
    public ClutchState Clutch = ClutchState.Slipping;
    public double ShiftTimer;

    public Drivetrain(DrivetrainConfig cfg)
    {
        Cfg = cfg;
        EngineOmega = cfg.IdleRpm * Math.PI / 30.0;
    }

    public double Ratio =>
        Gear >= 1 && Gear <= Cfg.GearRatios.Length
            ? Cfg.GearRatios[Gear - 1] * Cfg.FinalDrive
            : 0.0;

    public double EngineRpm => EngineOmega * 30.0 / Math.PI;

    /// <summary>Torque the clutch may transmit: throttle pedal only, no idle governor.</summary>
    public double ThrottleTorque(double throttle)
    {
        double rpm = EngineRpm;
        if (rpm >= Cfg.RedlineRpm) return 0;
        double tq = CurveLookup(rpm) * throttle;
        if (rpm > Cfg.RedlineRpm * 0.98) tq = Math.Min(tq, 0);
        return tq;
    }

    /// <summary>Net crank torque for the engine's own free-revving integration (governor included).</summary>
    public double EngineTorque(double throttle)
    {
        double tq = ThrottleTorque(throttle);
        double idleErr = Cfg.IdleRpm - EngineRpm;
        double idleT = Math.Clamp(idleErr * 4.0, 0, 200);
        return tq + idleT * (1.0 - throttle);
    }

    public double EngineFrictionTorque() => Cfg.FrictionBase + Cfg.FrictionSlope * EngineOmega;

    private double CurveLookup(double rpm)
    {
        var pts = Cfg.TorqueCurve;
        if (rpm <= pts[0].Rpm) return pts[0].Torque;
        for (int i = 1; i < pts.Length; i++)
        {
            if (rpm <= pts[i].Rpm)
            {
                double f = (rpm - pts[i - 1].Rpm) / (pts[i].Rpm - pts[i - 1].Rpm);
                return pts[i - 1].Torque + f * (pts[i].Torque - pts[i - 1].Torque);
            }
        }
        return pts[^1].Torque;
    }
}
