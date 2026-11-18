using System.Numerics;

namespace ApexGP.Core;

/// <summary>
/// Rigid-body vehicle: Pacejka tires, independent suspension, clutch/gearbox
/// drivetrain. Frame convention: Y up, -Z forward, X right (Godot convention).
/// Flat ground at y = 0 (surface model deferred). Physics sub-step ~1/240 s.
/// </summary>
public sealed class Vehicle
{
    public readonly VehicleConfig Config;
    public readonly Wheel[] Wheels;      // as configured (index order matters)
    public readonly Drivetrain Drivetrain;
    private readonly int[] _driven;

    public Vector3 Position;
    public Quaternion Orientation = Quaternion.Identity;
    public Vector3 Velocity;             // world frame
    public Vector3 AngularVelocity;      // body frame

    public double SteerInput;            // -1..1, positive = left
    public double Throttle;              // 0..1
    public double Brake;                 // 0..1

    public bool Diverged { get; private set; }

    private const double AirDensity = 1.22;
    private const double G0 = 9.81;

    public Vehicle(VehicleConfig config)
    {
        Config = config;
        Wheels = new Wheel[config.Wheels.Length];
        for (int i = 0; i < config.Wheels.Length; i++)
            Wheels[i] = new Wheel(config.Wheels[i]);
        Drivetrain = new Drivetrain(config.Drivetrain);
        _driven = Enumerable.Range(0, config.Wheels.Length)
                            .Where(i => config.Wheels[i].IsDriven)
                            .ToArray();
        Position = new Vector3(0f, 1.0f, 0f);
    }

    public double SpeedKmh => Velocity.Length() * 3.6;
    public double EngineRpm => Drivetrain.EngineRpm;
    public int Gear => Drivetrain.Gear;

    public void Step(double dt)
    {
        if (dt <= 0 || dt > 0.1) return;
        var snap = Capture();

        var q = Orientation;
        var invQ = Conj(q);
        Vector3 up = Vector3.Transform(Vector3.UnitY, q);
        Vector3 fwd = Vector3.Transform(new Vector3(0f, 0f, -1f), q);

        Vector3 force = new Vector3(0f, (float)(-G0 * Config.MassKg), 0f);
        Vector3 torque = Vector3.Zero;

        double speed = Velocity.Length();
        if (speed > 0.5)
        {
            double dragMag = 0.5 * AirDensity * Config.CdA * speed * speed;
            force -= Velocity * ((float)(dragMag / speed));
        }

        UpdateSuspension(dt, q, up, ref force, ref torque);
        UpdateDrivetrainState(dt, speed);
        UpdateTireForces(dt, q, up, fwd, ref force, ref torque);
        IntegrateWheels(dt);
        IntegrateChassis(dt, force, torque, q, invQ);

        if (HasNaN())
        {
            Diverged = true;
            Restore(snap);
        }
    }

    // ---------------- suspension ----------------

    private void UpdateSuspension(double dt, Quaternion q, Vector3 up, ref Vector3 force, ref Vector3 torque)
    {
        var s = Config.Suspension;
        var comp = new double[Wheels.Length];

        for (int i = 0; i < Wheels.Length; i++)
        {
            var w = Wheels[i];
            Vector3 hub = Position + Vector3.Transform(w.Config.MountLocal, q);
            // ray from hub along -up; it reaches the plane at t = hubY / up.Y
            double rayDownY = up.Y;
            double compression = 0;
            w.InContact = false;
            if (rayDownY > 1e-4)
            {
                if (hub.Y <= 0)
                {
                    compression = s.MaxTravel; // chassis embedded: full bump
                    w.InContact = true;
                }
                else
                {
                    double dist = hub.Y / rayDownY;
                    double maxLen = w.Config.Radius + s.RestLength + s.MaxTravel;
                    if (dist <= maxLen)
                    {
                        compression = w.Config.Radius + s.RestLength - dist;
                        if (compression > 0) w.InContact = true;
                    }
                }
            }
            comp[i] = compression;
        }

        for (int i = 0; i < Wheels.Length; i++)
        {
            var w = Wheels[i];
            double compVel = (comp[i] - w.PrevCompression) / dt;
            compVel = Math.Clamp(compVel, -10.0, 10.0);
            w.PrevCompression = comp[i];
            w.SuspensionCompression = comp[i];
            double f = 0;
            if (w.InContact)
            {
                f = s.SpringStiffness * comp[i] + s.DamperRate * compVel;
                if (comp[i] > s.MaxTravel) f += s.BumpStiffness * (comp[i] - s.MaxTravel);
            }
            w.SuspensionForce = Math.Max(0, f);
        }

        // anti-roll bars, per axle (pairs: 0-1 front, 2-3 rear)
        for (int axle = 0; axle * 2 + 1 < Wheels.Length; axle++)
        {
            int l = axle * 2, r = axle * 2 + 1;
            double diff = comp[l] - comp[r];
            Wheels[l].SuspensionForce = Math.Max(0, Wheels[l].SuspensionForce + s.AntiRollStiffness * diff);
            Wheels[r].SuspensionForce = Math.Max(0, Wheels[r].SuspensionForce - s.AntiRollStiffness * diff);
        }

        for (int i = 0; i < Wheels.Length; i++)
        {
            var w = Wheels[i];
            w.ContactLoad = w.SuspensionForce;
            Vector3 hub = Position + Vector3.Transform(w.Config.MountLocal, q);
            Vector3 fWorld = up * ((float)w.SuspensionForce);
            force += fWorld;
            torque += Vector3.Cross(hub - Position, fWorld);
        }
    }

