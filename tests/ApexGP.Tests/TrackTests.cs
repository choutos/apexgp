using ApexGP.Core;

namespace ApexGP.Tests;

public static class TrackTests
{
    public static void Build_ClosesLoopAndMeasuresLength()
    {
        var t = TrackCatalog.SunriseLoop();
        Checks.Check(t.TotalLength > 400 && t.TotalLength < 2000, $"unreasonable track length {t.TotalLength:F0} m");
        int n = t.Samples.Length;
        Console.WriteLine($"      (len={t.TotalLength:F1} m, samples={n}, spacing={t.TotalLength / n:F2} m)");
        Console.WriteLine($"      last=({t.Samples[n - 1].X:F1},{t.Samples[n - 1].Z:F1}) first=({t.Samples[0].X:F1},{t.Samples[0].Z:F1})");
        double gap = Dist(t.Samples[n - 1], t.Samples[0]);
        Checks.Check(gap < 7.0, $"loop does not close cleanly: {gap:F1} m gap");
        double spacing = t.TotalLength / n;
        Checks.Check(System.Math.Abs(spacing - 4.0) < 1.0, $"non-uniform sampling: {spacing:F2} m");
        // curvature exists somewhere (it's a circuit, not a drag strip)
        double maxK = t.Samples.Max(s => System.Math.Abs(s.Curvature));
        Checks.Check(maxK > 0.005, $"no curvature found (max κ={maxK:F4})");
    }

    public static void Build_DolphinRingIsValid()
    {
        var t = TrackCatalog.DolphinRing();
        Checks.Check(t.TotalLength > 500 && t.TotalLength < 1500, $"unreasonable Dolphin Ring length {t.TotalLength:F0} m");
        int n = t.Samples.Length;
        double gap = Dist(t.Samples[n - 1], t.Samples[0]);
        Checks.Check(gap < 7.0, $"Dolphin Ring does not close: {gap:F1} m gap");
        Checks.Check(t.SpeedTargets.Min() > 8.0, $"profile has a near-zero-speed point: {t.SpeedTargets.Min() * 3.6:F0} km/h");
    }

    public static void Build_KanyuPassIsValid()
    {
        var t = TrackCatalog.KanyuPass();
        Checks.Check(t.TotalLength > 700 && t.TotalLength < 1800, $"unreasonable Kanyu Pass length {t.TotalLength:F0} m");
        int n = t.Samples.Length;
        double gap = Dist(t.Samples[n - 1], t.Samples[0]);
        Checks.Check(gap < 7.0, $"Kanyu Pass does not close: {gap:F1} m gap");
        Checks.Check(t.SpeedTargets.Min() > 6.0, $"profile has a near-zero-speed point: {t.SpeedTargets.Min() * 3.6:F0} km/h");
        double maxK = t.Samples.Max(s => System.Math.Abs(s.Curvature));
        Checks.Check(maxK > 0.02, $"Kanyu Pass should have at least one tight hairpin (max κ={maxK:F3})");
    }

    public static void GripModel_OffTrackIsSlippery()
    {
        var t = TrackCatalog.SunriseLoop();
        int n = t.Samples.Length;
        // find a sample that is straight for several metres either side
        int best = -1;
        for (int i = 0; i < n && best < 0; i++)
        {
            bool straight = System.Math.Abs(t.Samples[i].Curvature) < 1e-6
                && System.Math.Abs(t.Samples[(i + 1) % n].Curvature) < 1e-6
                && System.Math.Abs(t.Samples[(i - 1 + n) % n].Curvature) < 1e-6;
            if (straight) best = i;
        }
        if (best < 0)
        {
            double bk = t.Samples.Min(s => System.Math.Abs(s.Curvature));
            best = System.Array.FindIndex(t.Samples, s => System.Math.Abs(s.Curvature) == bk);
        }
        var s = t.Samples[best];
        double hw = t.HalfWidth;
        // grip test on a guaranteed-straight sample (hint = that sample, so the
        // parallel return leg can't alias the lookup)
        Checks.Check(t.GripMultiplierAt(best, s.X, s.Z) == 1.0, "centreline should be full grip");
        Checks.Check(t.GripMultiplierAt(best, s.X + s.RightX * hw * 0.5, s.Z + s.RightZ * hw * 0.5) == 1.0, "inside track should be full grip");
        double edge = t.GripMultiplierAt(best, s.X + s.RightX * hw * 1.02, s.Z + s.RightZ * hw * 1.02);
        Checks.Check(edge > 0.7 && edge < 0.95, $"kerb grip should be ~0.85, got {edge:F2}");
        double grass = t.GripMultiplierAt(best, s.X + s.RightX * hw * 2.5, s.Z + s.RightZ * hw * 2.5);
        Checks.Check(grass < 0.6, $"grass grip should be ~0.45, got {grass:F2}");
    }

    public static void Bot_CompletesThreeLapsOnTrack()
    {
        var t = TrackCatalog.SunriseLoop();
        var start = t.Samples[0];
        var v = TestCars.PrototypeGtB();
        // spawn on the start line, one car's width right of centre, facing along the track
        v.Position = new System.Numerics.Vector3((float)(start.X + start.RightX * 3.0), 1.0f, (float)(start.Z + start.RightZ * 3.0));
        double yaw = System.Math.Atan2(-start.DirX, -start.DirZ);
        v.Orientation = System.Numerics.Quaternion.CreateFromAxisAngle(System.Numerics.Vector3.UnitY, (float)yaw);
        v.Surface = t;

        var timer = new LapTimer(t);
        var bot = new RacingLineFollower(t);
        double dt = 1.0 / 120.0;
        double tSim = 0;
        int offTrack = 0, steps = 0;
        while (timer.LapsCompleted < 3 && tSim < 260.0)
        {
            bot.Drive(v, dt);
            v.Step(dt);
            timer.Tick(v, dt);
            tSim += dt;
            steps++;
            var sHere = t.Samples[t.NearestIndex(v.Position.X, v.Position.Z, -1)];
            if (System.Math.Abs((v.Position.X - sHere.X) * sHere.RightX + (v.Position.Z - sHere.Z) * sHere.RightZ) > t.HalfWidth * 1.2)
                offTrack++;
            if (System.Math.Abs(tSim - System.Math.Round(tSim / 15.0) * 15.0) < dt / 2)
                Console.WriteLine($"      t={tSim:F0}s: {v.SpeedKmh:F0} km/h (target {t.SpeedTargets[t.NearestIndex(v.Position.X, v.Position.Z, -1)] * 3.6 * bot.SpeedFactor:F0}), s={sHere.S:F0} m, off={offTrack * 100.0 / steps:F1}%");
        }

        double offFrac = (double)offTrack / steps;
        Console.WriteLine($"      profile: min={t.SpeedTargets.Min() * 3.6:F0} avg={t.SpeedTargets.Average() * 3.6:F0} max={t.SpeedTargets.Max() * 3.6:F0} km/h");
        Checks.Check(timer.LapsCompleted >= 3, $"bot completed only {timer.LapsCompleted} laps in 260 s");
        Checks.Check(timer.BestLap > 10.0 && timer.BestLap < 90.0, $"implausible best lap {timer.BestLap:F1} s");
        Checks.Check(offFrac < 0.05, $"bot off-track {offFrac:P1} of the time");
        Console.WriteLine($"      (3 laps done, best {timer.BestLap:F2} s, off-track {offFrac:P1})");
    }

    private static double Dist(TrackSample a, TrackSample b)
        => System.Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z));
}
