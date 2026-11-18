using Godot;
using ApexGP.Core;

namespace ApexGP.Game;

/// <summary>Builds the visual track from Core geometry: tarmac, kerbs, start line.</summary>
public static class TrackMesh
{
    public static Node3D Build(TrackSurface track)
    {
        var root = new Node3D { Name = "Track" };
        int n = track.Samples.Length;
        double hw = track.HalfWidth;

        // --- tarmac ribbon ---
        var road = new SurfaceTool();
        road.Begin(Godot.Mesh.PrimitiveType.Triangles);
        var roadMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.24f, 0.24f, 0.26f),
            Roughness = 0.92f,
        };
        road.SetMaterial(roadMat);
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            var s = track.Samples[i];
            var e = track.Samples[j];
            AddQuad(road,
                Xy(s, -hw, 0.02f), Xy(s, +hw, 0.02f),
                Xy(e, +hw, 0.02f), Xy(e, -hw, 0.02f));
        }
        var roadMesh = new MeshInstance3D { Mesh = road.Commit() };
        root.AddChild(roadMesh);

        // --- kerb strips (alternating red/white) both sides ---
        for (int side = 0; side < 2; side++)
        {
            double inner = side == 0 ? hw * 0.92 : hw * 1.02;
            double outer = side == 0 ? hw * 1.02 : hw * 1.15;
            var kerb = new SurfaceTool();
            kerb.Begin(Godot.Mesh.PrimitiveType.Triangles);
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                var s = track.Samples[i];
                var e = track.Samples[j];
                bool red = (i / 2) % 2 == 0;
                kerb.SetColor(side == 0 || red ? new Color(0.85f, 0.1f, 0.08f) : new Color(0.92f, 0.92f, 0.9f));
                AddQuad(kerb, Xy(s, inner, 0.03f), Xy(s, outer, 0.03f), Xy(e, outer, 0.03f), Xy(e, inner, 0.03f));
            }
            var kerbMesh = new MeshInstance3D { Mesh = kerb.Commit() };
            root.AddChild(kerbMesh);
        }

        // --- start line ---
        var start = track.Samples[0];
        var sl = new SurfaceTool();
        sl.Begin(Godot.Mesh.PrimitiveType.Triangles);
        sl.SetMaterial(new StandardMaterial3D { AlbedoColor = new Color(0.92f, 0.92f, 0.92f), Roughness = 0.8f });
        double slw = hw * 0.9, slt = 1.2;
        AddQuad(sl,
            P(start, -slw, -slt / 2, 0.04f), P(start, +slw, -slt / 2, 0.04f),
            P(start, +slw, +slt / 2, 0.04f), P(start, -slw, +slt / 2, 0.04f));
        root.AddChild(new MeshInstance3D { Mesh = sl.Commit() });

        return root;
    }

    private static Vector3 Xy(TrackSample s, double lateral, float y)
        => new((float)(s.X + s.RightX * lateral), y, (float)(s.Z + s.RightZ * lateral));

    private static Vector3 P(TrackSample s, double lateral, double along, float y)
        => new((float)(s.X + s.RightX * lateral - s.DirX * along), y, (float)(s.Z + s.RightZ * lateral - s.DirZ * along));

    private static void AddQuad(SurfaceTool st, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        st.SetNormal(Vector3.Up);
        st.AddVertex(a); st.AddVertex(b); st.AddVertex(c);
        st.SetNormal(Vector3.Up);
        st.AddVertex(a); st.AddVertex(c); st.AddVertex(d);
    }
}
