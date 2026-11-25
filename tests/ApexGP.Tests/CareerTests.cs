using ApexGP.Core;

namespace ApexGP.Tests;

public static class CareerTests
{
    public static void BuyTuneAndInspectConfig()
    {
        var p = CareerProfile.New();
        p.Credits = 60000;
        var before = p.Credits;
        Career.BuyCar(p, CarSpec.ById("apexRS"));
        Checks.Check(p.Credits == before - 34000, $"credits wrong after buy: {p.Credits}");
        Checks.Check(p.ActiveCarId == "apexRS", "active car not set");

        Career.TuneCar(p, "eng2");
        Career.TuneCar(p, "weight1");
        var cfg = Career.ConfigFor(p);
        var stock = CarSpec.ById("apexRS").BuildConfig();
        Checks.Check(cfg.Drivetrain.TorqueCurve[^1].Torque > stock.Drivetrain.TorqueCurve[^1].Torque, "tune did not raise torque");
        Checks.Check(cfg.MassKg < stock.MassKg, "weight reduction did not lower mass");
        Checks.Throws<InvalidOperationException>(() => Career.TuneCar(p, "eng2")); // duplicate install
    }

    public static void BuyRejectsWhenBroke()
    {
        var p = CareerProfile.New();
        p.Credits = 100;
        Checks.Throws<InvalidOperationException>(() => Career.BuyCar(p, CarSpec.ById("circuit1600")));
    }

    public static void EventEntryRequiresLicenseAndFunds()
    {
        var p = CareerProfile.New();
        var ev = EventCalendar.ById("sunriseCup2"); // needs club licence + 500 Cr
        Checks.Check(!ev.CanEnter(p), "should not be able to enter without licence");
        p.Licenses.Add("club");
        p.Credits = 20000;
        Career.BuyCar(p, CarSpec.ById("circuit1600"));
        Checks.Check(ev.CanEnter(p), "should be able to enter with car + licence + funds");

        var track = TrackCatalog.SunriseLoop();
        var race = Career.StartEvent(p, ev, track, VehicleConfig.PrototypeGtB(), playerIsAi: true);
        Checks.Check(race.Racers.Count == ev.AiSpeedFactors.Length + 1, "wrong field size");

        // race it with the player bot
        var dt = 1.0 / 120.0;
        double t = 0;
        while (race.Phase != RacePhase.Finished && t < 420)
        {
            race.Step(dt, null);
            t += dt;
        }
        Checks.Check(race.Phase == RacePhase.Finished, "career race did not finish");
        var (prize, pos) = Career.CollectPrize(p, ev, race);
        Checks.Check(prize >= 0 && prize <= ev.Prize, $"prize out of range: {prize}");
        Checks.Check(p.CompletedEvents.Contains(ev.Id) == (pos == 1), "completed-events flag wrong");
        Console.WriteLine($"      (finished P{pos}, prize {prize:F0} Cr)");
    }

    public static void LicenseRun_PassesWithBot()
    {
        var track = TrackCatalog.SunriseLoop();
        var def = LicenseCatalog.ById("club");
        var run = new LicenseRun(track, def);
        var v = TestCars.PrototypeGtB();
        SpawnOnTrack(v, track);
        var bot = new RacingLineFollower(track) { SpeedFactor = 1.0 };
        double dt = 1.0 / 120.0, tSim = 0;
        while (!run.AllPassed && !run.TimeExpired && tSim < 150)
        {
            bot.Drive(v, dt);
            v.Step(dt);
            run.Tick(v, dt);
            tSim += dt;
        }
        Checks.Check(run.AllPassed, $"bot passed only {run.GatesPassed} gates");
        Checks.Check(run.FinishTime <= def.TimeLimit, $"licence time {run.FinishTime:F1} over limit {def.TimeLimit:F0}");
        Console.WriteLine($"      (club licence: all gates in {run.FinishTime:F1}s)");
    }

