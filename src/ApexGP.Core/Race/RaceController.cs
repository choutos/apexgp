namespace ApexGP.Core;

public enum RacePhase { Formation, Racing, Finished }

public sealed class Racer
{
    public required Vehicle Car;
    public required LapTimer Timer;
    public RacingLineFollower? Ai;      // null → player-controlled
    public required string Name;
    public bool IsPlayer;
    public double FinishTime = double.MaxValue;
    public int Position = 1;
    public int SurfaceHint = -1;          // cached nearest-sample hint for wall queries
    public double ContactCooldown;        // s — rumble hook, not sim-critical

    public int Laps => Timer.LapsCompleted;
}

public sealed class RaceResult
{
    public required List<(string Name, double Time)> FinishOrder;
    public double Duration;
}

/// <summary>
/// Runs a race: grid formation, N laps, live positions, results.
/// Steps every car (AI and player) on the same fixed-substep grid so all
/// racers share identical physics timing.
/// </summary>
public sealed class RaceController
{
    private readonly TrackSurface _track;
    public int Laps { get; }
    public RacePhase Phase { get; private set; } = RacePhase.Formation;
    public double Time { get; private set; }
    public List<Racer> Racers { get; } = new();
    public RaceResult? Result { get; private set; }

    private double _phaseTimer;
    private const double FormationTime = 1.0;
    private const double GridSpacing = 14.0;

    public RaceController(TrackSurface track, int laps)
    {
        _track = track;
        Laps = laps;
    }

    public Racer AddRacer(string name, VehicleConfig config, bool isPlayer, RacingLineFollower? ai, double lateralOffset)
    {
        int slot = Racers.Count;
        var car = new Vehicle(config);
        PlaceOnGrid(car, slot, lateralOffset);
        var racer = new Racer
        {
            Car = car,
            Timer = new LapTimer(_track),
            Ai = ai,
            Name = name,
            IsPlayer = isPlayer,
        };
        Racers.Add(racer);
        return racer;
    }

    private void PlaceOnGrid(Vehicle car, int slot, double lateralOffset)
    {
        int n = _track.Samples.Length;
        double backDist = slot * GridSpacing;
        int idx = (n - (int)(backDist / 4.0) % n + n) % n;
        var s = _track.Samples[idx];
        double lat = lateralOffset;
        car.Position = new System.Numerics.Vector3((float)(s.X + s.RightX * lat), 1.0f, (float)(s.Z + s.RightZ * lat));
        double yaw = System.Math.Atan2(-s.DirX, -s.DirZ);
        car.Orientation = System.Numerics.Quaternion.CreateFromAxisAngle(System.Numerics.Vector3.UnitY, (float)yaw);
        car.Surface = _track;
    }

    /// <summary>One physics frame: advances the race and every car.</summary>
    public void Step(double delta, (double throttle, double brake, double steer)? playerInput = null)
    {
        const double StepDt = 1.0 / 120.0;
        _accum += System.Math.Min(delta, 0.1);
        while (_accum >= StepDt)
        {
            SubStep(StepDt, playerInput);
            _accum -= StepDt;
        }
        UpdatePositions();
    }

    private double _accum;

    private void SubStep(double dt, (double throttle, double brake, double steer)? playerInput)
    {
        Time += dt;
        switch (Phase)
        {
            case RacePhase.Formation:
                _phaseTimer += dt;
                foreach (var r in Racers) HoldOnGrid(r);
                if (_phaseTimer >= FormationTime) { Phase = RacePhase.Racing; _phaseTimer = 0; }
                break;

            case RacePhase.Racing:
                foreach (var r in Racers)
                {
                    if (r.Car.Diverged) continue;
                    if (r.Ai != null)
                    {
                        r.Ai.Drive(r.Car, dt);
                    }
                    else if (playerInput.HasValue)
                    {
                        r.Car.Throttle = playerInput.Value.throttle;
                        r.Car.Brake = playerInput.Value.brake;
                        r.Car.SteerInput = playerInput.Value.steer;
                    }
                    r.Car.Step(dt);
                    r.Timer.Tick(r.Car, dt);
                    if (r.Laps >= Laps && r.FinishTime > Time)
                        r.FinishTime = Time;
                }
                ResolveCarCarCollisions();
                ResolveWallContacts();
                if (Racers.All(r => r.FinishTime <= Time))
                    Phase = RacePhase.Finished;
                break;

            case RacePhase.Finished:
                foreach (var r in Racers) HoldOnGrid(r);
                break;
        }

        if (Phase == RacePhase.Finished && Result == null)
        {
            var order = Racers.OrderBy(r => r.FinishTime).ThenBy(r => r.Position).ToList();
            Result = new RaceResult
            {
                FinishOrder = order.Select((r, i) => (r.Name, r.FinishTime)).ToList(),
                Duration = Time,
            };
        }
    }

    private void HoldOnGrid(Racer r)
    {
        r.Car.Throttle = 0;
        r.Car.Brake = 0.3;
    }

