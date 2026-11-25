namespace ApexGP.Core;

/// <summary>One recorded transform sample for a racer.</summary>
public struct ReplayFrame
{
    public double X, Y, Z;
    public double Qx, Qy, Qz, Qw;

    public System.Numerics.Vector3 Position => new((float)X, (float)Y, (float)Z);
    public System.Numerics.Quaternion Rotation => new((float)Qx, (float)Qy, (float)Qz, (float)Qw);
}

/// <summary>
/// Records racer transforms on a fixed grid so races can be replayed.
/// Called from the presentation layer every rendered frame (or on a timer);
/// sampling is nearest-frame by time. JSON round-trip enables saved replays.
/// </summary>
public sealed class ReplayRecorder
{
    private readonly List<double> _times = new();
    private readonly List<ReplayFrame[]> _frames = new();
    private int _racerCount;

    public int RacerCount => _racerCount;
    public int FrameCount => _frames.Count;
    public double Duration => _times.Count > 0 ? _times[^1] : 0;

    public void Capture(RaceController race, double time)
    {
        _racerCount = race.Racers.Count;
        var frames = new ReplayFrame[_racerCount];
        for (int i = 0; i < _racerCount; i++)
        {
            var car = race.Racers[i].Car;
            frames[i] = new ReplayFrame
            {
                X = car.Position.X, Y = car.Position.Y, Z = car.Position.Z,
                Qx = car.Orientation.X, Qy = car.Orientation.Y, Qz = car.Orientation.Z, Qw = car.Orientation.W,
            };
        }
        _times.Add(time);
        _frames.Add(frames);
    }

    /// <summary>Nearest-frame sample at <paramref name="time"/>; null before the first capture.</summary>
    public ReplayFrame[]? Sample(double time)
    {
        if (_times.Count == 0) return null;
        int lo = 0, hi = _times.Count - 1;
        while (lo < hi)
        {
            int mid = (lo + hi) / 2;
            if (_times[mid] < time) lo = mid + 1; else hi = mid;
        }
        int idx = (lo > 0 && (_times[lo] - time) > (time - _times[lo - 1])) ? lo - 1 : lo;
        return _frames[idx];
    }

    // ---- persistence ----
    public string ToJson()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("{\"racers\":").Append(_racerCount).Append(",\"times\":[");
        for (int i = 0; i < _times.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(_times[i].ToString("0.####", System.Globalization.CultureInfo.InvariantCulture));
        }
        sb.Append("],\"frames\":[");
        for (int f = 0; f < _frames.Count; f++)
        {
            if (f > 0) sb.Append(',');
            sb.Append('[');
            for (int r = 0; r < _racerCount; r++)
            {
                if (r > 0) sb.Append(',');
                var fr = _frames[f][r];
                sb.Append('[').Append(fr.X.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture))
                  .Append(',').Append(fr.Y.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture))
                  .Append(',').Append(fr.Z.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture))
                  .Append(',').Append(fr.Qx.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture))
                  .Append(',').Append(fr.Qy.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture))
                  .Append(',').Append(fr.Qz.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture))
                  .Append(',').Append(fr.Qw.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture))
                  .Append(']');
            }
            sb.Append(']');
        }
        sb.Append("]}");
        return sb.ToString();
    }

    public static ReplayRecorder FromJson(string json)
    {
        var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;
        var rec = new ReplayRecorder { _racerCount = root.GetProperty("racers").GetInt32() };
        var times = root.GetProperty("times");
        var frames = root.GetProperty("frames");
        for (int f = 0; f < times.GetArrayLength(); f++)
        {
            rec._times.Add(times[f].GetDouble());
            var arr = frames[f];
            var fs = new ReplayFrame[rec._racerCount];
            for (int r = 0; r < rec._racerCount; r++)
            {
                var q = arr[r];
                fs[r] = new ReplayFrame
                {
                    X = q[0].GetDouble(), Y = q[1].GetDouble(), Z = q[2].GetDouble(),
                    Qx = q[3].GetDouble(), Qy = q[4].GetDouble(), Qz = q[5].GetDouble(), Qw = q[6].GetDouble(),
                };
            }
            rec._frames.Add(fs);
        }
        return rec;
    }
}
