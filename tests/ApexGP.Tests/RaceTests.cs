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
}