    // ---------------- drivetrain ----------------

    private void UpdateDrivetrainState(double dt, double speed)
    {
        var d = Drivetrain;
        var dc = d.Cfg;

        // brakes (ABS uses smoothed slip: the filter is what the tire actually feels)
        foreach (var w in Wheels)
        {
            double t = w.Config.MaxBrakeTorque * Brake;
            if (Config.AbsEnabled && w.SmoothedKappa < -0.35) t *= 0.25;
            w.BrakeTorque = t;
        }

        // gearbox
        if (d.ShiftTimer > 0) d.ShiftTimer -= dt;
        if (d.ShiftTimer <= 0 && d.Clutch == ClutchState.Locked && d.Gear >= 1)
        {
            if (d.EngineRpm > dc.ShiftUpRpm && d.Gear < dc.GearRatios.Length && speed > 5 && Throttle > 0.2)
            {
                d.Gear++;
                d.ShiftTimer = dc.ShiftTime;
                d.Clutch = ClutchState.Slipping;
            }
            else if (d.EngineRpm < dc.ShiftDownRpm && d.Gear > 1 && speed > 2)
            {
                d.Gear--;
                d.ShiftTimer = dc.ShiftTime;
                d.Clutch = ClutchState.Slipping;
            }
        }

        double axleOmega = 0.0;
        foreach (int i in _driven) axleOmega += Wheels[i].SpinRate;
        if (_driven.Length > 0) axleOmega /= _driven.Length;

        // clutch state machine
        if (d.Clutch == ClutchState.Locked)
        {
            bool stallThreat = d.EngineRpm < dc.IdleRpm * 0.5;
            bool parked = speed < 0.4 && Throttle < 0.05;
            if (d.Gear < 1 || stallThreat || parked) d.Clutch = ClutchState.Slipping;
        }
        else if (d.Gear >= 1 && d.ShiftTimer <= 0)
        {
            double expected = axleOmega * d.Ratio;
            double mismatch = Math.Abs(d.EngineOmega - expected);
            if ((Throttle > 0.02 && mismatch < 0.03 * Math.Max(d.EngineOmega, 5.0)) ||
                (Throttle <= 0.02 && speed > 2.0))
            {
                d.Clutch = ClutchState.Locked;
                d.AxleOmega = axleOmega; // carry the wheels' current spin into the rigid state
            }
        }

        double gf = d.Ratio;
        double drivePerWheel = 0.0;

        if (d.Clutch == ClutchState.Locked && d.Gear >= 1)
        {
            d.EngineOmega = axleOmega * gf;
            double tAxle = (d.ThrottleTorque(Throttle) - d.EngineFrictionTorque()) * gf * dc.Efficiency;
            drivePerWheel = tAxle / _driven.Length;
        }
        else
        {
            // viscous clutch: T = K·Δω, clamped (power side full, coast side light).
            // The idle governor drives the engine only — it never reaches the wheels,
            // and a coasting/braking car gets no creep drive.
            double tEng = d.EngineTorque(Throttle);
            double transmit = 0.0;
            if (Throttle > 0.05 && d.Gear >= 1 && d.ShiftTimer <= 0)
            {
                double mismatch = d.EngineOmega - axleOmega * gf;
                transmit = Math.Clamp(60.0 * mismatch, -40.0, dc.ClutchCapacity);
            }
            drivePerWheel = transmit * gf * dc.Efficiency / _driven.Length;

            double netT = tEng - d.EngineFrictionTorque() - transmit - 0.3 * d.EngineOmega;
            d.EngineOmega += netT / dc.EngineInertia * dt;
            // The driver holds revs: never below the launch floor nor below the
            // road-matched speed (so Δω ≥ 0 during a launch → smooth engagement).
            double floor = dc.IdleRpm * 0.5 * Math.PI / 30.0;
            if (Throttle > 0.3 && d.Gear == 1)
                floor = Math.Max(floor, Math.Max(dc.LaunchFloorRpm * Math.PI / 30.0, axleOmega * gf));
            if (d.EngineOmega < floor) d.EngineOmega = floor;
        }

        // traction control: progressive cut, driven by smoothed slip
        foreach (int i in _driven)
        {
            double tq = drivePerWheel;
            double sk = Wheels[i].SmoothedKappa;
            if (Config.TractionControlEnabled && sk > 0.3)
                tq *= Math.Max(0.3, 1.0 - (sk - 0.3) * 2.0);
            Wheels[i].DriveTorque = tq;
        }
        foreach (var w in Wheels)
            if (!w.Config.IsDriven) w.DriveTorque = 0;
    }

