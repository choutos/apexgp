namespace ApexGP.Core;

/// <summary>A buyable car: original fictional model with its own configuration.</summary>
public sealed class CarSpec
{
    public required string Id;
    public required string Name;
    public required double Price;
    public required Func<VehicleConfig> BuildConfig;

    public static readonly List<CarSpec> All = new()
    {
        new()
        {
            Id = "circuit1600", Name = "Kappa Circuit 1600", Price = 12500,
            BuildConfig = () => VehicleConfig.PrototypeGtB(),
        },
        new()
        {
            Id = "touringS", Name = "Kappa Touring S", Price = 21000,
            BuildConfig = () => With(VehicleConfig.PrototypeGtB(), torque: 1.10, mass: 1280),
        },
        new()
        {
            Id = "corvid", Name = "Corvid Cup Edition", Price = 28000,
            BuildConfig = () => With(VehicleConfig.PrototypeGtB(), torque: 1.35, mass: 1150, finalDrive: 5.0),
        },
        new()
        {
            Id = "apexRS", Name = "Apex RS Coupe", Price = 34000,
            BuildConfig = () => With(VehicleConfig.PrototypeGtB(), torque: 1.22, mass: 1190, mu: 1.10),
        },
        new()
        {
            Id = "tsukasa", Name = "Tsukasa Type-R", Price = 46000,
            BuildConfig = () => With(VehicleConfig.PrototypeGtB(), torque: 1.55, mass: 1080, mu: 1.14),
        },
        new()
        {
            Id = "onibi", Name = "Onibi GT Twin-Turbo", Price = 68000,
            BuildConfig = () => With(VehicleConfig.PrototypeGtB(), torque: 2.0, mass: 1400),
        },
        new()
        {
            Id = "gtLegend", Name = "Vega GT Legend", Price = 52000,
            BuildConfig = () => With(VehicleConfig.PrototypeGtB(), torque: 1.38, mass: 1150, mu: 1.12, finalDrive: 4.4),
        },
    };

    public static CarSpec ById(string id) => All.FirstOrDefault(c => c.Id == id)
        ?? throw new KeyNotFoundException($"unknown car id {id}");

    private static VehicleConfig With(VehicleConfig baseConfig, double torque = 1.0, double mass = 0, double mu = 0, double finalDrive = 0)
    {
        var cfg = baseConfig;
        if (torque != 1.0)
            cfg.Drivetrain.TorqueCurve = cfg.Drivetrain.TorqueCurve.Select(p => (p.Rpm, p.Torque * torque)).ToArray();
        if (mass > 0) cfg.MassKg = mass;
        if (mu > 0) cfg.TireMuBase = mu;
        if (finalDrive > 0) cfg.Drivetrain.FinalDrive = finalDrive;
        cfg.Name = cfg.Name;
        return cfg;
    }
}
