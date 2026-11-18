using System.Numerics;

namespace ApexGP.Core;

/// <summary>Authoring data for a circuit: control points of a closed loop plus metadata.</summary>
public sealed class TrackData
{
    public string Name = "";
    public List<Vector2> Points = new();
    public double Width = 14.0;

    public TrackData(string name, double width, params (float x, float z)[] pts)
    {
        Name = name;
        Width = width;
        foreach (var (x, z) in pts) Points.Add(new Vector2(x, z));
    }
}

/// <summary>A sampled point along the track centreline.</summary>
public sealed class TrackSample
{
    public double S;        // arc length along the loop
    public double X, Z;
    public double DirX, DirZ;    // unit tangent
    public double RightX, RightZ; // unit right-hand normal
    public double Curvature;     // signed: + = turning left
}

public interface ITrackSurface
{
    /// <summary>Nearest sample index; hint = caller's last known index, -1 = full scan.</summary>
    int NearestIndex(double x, double z, int hint);

    /// <summary>Signed lateral offset from the centreline at a given sample.</summary>
    double LateralOffset(int idx, double x, double z);

    /// <summary>Grip multiplier given the resolved sample index (1.0 = full grip).</summary>
    double GripMultiplierAt(int idx, double x, double z);
}

/// <summary>
/// Built track: sampled centreline of a closed Catmull-Rom loop, racing line,
/// grip model and mesh geometry. Pure math — fully headless-testable.
/// </summary>
public sealed class TrackSurface : ITrackSurface
{
    public TrackData Data = null!;
    public TrackSample[] Samples = Array.Empty<TrackSample>();
    public double[] RacingOffset = Array.Empty<double>(); // lateral offset of the racing line per sample
    public double[] SpeedTargets = Array.Empty<double>(); // m/s target per sample (braking-aware)
    public double TotalLength;
    public double HalfWidth;

    private const double SampleSpacing = 4.0;   // m between centreline samples

    public static TrackSurface Build(TrackData data)
    {
        var t = new TrackSurface { Data = data, HalfWidth = data.Width / 2.0 };
        var pts = data.Points;
        int n = pts.Count;
        if (n < 3) throw new ArgumentException("track needs at least 3 control points");

        // Dense Catmull-Rom sampling of the closed loop.
        var raw = new List<(double x, double z)>();
        for (int i = 0; i < n; i++)
        {
            var p0 = pts[(i - 1 + n) % n];
            var p1 = pts[i];
            var p2 = pts[(i + 1) % n];
            var p3 = pts[(i + 2) % n];
            // approx segment length to pick a subdivision count
            double segLen = (float)Vector2.Distance(p1, p2);
            int sub = System.Math.Max(2, (int)(segLen / SampleSpacing));
            for (int j = 0; j < sub; j++)
            {
                double u = (double)j / sub;
                raw.Add(CatmullRom(p0, p1, p2, p3, u));
            }
        }

        // arc-length resample to uniform spacing (explicitly close the raw loop first)
        raw.Add(raw[0]);
        var cum = new List<double> { 0 };
        for (int i = 1; i <= raw.Count; i++)
        {
            var a = raw[i - 1];
            var b = raw[i % raw.Count];
            cum.Add(cum[^1] + System.Math.Sqrt((b.x - a.x) * (b.x - a.x) + (b.z - a.z) * (b.z - a.z)));
        }
        t.TotalLength = cum[^1];
        int count = System.Math.Max(8, (int)(t.TotalLength / SampleSpacing));
        var samples = new List<TrackSample>();
        int rawIdx = 0;
        for (int i = 0; i < count; i++)
        {
            double sTarget = i * t.TotalLength / count;
            while (rawIdx < raw.Count - 1 && cum[rawIdx + 1] < sTarget) rawIdx++;
            double segLen = cum[rawIdx + 1] - cum[rawIdx];
            double f = segLen > 1e-6 ? System.Math.Clamp((sTarget - cum[rawIdx]) / segLen, 0, 1) : 0;
            double px = raw[rawIdx].x + (raw[rawIdx + 1].x - raw[rawIdx].x) * f;
            double pz = raw[rawIdx].z + (raw[rawIdx + 1].z - raw[rawIdx].z) * f;
            samples.Add(new TrackSample { S = sTarget, X = px, Z = pz });
        }

        // tangents / normals / curvature
        for (int i = 0; i < count; i++)
        {
            var prev = samples[(i - 1 + count) % count];
            var cur = samples[i];
            var next = samples[(i + 1) % count];
            double dx = next.X - prev.X, dz = next.Z - prev.Z;
            double len = System.Math.Sqrt(dx * dx + dz * dz);
            if (len < 1e-6) len = 1e-6;
            cur.DirX = dx / len; cur.DirZ = dz / len;
            cur.RightX = cur.DirZ; cur.RightZ = -cur.DirX; // right-hand of travel
        }
        for (int i = 0; i < count; i++)
        {
            var prev = samples[(i - 1 + count) % count];
            var cur = samples[i];
            var next = samples[(i + 1) % count];
            double ds = cur.S - prev.S; if (ds <= 0) ds += t.TotalLength;
            double cross = prev.DirX * next.DirZ - prev.DirZ * next.DirX;
            double angle = System.Math.Asin(System.Math.Clamp(cross, -1, 1));
            cur.Curvature = angle / ds;
        }

        t.Samples = samples.ToArray();

        // racing line: cut to the inside, then smooth
        var off = new double[count];
        for (int i = 0; i < count; i++)
        {
            double k = t.Samples[i].Curvature;
            off[i] = -System.Math.Sign(k) * System.Math.Min(System.Math.Abs(k) * 260.0, t.HalfWidth * 0.55);
        }
        t.RacingOffset = Smooth(off, 9);

        // speed profile from the RACING LINE's own curvature (the offset path bends
        // differently from the centreline — that's what the car actually drives),
        // then a backward pass so the car starts braking before every corner.
        var rp = new (double x, double z)[count];
        for (int i = 0; i < count; i++)
            rp[i] = (samples[i].X + samples[i].RightX * t.RacingOffset[i],
                     samples[i].Z + samples[i].RightZ * t.RacingOffset[i]);

        var vlim = new double[count];
        const double aLatLimit = 8.0, aBrake = 9.0;
        double stepM = t.TotalLength / count;
        for (int i = 0; i < count; i++)
        {
            var p0 = rp[(i - 1 + count) % count];
            var p1 = rp[i];
            var p2 = rp[(i + 1) % count];
            double a1x = p1.x - p0.x, a1z = p1.z - p0.z;
            double a2x = p2.x - p1.x, a2z = p2.z - p1.z;
            double l1 = System.Math.Sqrt(a1x * a1x + a1z * a1z), l2 = System.Math.Sqrt(a2x * a2x + a2z * a2z);
            double cross = (a1x * a2z - a1z * a2x) / (l1 * l2 + 1e-9);
            double k = System.Math.Abs(2 * System.Math.Asin(System.Math.Clamp(cross, -1, 1)) / (l1 + l2));
            vlim[i] = System.Math.Min(System.Math.Sqrt(aLatLimit / System.Math.Max(k, 1e-4)), 75.0);
        }
        for (int pass = 0; pass < 2; pass++)
            for (int i = count - 1; i >= 0; i--)
            {
                int nxt = (i + 1) % count;
                vlim[i] = System.Math.Min(vlim[i], System.Math.Sqrt(vlim[nxt] * vlim[nxt] + 2 * aBrake * stepM));
            }
        t.SpeedTargets = vlim;

        return t;
    }

