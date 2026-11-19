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

        // --- guard rails both sides ---
        var railMat = new StandardMaterial3D { AlbedoColor = new Color(0.75f, 0.74f, 0.7f), Roughness = 0.6f, Metallic = 0.3f };
        foreach (int side in new[] { -1, 1 })
        {
            var rail = new SurfaceTool();
            rail.Begin(Godot.Mesh.PrimitiveType.Triangles);
            rail.SetMaterial(railMat);
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                var s0 = track.Samples[i];
                var s1 = track.Samples[j];
                double r0 = hw * 1.25 * side, r1 = (hw * 1.25 + 0.35) * side;
                AddQuad(rail,
                    Xy(s0, r0, 0.25f), Xy(s0, r1, 0.25f),
                    Xy(s1, r1, 0.25f), Xy(s1, r0, 0.25f));   // top face
                AddQuad(rail,
                    Xy(s0, r1, 0.0f), Xy(s0, r1, 0.25f),
                    Xy(s1, r1, 0.25f), Xy(s1, r1, 0.0f));    // outer face
            }
            root.AddChild(new MeshInstance3D { Mesh = rail.Commit() });
        }

        // --- trees (cones on trunks), scattered along the outside ---
        var rng = new System.Random(1994);
        var treeMat = new StandardMaterial3D { AlbedoColor = new Color(0.13f, 0.34f, 0.16f), Roughness = 1f };
        var trunkMat = new StandardMaterial3D { AlbedoColor = new Color(0.32f, 0.22f, 0.12f) };
        int trees = System.Math.Max(24, n / 8);
        for (int t = 0; t < trees; t++)
        {
            int i = rng.Next(n);
            var s = track.Samples[i];
            double side = rng.NextDouble() > 0.5 ? 1 : -1;
            double dist = hw + 4.0 + rng.NextDouble() * 9.0;
            double tx = s.X + s.RightX * dist * side;
            double tz = s.Z + s.RightZ * dist * side;
            double scale = 0.8 + rng.NextDouble() * 0.9;

            var tree = new Node3D();
            var trunk = new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0.12f, BottomRadius = 0.16f, Height = 1.1f, RadialSegments = 6 },
                Position = new Vector3(0, 0.55f * (float)scale, 0),
                Scale = new Vector3(1, (float)scale, 1),
            };
            trunk.MaterialOverride = trunkMat;
            tree.AddChild(trunk);
            var crown = new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0.0f, BottomRadius = 1.05f, Height = 2.2f, RadialSegments = 6 },
                Position = new Vector3(0, (1.1f + 1.1f) * (float)scale, 0),
                Scale = new Vector3((float)scale, (float)scale, (float)scale),
            };
            crown.MaterialOverride = treeMat;
            tree.AddChild(crown);
            tree.Position = new Vector3((float)tx, 0f, (float)tz);
            root.AddChild(tree);
        }

        // --- start gantry ---
        var gantry = new Node3D();
        var pillarMat = new StandardMaterial3D { AlbedoColor = new Color(0.2f, 0.2f, 0.22f), Metallic = 0.4f, Roughness = 0.5f };
        foreach (int side in new[] { -1, 1 })
        {
            var pillar = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(0.35f, 5.4f, 0.35f) },
                Position = new Vector3((float)(start.X + start.RightX * (hw + 1.2) * side), 2.7f,
                                       (float)(start.Z + start.RightZ * (hw + 1.2) * side)),
            };
            pillar.MaterialOverride = pillarMat;
            gantry.AddChild(pillar);
        }
        var banner = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(2.2f * (float)hw, 0.7f, 0.12f) },
            Position = new Vector3((float)start.X, 5.1f, (float)start.Z),
            Rotation = new Vector3(0, (float)System.Math.Atan2(-start.DirX, -start.DirZ) + Mathf.Pi / 2f, 0),
        };
        banner.MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.85f, 0.15f, 0.12f), Roughness = 0.7f };
        gantry.AddChild(banner);
        root.AddChild(gantry);

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
