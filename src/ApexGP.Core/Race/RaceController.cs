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

    private void UpdatePositions()
    {
        if (Phase != RacePhase.Racing && Phase != RacePhase.Finished) return;
        var ordered = Racers
            .OrderByDescending(r => r.Laps)
            .ThenByDescending(r => ProgressS(r))
            .ThenBy(r => r.FinishTime)
            .ToList();
        for (int i = 0; i < ordered.Count; i++) ordered[i].Position = i + 1;
    }

    private double ProgressS(Racer r)
    {
        int idx = r.Timer.CurrentIndex;
        return idx >= 0 ? _track.Samples[idx].S : 0;
    }
}
