namespace ApexGP.Core;

/// <summary>A licence test: drive through all gates before time runs out.</summary>
public sealed class LicenseTestDef
{
    public required string Id;
    public required string Name;
    public required string TrackName;
    public required double TimeLimit;
    public required int GateCount;   // evenly spaced along the racing line
}

public static class LicenseCatalog
{
    public static readonly List<LicenseTestDef> All = new()
    {
        new() { Id = "club", Name = "Club Licence", TrackName = "Sunrise Loop", TimeLimit = 95, GateCount = 8 },
        new() { Id = "national", Name = "National Licence", TrackName = "Dolphin Ring", TimeLimit = 82, GateCount = 10 },
    };

    public static LicenseTestDef ById(string id) => All.FirstOrDefault(l => l.Id == id)
        ?? throw new KeyNotFoundException($"unknown licence id {id}");
}

/// <summary>
/// Runtime checker for a licence test: tracks gate progress and time.
/// Feed Tick every physics step while the car is being driven.
/// </summary>
public sealed class LicenseRun
{
    private readonly TrackSurface _track;
    private readonly LicenseTestDef _def;
    private readonly double[] _gateS;
    private int _nextGate;
    private int _hint = -1;
    private double _time;
    public int GatesPassed => _nextGate;
    public bool AllPassed => _nextGate >= _def.GateCount;
    public bool TimeExpired => _time >= _def.TimeLimit;
    public double Elapsed => _time;
    public double FinishTime { get; private set; } = double.MaxValue;

    public LicenseRun(TrackSurface track, LicenseTestDef def)
    {
        _track = track;
        _def = def;
        _gateS = new double[def.GateCount];
        for (int i = 0; i < def.GateCount; i++)
            _gateS[i] = (i + 1) * _track.TotalLength / (def.GateCount + 1); // not on the start straight
    }

    public void Tick(Vehicle v, double dt)
    {
        if (AllPassed || TimeExpired) return;
        _time += dt;

        _hint = _track.NearestIndex(v.Position.X, v.Position.Z, _hint);
        int idx = _hint;
        double s = _track.Samples[idx].S;
        while (_nextGate < _def.GateCount && s >= _gateS[_nextGate])
        {
            double lat = _track.LateralOffset(idx, v.Position.X, v.Position.Z);
            if (System.Math.Abs(lat) < _track.HalfWidth * 0.95) // must pass on the track
                _nextGate++;
            else
                break; // missed the gate by going off — retry when back on the line
        }

        if (AllPassed && FinishTime > _time)
            FinishTime = _time;
    }
}
