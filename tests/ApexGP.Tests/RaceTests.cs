using ApexGP.Core;

namespace ApexGP.Tests;

public static class RaceTests
{
    public static void ThreeCarRace_CompletesWithSaneOrdering()
    {
        var track = TrackCatalog.SunriseLoop();
        var race = new RaceController(track, laps: 2);

        var playerBot = new RacingLineFollower(track) { SpeedFactor = 1.0 };
        var ai1 = new RacingLineFollower(track) { SpeedFactor = 0.94 };
        var ai2 = new RacingLineFollower(track) { SpeedFactor = 0.88 };

        race.AddRacer("PLAYER", VehicleConfig.PrototypeGtB(), isPlayer: true, ai: playerBot, lateralOffset: 3.0);
        race.AddRacer("AI-FAST", VehicleConfig.PrototypeGtB(), isPlayer: false, ai: ai1, lateralOffset: -3.0);
        race.AddRacer("AI-SLOW", VehicleConfig.PrototypeGtB(), isPlayer: false, ai: ai2, lateralOffset: 3.0);

        double dt = 1.0 / 120.0;
        double tSim = 0;
        int[] lastLaps = new int[3];
        while (race.Phase != RacePhase.Finished && tSim < 420.0)
        {
            race.Step(dt, null);
            tSim += dt;
            for (int i = 0; i < race.Racers.Count; i++)
                lastLaps[i] = System.Math.Max(lastLaps[i], race.Racers[i].Laps);
        }

        Checks.Check(race.Phase == RacePhase.Finished, $"race did not finish (phase={race.Phase}, sim time {tSim:F0}s)");
        Checks.Check(race.Result != null, "no result generated");
        Checks.Check(race.Result!.FinishOrder.Count == 3, "results must include all 3 racers");
        foreach (var r in race.Racers)
            Checks.Check(r.Laps >= 2, $"{r.Name} completed only {r.Laps} laps");

        var player = race.Racers[0];
        Checks.Check(player.Position >= 1 && player.Position <= 3, "player position out of range");

        // AI cars must stay on track (allow grass during battles, but not constantly)
        // (off-track stats were verified for a single car in TrackTests; here we check laps exist)
        Console.WriteLine($"      (race finished in {race.Result!.Duration:F1}s; P1={race.Result.FinishOrder[0].Name})");
    }

    public static void CarCarCollision_SeparatesWithoutDivergence()
    {
        var track = TrackCatalog.SunriseLoop();
        var race = new RaceController(track, laps: 1);
        var bot1 = new RacingLineFollower(track) { SpeedFactor = 1.0 };
        var bot2 = new RacingLineFollower(track) { SpeedFactor = 1.02 };

        race.AddRacer("A", VehicleConfig.PrototypeGtB(), isPlayer: false, ai: bot1, lateralOffset: 0.0);
        race.AddRacer("B", VehicleConfig.PrototypeGtB(), isPlayer: false, ai: bot2, lateralOffset: 0.0);

        // place B overlapping right behind A on the grid to force early contact
        var carA = race.Racers[0].Car;
        var carB = race.Racers[1].Car;
        carB.Position = new System.Numerics.Vector3(carA.Position.X, carA.Position.Y, carA.Position.Z - 1.6f);
        carB.Surface = track;

        double dt = 1.0 / 120.0;
        double tSim = 0;
        bool everTooClose = false;
        while (race.Phase != RacePhase.Finished && tSim < 200.0)
        {
            race.Step(dt, null);
            tSim += dt;
            double dx = carB.Position.X - carA.Position.X;
            double dz = carB.Position.Z - carA.Position.Z;
            if (dx * dx + dz * dz < 1.4 * 1.4) everTooClose = true;
            if (carA.Diverged || carB.Diverged) break;
        }

        Checks.Check(!carA.Diverged && !carB.Diverged, "collision caused divergence");
        Checks.Check(race.Phase == RacePhase.Finished, $"race with contact did not finish ({tSim:F0}s)");
        Checks.Check(!everTooClose, "cars interpenetrated below the collision radius");
        Console.WriteLine($"      (contact race finished in {race.Result!.Duration:F1}s)");
    }