    // ---------------- tires ----------------

    private void UpdateTireForces(double dt, Quaternion q, Vector3 up, Vector3 fwd, ref Vector3 force, ref Vector3 torque)
    {
        Vector3 omegaWorld = Vector3.Transform(AngularVelocity, q);
        double steerAngle = SteerInput * Config.MaxSteerAngleDeg * Math.PI / 180.0;

        for (int i = 0; i < Wheels.Length; i++)
        {
            var w = Wheels[i];
            w.TireFx = 0; w.TireFy = 0;
            w.ForceWorld = Vector3.Zero;
            if (!w.InContact || w.ContactLoad <= 1.0)
            {
                w.SlipRatio = 0;
                w.SlipAngleDeg = 0;
                continue;
            }

            double sa = w.Config.IsFront ? steerAngle : 0.0;
            w.SteerAngleRad = sa;
            Vector3 wFwd = sa == 0 ? fwd : Vector3.Transform(fwd, Quaternion.CreateFromAxisAngle(up, (float)sa));
            Vector3 wSide = Vector3.Cross(wFwd, up);

            Vector3 hub = Position + Vector3.Transform(w.Config.MountLocal, q);
            Vector3 cp = new Vector3(hub.X, 0f, hub.Z); // contact point on flat ground
            Vector3 vContact = Velocity + Vector3.Cross(omegaWorld, cp - Position);
            double vx = Vector3.Dot(vContact, wFwd);
            double vy = Vector3.Dot(vContact, wSide);

            double denom = Math.Max(Math.Abs(vx), 2.0);
            double kappa = (w.SpinRate * w.Config.Radius - vx) / denom;
            double alphaDeg = Math.Atan2(vy, Math.Max(Math.Abs(vx), 1.5)) * (180.0 / Math.PI);
            w.ContactVx = vx;

            // Relaxation filter on slip: damps the wheel-inertia <-> tire-force
            // feedback loop at low speed (classic game-sim trick, ~20 ms lag).
            const double Tau = 0.02;
            double k = 1.0 - Math.Exp(-dt / Tau);
            w.SmoothedKappa += (kappa - w.SmoothedKappa) * k;
            w.SmoothedAlphaDeg += (alphaDeg - w.SmoothedAlphaDeg) * k;
            w.SlipRatio = kappa;
            w.SlipAngleDeg = alphaDeg;

            var (fx, fy) = TireModel.Combined(w.SmoothedKappa, w.SmoothedAlphaDeg, w.ContactLoad, Config.TireMuBase);
            // Lateral: Fy opposes slip (positive slip = sliding +side, force acts -side).
            fy = -fy;
            w.TireFx = fx;
            w.TireFy = fy;

            Vector3 fW = wFwd * (float)fx + wSide * (float)fy;
            w.ForceWorld = fW;
            force += fW;
            torque += Vector3.Cross(cp - Position, fW);
        }
    }

    // ---------------- integration ----------------

    private void IntegrateWheels(double dt)
    {
        bool locked = Drivetrain.Clutch == ClutchState.Locked && Drivetrain.Gear >= 1;

        if (locked)
        {
            double gf = Drivetrain.Ratio;
            double J = Drivetrain.Cfg.EngineInertia * gf * gf;
            double tSum = 0, react = 0, brk = 0;
            foreach (int i in _driven)
            {
                var w = Wheels[i];
                J += w.Config.Inertia;
                tSum += w.DriveTorque;
                react += w.TireFx * w.Config.Radius;
                brk += w.BrakeTorque * (w.SpinRate >= 0 ? 1 : -1);
            }
            double nw = Drivetrain.AxleOmega + (tSum - react - brk) / J * dt;
            if (nw < 0) nw = 0;
            Drivetrain.AxleOmega = nw;
            foreach (int i in _driven) Wheels[i].SpinRate = nw;
        }

        // Kinematic rolling for coasting wheels (no drive, no brake): spin follows
        // the road exactly. This kills the low-speed slip oscillation that ratchets
        // the car forward. Braked or driven wheels integrate dynamically.
        for (int i = 0; i < Wheels.Length; i++)
        {
            var w = Wheels[i];
            if (locked && Array.IndexOf(_driven, i) >= 0) continue;
            if (w.DriveTorque < 0.5 && w.BrakeTorque < 0.5)
            {
                if (w.InContact) w.SpinRate = w.ContactVx / w.Config.Radius;
                continue;
            }
            double s = w.SpinRate >= 0 ? 1 : -1;
            double nw = w.SpinRate + (w.DriveTorque - w.TireFx * w.Config.Radius - w.BrakeTorque * s) / w.Config.Inertia * dt;
            if (nw < 0) nw = 0;
            w.SpinRate = nw;
        }

        foreach (var w in Wheels)
            if (Math.Abs(w.SpinRate) < 0.05 && w.DriveTorque < 1.0 && w.BrakeTorque > 1.0)
                w.SpinRate = 0;
    }

