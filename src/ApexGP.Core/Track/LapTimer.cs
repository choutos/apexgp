namespace ApexGP.Core;

/// <summary>
/// Lap timing for one car on a track. Tracks progress by nearest-sample index;
/// a lap completes when progress wraps forward past the start line.
/// </summary>
public sealed class LapTimer
{
    private readonly TrackSurface _track;
    private int _lastIdx;
    private double _maxProgress;
    private double _lapStart;
    private double _now;

    public int LapsCompleted { get; private set; }
    public double BestLap { get; private set; } = double.MaxValue;
    public double CurrentLapTime => _now - _lapStart;

    public LapTimer(TrackSurface track)
    {
        _track = track;
        _lapStart = 0;
        _lastIdx = -1;
    }

    public void Tick(Vehicle v, double dt)
    {
        _now += dt;
        int idx = _track.NearestIndex(v.Position.X, v.Position.Z, _lastIdx);
        if (idx == _lastIdx) return;
        if (_lastIdx < 0) { _lastIdx = idx; return; }

        int n = _track.Samples.Length;
        // track max forward progress since the last crossing (guards against cuts)
        int step = idx - _lastIdx;
        if (step < -n / 2) step += n;   // wrapped forward
        else if (step > n / 2) step -= n; // wrapped backward
        if (step > 0) _maxProgress = System.Math.Min(_maxProgress + step * _track.TotalLength / n, _track.TotalLength);

        if (_lastIdx > n * 0.75 && idx < n * 0.25 && _maxProgress >= _track.TotalLength * 0.7)
        {
            double lap = CurrentLapTime;
            if (LapsCompleted > 0 || lap > 5.0) // ignore an instant false first lap
            {
                LapsCompleted++;
                if (lap < BestLap) BestLap = lap;
            }
            _lapStart = _now;
            _maxProgress = 0;
        }
        // NOTE: on a rejected wrap we keep _maxProgress — the car may still be mid-lap
        _lastIdx = idx;
    }
}
