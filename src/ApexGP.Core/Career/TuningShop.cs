namespace ApexGP.Core;

/// <summary>A tuning part: cost + effect applied to a car config.</summary>
public sealed class TunePart
{
    public required string Id;
    public required string Name;
    public required double Price;
    public required Action<VehicleConfig> Apply;

    public static readonly List<TunePart> All = new()
    {
        new() { Id = "eng1", Name = "Stage 1 Engine Tune", Price = 2500, Apply = c => ScaleTorque(c, 1.06) },
        new() { Id = "eng2", Name = "Stage 2 Engine Tune", Price = 5500, Apply = c => ScaleTorque(c, 1.14) },
        new() { Id = "eng3", Name = "Racing Exhaust + Cams", Price = 9500, Apply = c => ScaleTorque(c, 1.22) },
        new() { Id = "weight1", Name = "Weight Reduction I", Price = 3000, Apply = c => c.MassKg = System.Math.Max(900, c.MassKg - 60) },
        new() { Id = "weight2", Name = "Weight Reduction II", Price = 6500, Apply = c => c.MassKg = System.Math.Max(900, c.MassKg - 100) },
        new() { Id = "tires", Name = "Sport Tires", Price = 4000, Apply = c => c.TireMuBase = System.Math.Min(1.2, c.TireMuBase + 0.05) },
        new() { Id = "gearing", Name = "Close-Ratio Gearbox", Price = 5000, Apply = c => c.Drivetrain.FinalDrive = System.Math.Min(5.4, c.Drivetrain.FinalDrive + 0.25) },
    };

    public static TunePart ById(string id) => All.FirstOrDefault(p => p.Id == id)
        ?? throw new KeyNotFoundException($"unknown part id {id}");

    private static void ScaleTorque(VehicleConfig c, double f) =>
        c.Drivetrain.TorqueCurve = c.Drivetrain.TorqueCurve.Select(p => (p.Rpm, p.Torque * f)).ToArray();
}

/// <summary>Applies owned tuning parts to a fresh config (used when spawning a garage car).</summary>
public static class Tuning
{
    public static VehicleConfig Apply(VehicleConfig baseConfig, IEnumerable<string> partIds)
    {
        var cfg = baseConfig;
        foreach (var id in partIds)
            TunePart.ById(id).Apply(cfg);
        return cfg;
    }
}