    // ---- contact resolution (v0.3) ----
    // Sphere-per-car approximation: two spheres per car would be more physical,
    // but one 2.3 m sphere at the CoM gives stable, tunable, non-explosive
    // bumper-car behaviour that is *good enough* to make racing close racing.
    // Walls: lateral clamp at halfWidth * WallFactor with soft bounce — cuts
    // are now impossible, matching GT1's invisible-wall behaviour.
    private const double CarRadius = 1.15;
    private const double WallFactor = 1.32;      // forgiving runoff (GT1-style)
    private const double Restitution = 0.3;
    private const double WallRestitution = 0.45;
    private const double WallMinSlide = 2.5;     // m/s tangential floor so wedged cars slide out

    private void ResolveCarCarCollisions()
    {
        for (int a = 0; a < Racers.Count; a++)
        for (int b = a + 1; b < Racers.Count; b++)
        {
            var ca = Racers[a].Car;
            var cb = Racers[b].Car;
            double dx = cb.Position.X - ca.Position.X;
            double dz = cb.Position.Z - ca.Position.Z;
            double d2 = dx * dx + dz * dz;
            double minDist = CarRadius * 2;
            if (d2 >= minDist * minDist || d2 < 1e-9) continue;

            double d = System.Math.Sqrt(d2);
            double nx = dx / d, nz = dz / d;
            double overlap = minDist - d;

            // positional separation, half each (equal mass)
            ca.Position = new System.Numerics.Vector3(
                ca.Position.X - (float)(nx * overlap * 0.5), ca.Position.Y,
                ca.Position.Z - (float)(nz * overlap * 0.5));
            cb.Position = new System.Numerics.Vector3(
                cb.Position.X + (float)(nx * overlap * 0.5), cb.Position.Y,
                cb.Position.Z + (float)(nz * overlap * 0.5));

            // impulse if closing
            double rvx = cb.Velocity.X - ca.Velocity.X;
            double rvz = cb.Velocity.Z - ca.Velocity.Z;
            double vRel = rvx * nx + rvz * nz;
            if (vRel >= 0) continue;

            double dv = vRel * (1 + Restitution) / 2;
            var n = new System.Numerics.Vector3((float)nx, 0f, (float)nz);
            ca.Velocity += n * (float)dv;
            cb.Velocity -= n * (float)dv;
            Racers[a].ContactCooldown = 0.4;
            Racers[b].ContactCooldown = 0.4;
        }
    }

    private void ResolveWallContacts()
    {
        double wallLat = _track.HalfWidth * WallFactor;
        foreach (var r in Racers)
        {
            var car = r.Car;
            r.SurfaceHint = _track.NearestIndex(car.Position.X, car.Position.Z, r.SurfaceHint);
            var s = _track.Samples[r.SurfaceHint];
            double lat = (car.Position.X - s.X) * s.RightX + (car.Position.Z - s.Z) * s.RightZ;
            if (System.Math.Abs(lat) <= wallLat) continue;

            int sign = System.Math.Sign(lat);
            car.Position = new System.Numerics.Vector3(
                (float)(s.X + s.RightX * wallLat * sign), car.Position.Y,
                (float)(s.Z + s.RightZ * wallLat * sign));

            double vLat = car.Velocity.X * s.RightX + car.Velocity.Z * s.RightZ;
            if (System.Math.Sign(vLat) == sign)
            {
                var right = new System.Numerics.Vector3((float)s.RightX, 0f, (float)s.RightZ);
                car.Velocity -= right * (float)(vLat * (1 + WallRestitution));
                r.ContactCooldown = 0.4;
            }

            // slide-along: a car nose-in against the wall keeps a tangential crawl so
            // it can't pin itself there forever; the AI's steering then walks it out.
            double vTan = car.Velocity.X * s.DirX + car.Velocity.Z * s.DirZ;
            if (System.Math.Abs(vTan) < WallMinSlide)
            {
                var fwd = System.Numerics.Vector3.Transform(System.Numerics.Vector3.UnitZ, car.Orientation);
                double fwdTan = -(fwd.X * s.DirX + fwd.Z * s.DirZ);
                double dir = System.Math.Abs(vTan) > 0.5 ? System.Math.Sign(vTan) : (fwdTan >= 0 ? 1 : -1);
                double latAfterBounce = -(vLat * (System.Math.Sign(vLat) == sign ? 1 : 0)) * (1 + WallRestitution);
                car.Velocity = new System.Numerics.Vector3(
                    (float)(s.DirX * WallMinSlide * dir + s.RightX * latAfterBounce),
                    car.Velocity.Y,
                    (float)(s.DirZ * WallMinSlide * dir + s.RightZ * latAfterBounce));
            }
        }
    }

    private void UpdatePositions()
    {
        if (Phase != RacePhase.Racing && Phase != RacePhase.Finished) return;
        // finished cars rank strictly by finish time; unfinished cars fall back
        // to laps/progress (a car just before the line must not outrank one
        // that has already crossed — that cost the wrong podium in testing)
        var ordered = Racers
            .OrderBy(r => r.FinishTime)
            .ThenByDescending(r => r.Laps)
            .ThenByDescending(r => ProgressS(r))
            .ToList();
        for (int i = 0; i < ordered.Count; i++) ordered[i].Position = i + 1;
    }

    private double ProgressS(Racer r)
    {
        int idx = r.Timer.CurrentIndex;
        return idx >= 0 ? _track.Samples[idx].S : 0;
    }
}
