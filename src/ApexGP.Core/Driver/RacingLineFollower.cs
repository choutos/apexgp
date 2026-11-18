namespace ApexGP.Core;

/// <summary>
/// A driver that follows the racing line: P-steering toward a lookahead point,
/// curvature-based speed target with throttle/brake control.
/// Used by the AI (Loop 3) and by the track/lap tests.
/// </summary>
public sealed class RacingLineFollower
{
    private readonly TrackSurface _track;
    private int _idx;

    /// <summary>1.0 = full attack; <1 for slower AI.</summary>
    public double SpeedFactor = 1.0;
    /// <summary>Lookahead distance scale (seconds of travel).</summary>
    public double LookaheadTime = 0.55;

    public double Throttle, Brake, Steer;
    private double _stuckTime;

    public RacingLineFollower(TrackSurface track) => _track = track;

    public void Drive(Vehicle v, double dt)
    {
        double speed = System.Math.Max(1.0, v.SpeedKmh / 3.6);
        int n = _track.Samples.Length;
        _idx = _track.NearestIndex(v.Position.X, v.Position.Z, _idx);

        // lateral state (for steering gain) — target point on the racing line ahead
        int ahead = (int)(speed * LookaheadTime / 4.0) + 3; // samples are ~4 m apart
        int ti = (_idx + ahead) % n;
        var (tx, tz) = _track.RacingLineAt(ti);

        // steering: angle between car heading and target direction, plus a lateral
        // correction that pulls the car back onto the racing line
        var fwd = System.Numerics.Vector3.Transform(new System.Numerics.Vector3(0, 0, -1), v.Orientation);
        double heading = System.Math.Atan2(-fwd.X, -fwd.Z);    // 0 = -Z, positive = left turn
        double target = System.Math.Atan2(-(tx - v.Position.X), -(tz - v.Position.Z));
        double err = WrapAngle(target - heading);
        double lateral = _track.LateralOffset(_idx, v.Position.X, v.Position.Z);
        double steerCmd = System.Math.Clamp(err * 3.0 + System.Math.Clamp(lateral * 0.06, -0.35, 0.35), -1, 1);
        Steer = steerCmd;

        // speed target from the precomputed braking-aware profile
        double vTarget = _track.SpeedTargets[_idx] * SpeedFactor;
        double vNow = v.SpeedKmh / 3.6;
        if (vNow < vTarget - 0.5) { Throttle = 1; Brake = 0; }
        else if (vNow > vTarget + 0.5) { Throttle = 0; Brake = System.Math.Clamp((vNow - vTarget) * 0.35, 0.15, 1); }
        else { Throttle = 0.35; Brake = 0; }
        // gentle traction assist: ease the throttle under steering to keep the RWD car settled
        Throttle *= 1.0 - System.Math.Min(System.Math.Abs(steerCmd) * 0.25, 0.25);

        // stuck recovery: if beached (too slow for too long), punch the throttle
        if (vNow < 0.7) _stuckTime += dt; else _stuckTime = 0;
        if (_stuckTime > 1.5)
        {
            Throttle = 1.0;
            Brake = 0.0;
            if (_stuckTime > 6.0) _stuckTime = 4.0; // re-arm so we keep trying
        }

        v.Throttle = Throttle;
        v.Brake = Brake;
        v.SteerInput = Steer;
    }

    public static double WrapAngle(double a)
    {
        while (a > System.Math.PI) a -= 2 * System.Math.PI;
        while (a < -System.Math.PI) a += 2 * System.Math.PI;
        return a;
    }
}