    public static void WallClamp_PreventsInfieldCuts()
    {
        var track = TrackCatalog.SunriseLoop();
        var race = new RaceController(track, laps: 1);
        // aim the car at ~60° off the racing line so it runs at the wall
        var car = race.AddRacer("CUTTER", VehicleConfig.PrototypeGtB(), isPlayer: false, ai: null, lateralOffset: 0.0).Car;
        car.Throttle = 1.0;
        car.SteerInput = 0.9; // hard right, off track

        double dt = 1.0 / 120.0;
        double wallLat = track.HalfWidth * 1.06;
        double maxLat = 0;
        for (int i = 0; i < 120 * 6; i++)
        {
            race.Step(dt, null);
            int idx = track.NearestIndex(car.Position.X, car.Position.Z, race.Racers[0].SurfaceHint);
            var s = track.Samples[idx];
            double lat = System.Math.Abs((car.Position.X - s.X) * s.RightX + (car.Position.Z - s.Z) * s.RightZ);
            maxLat = System.Math.Max(maxLat, lat);
        }

        Checks.Check(!car.Diverged, "wall clamp caused divergence");
        Checks.Check(maxLat <= wallLat + 0.6, $"car exceeded wall by {maxLat - wallLat:F1} m (max lat {maxLat:F1} m)");
        Console.WriteLine($"      (max lateral offset {maxLat:F1} m, wall at {wallLat:F1} m)");
    }

    public static void ReplayRecorder_CapturesAndRoundTrips()
    {
        var track = TrackCatalog.SunriseLoop();
        var race = new RaceController(track, laps: 1);
        race.AddRacer("A", VehicleConfig.PrototypeGtB(), isPlayer: false, ai: new RacingLineFollower(track) { SpeedFactor = 1.0 }, lateralOffset: 0);
        race.AddRacer("B", VehicleConfig.PrototypeGtB(), isPlayer: false, ai: new RacingLineFollower(track) { SpeedFactor = 0.9 }, lateralOffset: 0);

        var rec = new ReplayRecorder();
        double dt = 1.0 / 120.0, t = 0;
        int step = 0;
        while (race.Phase != RacePhase.Finished && t < 200)
        {
            race.Step(dt, null);
            t += dt;
            if (++step % 6 == 0) rec.Capture(race, t); // ~20 Hz capture grid
        }

        Checks.Check(rec.FrameCount > 100, $"too few replay frames: {rec.FrameCount}");
        Checks.Check(rec.RacerCount == 2, "wrong racer count in replay");
        var last = rec.Sample(rec.Duration - 0.05)!;
        Checks.Check(last.Length == 2, "replay sample must return one frame per racer");

        // car A should be near its final recorded position
        var carA = race.Racers[0].Car;
        double dist = System.Math.Sqrt(
            (carA.Position.X - last[0].X) * (carA.Position.X - last[0].X) +
            (carA.Position.Z - last[0].Z) * (carA.Position.Z - last[0].Z));
        Checks.Check(dist < 3.0, $"replay end frame too far from live car: {dist:F1} m");

        // JSON round-trip
        var clone = ReplayRecorder.FromJson(rec.ToJson());
        Checks.Check(clone.FrameCount == rec.FrameCount && clone.Duration.ToString("0.0") == rec.Duration.ToString("0.0"), "round-trip lost frames/duration");
        var cloneSample = clone.Sample(rec.Duration - 0.05)!;
        Checks.Check(System.Math.Abs(cloneSample[1].X - last[1].X) < 1e-4, "round-trip position mismatch");
        Checks.Check(System.Math.Abs(cloneSample[1].Qw - last[1].Qw) < 1e-4, "round-trip orientation mismatch");

        // sample before the first capture returns the first frame (clamp)
        var early = rec.Sample(-5.0);
        Checks.Check(early != null, "sample before start should clamp to first frame");
        Console.WriteLine($"      (replay: {rec.FrameCount} frames, {rec.Duration:F1}s, round-trip ok)");
    }
}