    public static void ProfileRoundTrip()
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"apexgp_test_{System.Guid.NewGuid():N}.json");
        try
        {
            var p = CareerProfile.New();
            Career.BuyCar(p, CarSpec.ById("circuit1600"));
            p.Licenses.Add("club");
            p.Save(path);
            var loaded = CareerProfile.Load(path);
            Checks.Check(loaded.Garage.Count == 1 && loaded.Garage[0].CarId == "circuit1600", "garage lost in save/load");
            Checks.Check(loaded.Licenses.Contains("club"), "licence lost in save/load");
        }
        finally
        {
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
        }
    }

    public static void Balance_StockCarCanWinCup1()
    {
        var p = CareerProfile.New();
        p.Credits = 20000;
        Career.BuyCar(p, CarSpec.ById("circuit1600")); // the starter car, stock
        var ev = EventCalendar.ById("sunriseCup1");
        var track = TrackCatalog.SunriseLoop();
        var race = Career.StartEvent(p, ev, track, Career.ConfigFor(p), playerIsAi: true);

        double dt = 1.0 / 120.0, t = 0;
        while (race.Phase != RacePhase.Finished && t < 420)
        {
            race.Step(dt, null);
            t += dt;
        }
        Checks.Check(race.Phase == RacePhase.Finished, "balance race did not finish");
        var player = race.Racers.First(r => r.IsPlayer);
        Checks.Check(player.Position == 1, $"stock starter car finished P{player.Position} vs rivals {ev.AiSpeedFactors[0]}/{ev.AiSpeedFactors[1]} — ladder too hard");
        var (prize, _) = Career.CollectPrize(p, ev, race);
        Checks.Check(prize == ev.Prize, $"P1 prize should be full {ev.Prize}, got {prize}");
        Console.WriteLine($"      (stock starter won Cup R1 in {race.Result!.Duration:F1}s, +{prize:F0} Cr)");
    }

    public static void PaintHexIsValid()
    {
        foreach (var spec in CarSpec.All)
            Checks.Check(System.Text.RegularExpressions.Regex.IsMatch(spec.PaintHex, "\\A[0-9a-fA-F]{6}\\z"),
                $"car {spec.Id} has invalid PaintHex '{spec.PaintHex}'");
    }

    public static void Ladder_ProgressionIsCompletable()
    {
        // Intended economy: start → 2× Cup1 + Cup2 + Dolphin wins → Corvid tier
        var p = CareerProfile.New();
        Career.BuyCar(p, CarSpec.ById("circuit1600"));
        Checks.Check(p.Credits == 2500, $"starter purchase should leave 2500 Cr, has {p.Credits}");

        p.Credits += 5000 + 5000;              // 2× Cup1 wins
        p.Licenses.Add("club");
        p.Credits += 8000 - 500;               // Cup2 win (entry fee)
        p.Credits += 11000 - 800;              // Dolphin Sprint win (entry fee)
        Checks.Check(p.Credits == 30200, $"four wins should bank 30,200 Cr, has {p.Credits}");

        Career.BuyCar(p, CarSpec.ById("corvid"));
        Checks.Check(p.Credits == 2200, $"credits after Corvid buy wrong: {p.Credits}");

        p.Credits += 11000;                    // defend the sprint title
        Career.TuneCar(p, "eng2", "corvid");
        Checks.Check(p.Credits == 13200 - 5500, $"credits after eng2 tune wrong: {p.Credits}");
    }

    public static void Balance_TunedCorvidCanWinKanyuChallenge()
    {
        var p = CareerProfile.New();
        p.Credits = 60000;
        p.Licenses.Add("national");
        Career.BuyCar(p, CarSpec.ById("corvid"));
        Career.TuneCar(p, "eng2");
        Career.TuneCar(p, "weight1");
        Career.TuneCar(p, "tires");
        var ev = EventCalendar.ById("kanyuChallenge");
        var track = TrackCatalog.KanyuPass();
        var race = Career.StartEvent(p, ev, track, Career.ConfigFor(p), playerIsAi: true);

        double dt = 1.0 / 120.0, t = 0;
        while (race.Phase != RacePhase.Finished && t < 600)
        {
            race.Step(dt, null);
            t += dt;
        }
        Checks.Check(race.Phase == RacePhase.Finished, "Kanyu Challenge did not finish");
        var player = race.Racers.First(r => r.IsPlayer);
        Checks.Check(player.Position <= 2, $"tuned Corvid finished P{player.Position} at Kanyu Challenge — third tier unreachable");
        Console.WriteLine($"      (tuned Corvid finished P{player.Position} in Kanyu Challenge, {race.Result!.Duration:F1}s)");
    }

    public static void GuardRailsForEmptyGarage()
    {
        // Fresh profile (no car bought yet): licence school and the menus must not crash.
        var p = CareerProfile.New();
        foreach (var ev in EventCalendar.All)
            Checks.Check(!ev.CanEnter(p), $"event {ev.Id} should be locked with an empty garage");
        Checks.Throws<InvalidOperationException>(() => Career.ConfigFor(p));

        // Buying the first car sets the active car and unlocks entry requirements again.
        p.Credits = 50000;
        Career.BuyCar(p, CarSpec.ById("circuit1600"));
        Checks.Check(Career.ConfigFor(p).MassKg > 0, "config after first buy is broken");
        Checks.Check(EventCalendar.ById("sunriseCup1").CanEnter(p), "Cup 1 should be enterable with a car + credits");
    }

    private static void SpawnOnTrack(Vehicle v, TrackSurface track)
    {
        var s = track.Samples[0];
        v.Position = new System.Numerics.Vector3((float)(s.X + s.RightX * 3), 1.0f, (float)(s.Z + s.RightZ * 3));
        double yaw = System.Math.Atan2(-s.DirX, -s.DirZ);
        v.Orientation = System.Numerics.Quaternion.CreateFromAxisAngle(System.Numerics.Vector3.UnitY, (float)yaw);
        v.Surface = track;
    }
}