    private static (double x, double z) CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, double u)
    {
        double u2 = u * u, u3 = u2 * u;
        double x = 0.5 * ((2 * p1.X) + (-p0.X + p2.X) * u + (2 * p0.X - 5 * p1.X + 4 * p2.X - p3.X) * u2 + (-p0.X + 3 * p1.X - 3 * p2.X + p3.X) * u3);
        double z = 0.5 * ((2 * p1.Y) + (-p0.Y + p2.Y) * u + (2 * p0.Y - 5 * p1.Y + 4 * p2.Y - p3.Y) * u2 + (-p0.Y + 3 * p1.Y - 3 * p2.Y + p3.Y) * u3);
        return (x, z);
    }

    private static double[] Smooth(double[] v, int radius)
    {
        int n = v.Length;
        var outv = new double[n];
        for (int i = 0; i < n; i++)
        {
            double sum = 0; int c = 0;
            for (int j = -radius; j <= radius; j++)
            {
                sum += v[(i + j + n) % n]; c++;
            }
            outv[i] = sum / c;
        }
        return outv;
    }

    /// <summary>Nearest sample index, searching locally around a hint (fast path), else full scan.</summary>
    public int NearestIndex(double x, double z, int hint)
    {
        int n = Samples.Length;
        if (hint < 0 || hint >= n)
        {
            double best = double.MaxValue; int bi = 0;
            for (int i = 0; i < n; i++)
            {
                double d = Dist2(Samples[i].X, Samples[i].Z, x, z);
                if (d < best) { best = d; bi = i; }
            }
            return bi;
        }
        int bestIdx = hint; double bestD = double.MaxValue;
        for (int off = -4; off <= 12; off++)
        {
            int i = (hint + off + n) % n;
            double d = Dist2(Samples[i].X, Samples[i].Z, x, z);
            if (d < bestD) { bestD = d; bestIdx = i; }
        }
        if (bestD > 2500.0) return NearestIndex(x, z, -1); // lost: full rescan
        return bestIdx;
    }

    /// <summary>Signed lateral offset from the centreline (+ = right of travel direction).</summary>
    public double LateralOffset(int idx, double x, double z)
    {
        var s = Samples[idx];
        return (x - s.X) * s.RightX + (z - s.Z) * s.RightZ;
    }

    public double GripMultiplierAt(int idx, double x, double z)
    {
        double lat = System.Math.Abs(LateralOffset(idx, x, z));
        if (lat < HalfWidth * 0.92) return 1.0;          // tarmac
        if (lat < HalfWidth * 1.15) return 0.85;         // kerbs
        return 0.45;                                      // grass
    }

    /// <summary>Racing-line world position ahead of a given progress.</summary>
    public (double x, double z) RacingLineAt(int idx)
    {
        var s = Samples[idx];
        double o = RacingOffset[idx];
        return (s.X + s.RightX * o, s.Z + s.RightZ * o);
    }

    private static double Dist2(double ax, double az, double bx, double bz)
        => (ax - bx) * (ax - bx) + (az - bz) * (az - bz);
}