    private void IntegrateChassis(double dt, Vector3 force, Vector3 torque, Quaternion q, Quaternion invQ)
    {
        Velocity += force * ((float)(dt / Config.MassKg));
        Position += Velocity * ((float)dt);

        Vector3 tBody = Vector3.Transform(torque, invQ);
        Vector3 w = AngularVelocity;
        Vector3 Iw = new Vector3(
            (float)(Config.InertiaPitch * w.X),
            (float)(Config.InertiaYaw * w.Y),
            (float)(Config.InertiaRoll * w.Z));
        Vector3 gyro = Vector3.Cross(w, Iw);
        AngularVelocity += new Vector3(
            (float)((tBody.X - gyro.X) / Config.InertiaPitch),
            (float)((tBody.Y - gyro.Y) / Config.InertiaYaw),
            (float)((tBody.Z - gyro.Z) / Config.InertiaRoll)) * (float)dt;

        Vector3 omegaWorld = Vector3.Transform(AngularVelocity, q);
        var wq = new Quaternion(omegaWorld, 0f);
        var dq = wq * q;
        Orientation = Norm(new Quaternion(
            q.X + 0.5f * dq.X * (float)dt,
            q.Y + 0.5f * dq.Y * (float)dt,
            q.Z + 0.5f * dq.Z * (float)dt,
            q.W + 0.5f * dq.W * (float)dt));

        // failsafes: keep the sim finite; divergence is reported, not hidden
        if (Velocity.Length() > 200f) Velocity *= 200f / Velocity.Length();
        AngularVelocity = new Vector3(
            Math.Clamp(AngularVelocity.X, -20f, 20f),
            Math.Clamp(AngularVelocity.Y, -20f, 20f),
            Math.Clamp(AngularVelocity.Z, -20f, 20f));
    }

    // ---------------- guards & helpers ----------------

    private static bool AnyNaN(Vector3 v) => float.IsNaN(v.X) || float.IsNaN(v.Y) || float.IsNaN(v.Z);

    private bool HasNaN()
    {
        if (AnyNaN(Position) || AnyNaN(Velocity) || AnyNaN(AngularVelocity)) return true;
        foreach (var w in Wheels)
            if (double.IsNaN(w.SpinRate) || double.IsNaN(w.SuspensionCompression)) return true;
        if (double.IsNaN(Drivetrain.EngineOmega)) return true;
        return false;
    }

    private static Quaternion Conj(Quaternion q) => new(-q.X, -q.Y, -q.Z, q.W);

    private static Quaternion Norm(Quaternion q)
    {
        double len = Math.Sqrt(q.X * q.X + q.Y * q.Y + q.Z * q.Z + q.W * q.W);
        return new Quaternion((float)(q.X / len), (float)(q.Y / len), (float)(q.Z / len), (float)(q.W / len));
    }

    private struct Snap
    {
        public Vector3 P, V, W;
        public Quaternion Q;
        public double[] Spins;
        public double Ew, Axle;
        public int Gear, Clutch;
    }

    private Snap Capture()
    {
        var s = new Snap
        {
            P = Position, V = Velocity, W = AngularVelocity, Q = Orientation,
            Spins = new double[Wheels.Length],
            Ew = Drivetrain.EngineOmega, Axle = Drivetrain.AxleOmega,
            Gear = Drivetrain.Gear, Clutch = (int)Drivetrain.Clutch,
        };
        for (int i = 0; i < Wheels.Length; i++) s.Spins[i] = Wheels[i].SpinRate;
        return s;
    }

    private void Restore(Snap s)
    {
        Position = s.P; Velocity = s.V; AngularVelocity = s.W; Orientation = s.Q;
        for (int i = 0; i < Wheels.Length; i++) Wheels[i].SpinRate = s.Spins[i];
        Drivetrain.EngineOmega = s.Ew;
        Drivetrain.AxleOmega = s.Axle;
        Drivetrain.Gear = s.Gear;
        Drivetrain.Clutch = (ClutchState)s.Clutch;
    }
}
