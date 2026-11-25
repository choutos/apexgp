namespace ApexGP.Core;

/// <summary>A buyable car: original fictional model with its own configuration.</summary>
public sealed class CarSpec
{
    public required string Id;
    public required string Name;
    public required double Price;
    public required Func<VehicleConfig> BuildConfig;
    /// <summary>Showroom paint in RRGGBB hex (original fictional livery).</summary>
    public string PaintHex = "B8302A";

    public static readonly List<CarSpec> All = new()
    {
        new()
        {
            Id = "circuit1600", Name = "Kappa Circuit 1600", Price = 12500,
            BuildConfig = () => With(VehicleConfig.PrototypeGtB(), c =>
            {
                c.Body.BodyLength = 3.9; c.Body.BodyWidth = 1.62; c.Body.BodyHeight = 0.46;
                c.Body.NoseLength = 0.85; c.Body.NoseDrop = 0.26;
                c.Body.CabinLength = 1.7; c.Body.CabinHeight = 0.44; c.Body.CabinShift = 0.35;
                c.Body.SpoilerSpan = 1.35; c.Body.SpoilerChord = 0.30; c.Body.SpoilerHeight = 0.18;
                c.Body.StripeHex = "F5F1E6"; c.Body.StripeStyle = 1; c.Body.CarNumber = 16;
            }),
            PaintHex = "C24A2A",
        },
        new()
        {
            Id = "touringS", Name = "Kappa Touring S", Price = 21000,
            BuildConfig = () => With(VehicleConfig.PrototypeGtB(), c =>
            {
                c.Body.BodyLength = 4.5; c.Body.BodyWidth = 1.74; c.Body.BodyHeight = 0.46;
                c.Body.NoseLength = 1.1; c.Body.NoseDrop = 0.18;
                c.Body.CabinLength = 2.1; c.Body.CabinHeight = 0.46; c.Body.CabinShift = 0.15;
                c.Body.SpoilerSpan = 1.5; c.Body.SpoilerChord = 0.34; c.Body.SpoilerHeight = 0.16;
                c.Body.StripeHex = "DDE3EC"; c.Body.StripeStyle = 2; c.Body.CarNumber = 21;
            }, torque: 1.10, mass: 1280),
            PaintHex = "2E5E9E",
        },
        new()
        {
            Id = "corvid", Name = "Corvid Cup Edition", Price = 28000,
            BuildConfig = () => With(VehicleConfig.PrototypeGtB(), c =>
            {
                c.Body.BodyLength = 4.3; c.Body.BodyWidth = 1.78; c.Body.BodyHeight = 0.44;
                c.Body.NoseLength = 1.0; c.Body.NoseDrop = 0.22;
                c.Body.CabinLength = 1.8; c.Body.CabinHeight = 0.42; c.Body.CabinShift = 0.25;
                c.Body.SpoilerSpan = 1.7; c.Body.SpoilerChord = 0.46; c.Body.SpoilerHeight = 0.30;
                c.Body.StripeHex = "F2D34E"; c.Body.StripeStyle = 2; c.Body.CarNumber = 8;
            }, torque: 1.35, mass: 1150, finalDrive: 5.0),
            PaintHex = "1F5C38",
        },
        new()
        {
            Id = "apexRS", Name = "Apex RS Coupe", Price = 34000,
            BuildConfig = () => With(VehicleConfig.PrototypeGtB(), c =>
            {
                c.Body.BodyLength = 4.35; c.Body.BodyWidth = 1.7; c.Body.BodyHeight = 0.38;
                c.Body.NoseLength = 1.2; c.Body.NoseDrop = 0.16;
                c.Body.CabinLength = 1.75; c.Body.CabinHeight = 0.34; c.Body.CabinShift = 0.4;
                c.Body.SpoilerSpan = 1.55; c.Body.SpoilerChord = 0.36; c.Body.SpoilerHeight = 0.14;
                c.Body.StripeHex = "1A1C22"; c.Body.StripeStyle = 1; c.Body.CarNumber = 33;
            }, torque: 1.22, mass: 1190, mu: 1.10),
            PaintHex = "D97614",
        },
        new()
        {
            Id = "tsukasa", Name = "Tsukasa Type-R", Price = 46000,
            BuildConfig = () => With(VehicleConfig.PrototypeGtB(), c =>
            {
                c.Body.BodyLength = 4.0; c.Body.BodyWidth = 1.76; c.Body.BodyHeight = 0.40;
                c.Body.NoseLength = 0.95; c.Body.NoseDrop = 0.20;
                c.Body.CabinLength = 1.65; c.Body.CabinHeight = 0.38; c.Body.CabinShift = 0.42;
                c.Body.SpoilerSpan = 1.72; c.Body.SpoilerChord = 0.5; c.Body.SpoilerHeight = 0.34;
                c.Body.StripeHex = "C22A38"; c.Body.StripeStyle = 1; c.Body.CarNumber = 46;
            }, torque: 1.55, mass: 1080, mu: 1.14),
            PaintHex = "E8E4DA",
        },
        new()
        {
            Id = "onibi", Name = "Onibi GT Twin-Turbo", Price = 68000,
            BuildConfig = () => With(VehicleConfig.PrototypeGtB(), c =>
            {
                c.Body.BodyLength = 4.6; c.Body.BodyWidth = 1.86; c.Body.BodyHeight = 0.40;
                c.Body.NoseLength = 1.15; c.Body.NoseDrop = 0.14;
                c.Body.CabinLength = 1.8; c.Body.CabinHeight = 0.36; c.Body.CabinShift = 0.3;
                c.Body.SpoilerSpan = 1.8; c.Body.SpoilerChord = 0.52; c.Body.SpoilerHeight = 0.36;
                c.Body.StripeHex = "C7CCD6"; c.Body.StripeStyle = 1; c.Body.CarNumber = 7;
            }, torque: 2.0, mass: 1400),
            PaintHex = "4A4E56",
        },
        new()
        {
            Id = "gtLegend", Name = "Vega GT Legend", Price = 52000,
            BuildConfig = () => With(VehicleConfig.PrototypeGtB(), c =>
            {
                c.Body.BodyLength = 4.55; c.Body.BodyWidth = 1.8; c.Body.BodyHeight = 0.44;
                c.Body.NoseLength = 1.35; c.Body.NoseDrop = 0.24;
                c.Body.CabinLength = 1.6; c.Body.CabinHeight = 0.44; c.Body.CabinShift = -0.3;
                c.Body.SpoilerSpan = 1.6; c.Body.SpoilerChord = 0.3; c.Body.SpoilerHeight = 0.12;
                c.Body.StripeHex = "1A1A1A"; c.Body.StripeStyle = 1; c.Body.CarNumber = 12;
            }, torque: 1.38, mass: 1150, mu: 1.12, finalDrive: 4.4),
            PaintHex = "C9A227",
        },
    };

    public static CarSpec ById(string id) => All.FirstOrDefault(c => c.Id == id)
        ?? throw new KeyNotFoundException($"unknown car id {id}");

    private static VehicleConfig With(VehicleConfig baseConfig, Action<VehicleConfig>? style = null, double torque = 1.0, double mass = 0, double mu = 0, double finalDrive = 0)
    {
        var cfg = baseConfig;
        if (torque != 1.0)
            cfg.Drivetrain.TorqueCurve = cfg.Drivetrain.TorqueCurve.Select(p => (p.Rpm, p.Torque * torque)).ToArray();
        if (mass > 0) cfg.MassKg = mass;
        if (mu > 0) cfg.TireMuBase = mu;
        if (finalDrive > 0) cfg.Drivetrain.FinalDrive = finalDrive;
        style?.Invoke(cfg);
        return cfg;
    }
}
