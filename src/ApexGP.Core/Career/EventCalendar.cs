namespace ApexGP.Core;

public sealed class EventDef
{
    public required string Id;
    public required string Name;
    public required string TrackName;
    public required int Laps;
    public required string? RequiredLicense;   // null = open
    public required double EntryFee;
    public required double Prize;
    public required double[] AiSpeedFactors;   // one per opponent
    public string[] RivalCarIds = [];          // optional: per-rival car specs (defaults to stock prototype)
    public int PlayerGridSlot = 0;

    public bool CanEnter(CareerProfile p) =>
        (RequiredLicense == null || p.Licenses.Contains(RequiredLicense)) && p.Credits >= EntryFee;
}

public static class EventCalendar
{
    public static readonly List<EventDef> All = new()
    {
        new()
        {
            Id = "sunriseCup1", Name = "Sunrise Cup — Round 1", TrackName = "Sunrise Loop",
            Laps = 2, RequiredLicense = null, EntryFee = 0, Prize = 5000,
            AiSpeedFactors = new[] { 0.90, 0.85 },
        },
        new()
        {
            Id = "sunriseCup2", Name = "Sunrise Cup — Round 2", TrackName = "Sunrise Loop",
            Laps = 3, RequiredLicense = "club", EntryFee = 500, Prize = 8000,
            AiSpeedFactors = new[] { 0.95, 0.92, 0.88 },
            RivalCarIds = ["touringS", "circuit1600", "touringS"],
        },
        new()
        {
            Id = "dolphinSprint", Name = "Dolphin Ring Sprint", TrackName = "Dolphin Ring",
            Laps = 2, RequiredLicense = "club", EntryFee = 800, Prize = 11000,
            AiSpeedFactors = new[] { 0.93, 0.90, 0.86 },
            RivalCarIds = ["touringS", "corvid", "apexRS"],
        },
        new()
        {
            Id = "kanyuSprint", Name = "Kanyu Hillclimb Sprint", TrackName = "Kanyu Pass",
            Laps = 2, RequiredLicense = "club", EntryFee = 600, Prize = 9500,
            AiSpeedFactors = new[] { 0.92, 0.89, 0.85 },
            RivalCarIds = ["touringS", "corvid", "apexRS"],
        },
        new()
        {
            Id = "kanyuChallenge", Name = "Kanyu Challenge", TrackName = "Kanyu Pass",
            Laps = 3, RequiredLicense = "national", EntryFee = 1200, Prize = 20000,
            AiSpeedFactors = new[] { 0.93, 0.92, 0.89, 0.86 },
            RivalCarIds = ["tsukasa", "onibi", "gtLegend", "tsukasa"],
        },
        new()
        {
            Id = "sunriseEnduro", Name = "Sunrise Enduro", TrackName = "Sunrise Loop",
            Laps = 5, RequiredLicense = "national", EntryFee = 1000, Prize = 16000,
            AiSpeedFactors = new[] { 0.98, 0.96, 0.94, 0.90 },
        },
    };

    public static EventDef ById(string id) => All.FirstOrDefault(e => e.Id == id)
        ?? throw new KeyNotFoundException($"unknown event id {id}");
}

/// <summary>High-level career operations (buy/tune/test/race). Pure logic — the UI calls these.</summary>
public static class Career
{
    public static string BuyCar(CareerProfile p, CarSpec spec)
    {
        if (p.Credits < spec.Price)
            throw new InvalidOperationException("not enough credits");
        p.Credits -= spec.Price;
        var owned = new CarOwnership { CarId = spec.Id };
        p.Garage.Add(owned);
        if (p.ActiveCarId == "") p.ActiveCarId = spec.Id;
        return spec.Id;
    }

    public static void TuneCar(CareerProfile p, string partId, string? carId = null)
    {
        var part = TunePart.ById(partId);
        carId ??= p.ActiveCarId;
        var car = p.Garage.FirstOrDefault(c => c.CarId == carId)
            ?? throw new InvalidOperationException("car not in garage");
        if (p.Credits < part.Price)
            throw new InvalidOperationException("not enough credits");
        if (car.TuningParts.Contains(partId))
            throw new InvalidOperationException("already installed");
        p.Credits -= part.Price;
        car.TuningParts.Add(partId);
    }

    public static VehicleConfig ConfigFor(CareerProfile p, string? carId = null)
    {
        carId ??= p.ActiveCarId;
        var car = p.Garage.FirstOrDefault(c => c.CarId == carId)
            ?? throw new InvalidOperationException("no active car");
        return Tuning.Apply(CarSpec.ById(car.CarId).BuildConfig(), car.TuningParts);
    }

    public static LicenseRun StartLicenseTest(TrackSurface track, LicenseTestDef def, VehicleConfig config)
        => new(track, def);

    public static RaceController StartEvent(CareerProfile p, EventDef ev, TrackSurface track, VehicleConfig playerConfig, bool playerIsAi = false)
    {
        if (!ev.CanEnter(p))
            throw new InvalidOperationException("event requirements not met");
        var race = new RaceController(track, ev.Laps);
        var playerAi = playerIsAi ? new RacingLineFollower(track) { SpeedFactor = 1.0 } : null;
        race.AddRacer("YOU", playerConfig, isPlayer: true, ai: playerAi, lateralOffset: 3.0);
        for (int i = 0; i < ev.AiSpeedFactors.Length; i++)
        {
            var ai = new RacingLineFollower(track) { SpeedFactor = ev.AiSpeedFactors[i] };
            var spec = i < ev.RivalCarIds.Length ? CarSpec.ById(ev.RivalCarIds[i]) : null;
            race.AddRacer($"RIVAL-{i + 1}", spec?.BuildConfig() ?? VehicleConfig.PrototypeGtB(), isPlayer: false, ai: ai, lateralOffset: i % 2 == 0 ? -3.0 : 3.0);
        }
        return race;
    }

    /// <summary>Called with the finished race to pay out.</summary>
    public static (double Prize, int Position) CollectPrize(CareerProfile p, EventDef ev, RaceController race)
    {
        var player = race.Racers.First(r => r.IsPlayer);
        int fieldSize = race.Racers.Count;
        double prize = ev.Prize * (fieldSize - player.Position) / System.Math.Max(1, fieldSize - 1);
        if (player.Position == 1) prize = ev.Prize;
        p.Credits += prize;
        if (player.Position == 1) p.CompletedEvents.Add(ev.Id);
        return (prize, player.Position);
    }
}
