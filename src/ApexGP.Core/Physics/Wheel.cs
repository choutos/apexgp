using System.Numerics;

namespace ApexGP.Core;

public sealed class Wheel
{
    public readonly WheelConfig Config;

    public double SteerAngleRad;
    public double SpinRate;               // rad/s, positive rolling forward
    public double PrevCompression;        // m, previous sub-step
    public double SuspensionCompression;  // m
    public double SuspensionForce;        // N along chassis up axis (post-ARB)
    public double ContactLoad;            // N vertical load seen by the tire
    public bool InContact;
    public double SlipRatio;
    public double SlipAngleDeg;
    public double SmoothedKappa, SmoothedAlphaDeg;  // relaxation-filtered slip (force input)
    public double ContactVx;                        // road speed along wheel forward (m/s)
    public double DriveTorque;                      // Nm at the wheel
    public double BrakeTorque;            // Nm (ABS-adjusted)
    public double TireFx, TireFy;         // last computed tire forces (wheel frame)
    public Vector3 ForceWorld;

    public Wheel(WheelConfig config) { Config = config; }
}
