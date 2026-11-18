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
        Checks.Check(ev.CanEnter(p), "should be able to enter with licence + funds");

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

    private static void SpawnOnTrack(Vehicle v, TrackSurface track)
    {
        var s = track.Samples[0];
        v.Position = new System.Numerics.Vector3((float)(s.X + s.RightX * 3), 1.0f, (float)(s.Z + s.RightZ * 3));
        double yaw = System.Math.Atan2(-s.DirX, -s.DirZ);
        v.Orientation = System.Numerics.Quaternion.CreateFromAxisAngle(System.Numerics.Vector3.UnitY, (float)yaw);
        v.Surface = track;
    }
}
