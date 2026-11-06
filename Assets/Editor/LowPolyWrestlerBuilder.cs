using System.Collections.Generic;
using System.IO;
using SumoGame;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds the low poly wrestler: faceted, flat shaded meshes on the same joint rig as
/// <see cref="WrestlerPuppetBuilder"/>, so <see cref="WrestlerView"/> animates it unchanged.
/// Pivot is at the feet, +Z forward. Meshes are saved under <see cref="MeshFolder"/> and shared by both wrestlers.
/// </summary>
public static class LowPolyWrestlerBuilder
{
    const string MeshFolder = "Assets/Art/Wrestler";

    /// <summary>One cross-section of a lathed part: an ellipse at height Y, shifted forward by Z and tipped by Tilt.</summary>
    struct Ring
    {
        public float Y, RX, RZ, Z, Tilt;
        public Ring(float y, float rx, float rz, float z = 0f, float tilt = 0f)
        {
            Y = y; RX = rx; RZ = rz; Z = z; Tilt = tilt;
        }
        // Only two arguments, so a three number ring always means (y, width, depth), never a shifted circle.
        public Ring(float y, float r) : this(y, r, r) { }
    }

    public static WrestlerView Build(string name, Transform parent, WrestlerPuppetBuilder.Palette palette)
    {
        Directory.CreateDirectory(MeshFolder);
        var root = new GameObject(name);
        root.transform.SetParent(parent, false);

        var pelvis = Joint("Pelvis", root.transform, new Vector3(0f, 0.74f, 0f));
        var spine = Joint("Spine", pelvis, Vector3.zero);
        Torso(spine, palette);

        var head = Joint("Head", spine, new Vector3(0f, 1f, 0.08f));
        Head(head, palette);

        var view = root.AddComponent<WrestlerView>();
        view.Pelvis = pelvis;
        view.Spine = spine;
        view.Head = head;
        (view.ArmL, view.ForearmL) = Arm("L", spine, -1f, palette.Skin);
        (view.ArmR, view.ForearmR) = Arm("R", spine, 1f, palette.Skin);
        (view.ThighL, view.ShinL, view.FootL) = Leg("L", pelvis, -1f, palette.Skin);
        (view.ThighR, view.ShinR, view.FootR) = Leg("R", pelvis, 1f, palette.Skin);
        return view;
    }

    static void Torso(Transform spine, WrestlerPuppetBuilder.Palette palette)
    {
        // Pear shaped: the belly is the widest point and sits forward, the chest narrows into a thick neck.
        Part("Body", spine, palette.Skin, () => Lathe(10, true, true,
            new Ring(-0.32f, 0.3f, 0.28f, 0.04f),
            new Ring(-0.15f, 0.47f, 0.44f, 0.06f),
            new Ring(0.06f, 0.56f, 0.53f, 0.1f),
            new Ring(0.28f, 0.54f, 0.5f, 0.08f),
            new Ring(0.48f, 0.5f, 0.42f, 0.03f),
            new Ring(0.68f, 0.5f, 0.37f, 0f),
            new Ring(0.84f, 0.38f, 0.29f, 0f),
            new Ring(0.93f, 0.19f, 0.17f, 0.04f),
            new Ring(1f, 0.15f, 0.14f, 0.06f)));

        Part("MawashiBelt", spine, palette.Mawashi, () => Lathe(10, true, false,
            new Ring(-0.36f, 0.33f, 0.31f, 0.04f),
            new Ring(-0.24f, 0.44f, 0.42f, 0.05f),
            new Ring(0f, 0.575f, 0.545f, 0.09f),
            new Ring(0.05f, 0.57f, 0.54f, 0.095f)));

        // Front flap, crotch strap and the knot at the back.
        Part("MawashiFront", spine, palette.Mawashi, () => Box(new Vector3(0f, -0.26f, 0.58f), new Vector3(0.28f, 0.34f, 0.07f), new Vector3(-12f, 0f, 0f), 0.75f));
        Part("MawashiStrap", spine, palette.Mawashi, () => Box(new Vector3(0f, -0.38f, 0.05f), new Vector3(0.22f, 0.1f, 0.74f), Vector3.zero, 0.8f));
        Part("MawashiKnot", spine, palette.Mawashi, () => Merge(
            Box(new Vector3(0f, -0.02f, -0.5f), new Vector3(0.2f, 0.16f, 0.1f), new Vector3(0f, 0f, 45f)),
            Box(new Vector3(0f, -0.16f, -0.43f), new Vector3(0.13f, 0.26f, 0.05f), new Vector3(-20f, 0f, 0f), 0.8f)));
    }

    static void Head(Transform head, WrestlerPuppetBuilder.Palette palette)
    {
        // Wide jowls and a narrower crown.
        Part("Skull", head, palette.Skin, () => Lathe(8, true, true,
            new Ring(-0.22f, 0.12f, 0.12f, 0.04f),
            new Ring(-0.12f, 0.23f, 0.22f, 0.03f),
            new Ring(0.02f, 0.235f, 0.23f, 0.01f),
            new Ring(0.14f, 0.21f, 0.21f, -0.01f),
            new Ring(0.22f, 0.14f, 0.15f, -0.02f),
            new Ring(0.26f, 0.05f, 0.05f, -0.03f)));

        // Hairline is tipped so it sits high on the forehead and low at the nape.
        Part("Hair", head, palette.Hair, () => Lathe(8, false, true,
            new Ring(0.04f, 0.245f, 0.24f, -0.02f, 0.3f),
            new Ring(0.15f, 0.225f, 0.225f, -0.025f, 0.15f),
            new Ring(0.23f, 0.155f, 0.165f, -0.03f),
            new Ring(0.285f, 0.06f, 0.06f, -0.04f)));

        // The topknot lies forward along the crown and fans out at the front.
        Part("Topknot", head, palette.Hair, () => Merge(
            Lathe(6, true, true, Rotated(new Vector3(0f, 0.27f, -0.13f), new Vector3(82f, 0f, 0f)),
                new Ring(0f, 0.045f, 0.04f), new Ring(0.12f, 0.05f, 0.04f), new Ring(0.2f, 0.04f, 0.035f)),
            Lathe(6, true, true, Rotated(new Vector3(0f, 0.3f, 0.07f), new Vector3(84f, 0f, 0f)),
                new Ring(-0.01f, 0.04f, 0.03f), new Ring(0.05f, 0.1f, 0.035f), new Ring(0.09f, 0.08f, 0.02f))));

        Part("Ears", head, palette.Skin, () => Merge(
            Box(new Vector3(-0.235f, 0f, 0.01f), new Vector3(0.05f, 0.1f, 0.08f), new Vector3(0f, -15f, 0f), 0.7f),
            Box(new Vector3(0.235f, 0f, 0.01f), new Vector3(0.05f, 0.1f, 0.08f), new Vector3(0f, 15f, 0f), 0.7f)));
        Part("Nose", head, palette.Skin, () => Box(new Vector3(0f, -0.02f, 0.235f), new Vector3(0.07f, 0.08f, 0.06f), new Vector3(-10f, 0f, 0f), 0.6f));

        Part("Eyes", head, palette.Eye, () => Merge(
            Box(new Vector3(-0.085f, 0.04f, 0.218f), new Vector3(0.05f, 0.03f, 0.02f), new Vector3(0f, -18f, 0f)),
            Box(new Vector3(0.085f, 0.04f, 0.218f), new Vector3(0.05f, 0.03f, 0.02f), new Vector3(0f, 18f, 0f))));
        Part("Brows", head, palette.Hair, () => Merge(
            Box(new Vector3(-0.085f, 0.09f, 0.214f), new Vector3(0.08f, 0.025f, 0.03f), new Vector3(0f, -18f, 8f)),
            Box(new Vector3(0.085f, 0.09f, 0.214f), new Vector3(0.08f, 0.025f, 0.03f), new Vector3(0f, 18f, -8f))));
    }

    static (Transform, Transform) Arm(string side, Transform spine, float sign, Material skin)
    {
        var shoulder = Joint("Arm" + side, spine, new Vector3(sign * 0.54f, 0.76f, 0f));
        // Limbs use an even number of sides, so opposite sides match and nothing looks bent when seen side on.
        // Shoulder and elbow end in domes rather than flat lids, and each segment overlaps the next,
        // so a bent elbow closes up instead of opening a step.
        Part("Upper", shoulder, skin, () => Lathe(8, true, true,
            new Ring(0.135f, 0.02f),
            new Ring(0.1f, 0.11f, 0.1f),
            new Ring(0.05f, 0.155f, 0.145f),
            new Ring(0f, 0.175f, 0.165f),
            new Ring(-0.16f, 0.16f, 0.15f),
            new Ring(-0.38f, 0.125f, 0.12f),
            new Ring(-0.54f, 0.11f),
            new Ring(-0.6f, 0.07f),
            new Ring(-0.62f, 0.03f)));

        var forearm = Joint("Forearm" + side, shoulder, new Vector3(0f, -0.54f, 0f));
        Part("Lower", forearm, skin, () => Lathe(8, true, true,
            new Ring(0.08f, 0.05f),
            new Ring(0.04f, 0.1f),
            new Ring(0f, 0.115f, 0.11f),
            new Ring(-0.14f, 0.12f, 0.11f),
            new Ring(-0.38f, 0.08f, 0.072f)));

        // A closed fist, a little wider than it is deep.
        Part("Hand", forearm, skin, () => Lathe(8, true, true,
            new Ring(-0.36f, 0.078f, 0.07f),
            new Ring(-0.42f, 0.105f, 0.085f, 0.01f),
            new Ring(-0.51f, 0.105f, 0.085f, 0.015f),
            new Ring(-0.57f, 0.07f, 0.06f, 0.015f),
            new Ring(-0.59f, 0.03f, 0.03f, 0.015f)));
        return (shoulder, forearm);
    }

    static (Transform, Transform, Transform) Leg(string side, Transform pelvis, float sign, Material skin)
    {
        var thigh = Joint("Thigh" + side, pelvis, new Vector3(sign * 0.3f, 0f, 0f));
        Part("Thigh", thigh, skin, () => Lathe(8, true, true,
            new Ring(0.08f, 0.19f),
            new Ring(-0.04f, 0.24f, 0.23f),
            new Ring(-0.24f, 0.21f, 0.2f, 0.01f),
            new Ring(-0.42f, 0.15f, 0.15f, 0.01f)));

        var shin = Joint("Shin" + side, thigh, new Vector3(0f, -0.38f, 0f));
        Part("Shin", shin, skin, () => Lathe(8, true, true,
            new Ring(0.02f, 0.15f),
            new Ring(-0.12f, 0.155f, 0.16f, -0.025f),
            new Ring(-0.3f, 0.1f),
            new Ring(-0.38f, 0.085f)));

        // Flat soled and wide at the toes, lofted from heel to toe.
        var foot = Joint("Foot" + side, shin, new Vector3(0f, -0.36f, 0f));
        Part("Foot", foot, skin, () => Lathe(6, true, true, Rotated(Vector3.zero, new Vector3(90f, 0f, 0f)),
            new Ring(-0.12f, 0.09f, 0.06f, 0.0f),
            new Ring(-0.04f, 0.12f, 0.08f, -0.02f),
            new Ring(0.14f, 0.145f, 0.065f, -0.005f),
            new Ring(0.25f, 0.11f, 0.035f, 0.025f)));
        return (thigh, shin, foot);
    }

    static Transform Joint(string name, Transform parent, Vector3 position)
    {
        var joint = new GameObject(name).transform;
        joint.SetParent(parent, false);
        joint.localPosition = position;
        return joint;
    }

    /// <summary>Adds a renderer for one mesh, generating and saving its mesh.</summary>
    static void Part(string name, Transform parent, Material material, System.Func<MeshData> shape)
    {
        var mesh = SaveMesh($"{parent.name.TrimEnd('L', 'R')}_{name}", shape());

        var part = new GameObject(name);
        part.transform.SetParent(parent, false);
        part.AddComponent<MeshFilter>().sharedMesh = mesh;
        part.AddComponent<MeshRenderer>().sharedMaterial = material;
    }

    static Mesh SaveMesh(string key, MeshData data)
    {
        var path = $"{MeshFolder}/{key}.asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null)
        {
            mesh = new Mesh { name = key };
            AssetDatabase.CreateAsset(mesh, path);
        }

        // Every triangle gets its own corners, so each facet shades flat.
        // A closed mesh facing outwards has positive volume. Negative means it is inside out,
        // so the camera would see its inner faces.
        var volume = 0f;
        for (var i = 0; i < data.Triangles.Count; i += 3)
            volume += Vector3.Dot(data.Vertices[data.Triangles[i]],
                Vector3.Cross(data.Vertices[data.Triangles[i + 1]], data.Vertices[data.Triangles[i + 2]])) / 6f;
        if (volume <= 0f) throw new System.InvalidOperationException($"Wrestler part {key} is inside out (volume {volume:0.0000})");

        var count = data.Triangles.Count;
        var vertices = new Vector3[count];
        var normals = new Vector3[count];
        var triangles = new int[count];
        for (var i = 0; i < count; i += 3)
        {
            var a = data.Vertices[data.Triangles[i]];
            var b = data.Vertices[data.Triangles[i + 1]];
            var c = data.Vertices[data.Triangles[i + 2]];
            var normal = Vector3.Cross(b - a, c - a).normalized;
            vertices[i] = a; vertices[i + 1] = b; vertices[i + 2] = c;
            normals[i] = normals[i + 1] = normals[i + 2] = normal;
            triangles[i] = i; triangles[i + 1] = i + 1; triangles[i + 2] = i + 2;
        }

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
        return mesh;
    }

    class MeshData
    {
        public readonly List<Vector3> Vertices = new();
        public readonly List<int> Triangles = new();

        public void Quad(int a, int b, int c, int d)
        {
            Triangles.AddRange(new[] { a, b, c, a, c, d });
        }
    }

    static Matrix4x4 Rotated(Vector3 position, Vector3 euler) => Matrix4x4.TRS(position, Quaternion.Euler(euler), Vector3.one);

    static MeshData Lathe(int sides, bool capBottom, bool capTop, params Ring[] rings) =>
        Lathe(sides, capBottom, capTop, Matrix4x4.identity, rings);

    /// <summary>Sweeps elliptical rings up the Y axis. Starts half a step round so a flat face points forward.</summary>
    static MeshData Lathe(int sides, bool capBottom, bool capTop, Matrix4x4 transform, params Ring[] rings)
    {
        var data = new MeshData();
        foreach (var ring in rings)
        {
            for (var s = 0; s < sides; s++)
            {
                var angle = (s + 0.5f) / sides * 2f * Mathf.PI;
                var x = Mathf.Sin(angle) * ring.RX;
                var z = Mathf.Cos(angle) * ring.RZ;
                data.Vertices.Add(transform.MultiplyPoint3x4(new Vector3(x, ring.Y + z * ring.Tilt, z + ring.Z)));
            }
        }

        for (var r = 0; r < rings.Length - 1; r++)
        {
            for (var s = 0; s < sides; s++)
            {
                var a = r * sides + s;
                var b = r * sides + (s + 1) % sides;
                data.Quad(a, b, b + sides, a + sides);
            }
        }

        if (capBottom) Cap(data, 0, sides, rings[0], transform, false);
        if (capTop) Cap(data, (rings.Length - 1) * sides, sides, rings[^1], transform, true);

        // The winding above assumes rings run upwards. Limbs list them from the joint downwards,
        // which turns every face inside out, so flip them back.
        if (rings[^1].Y < rings[0].Y)
        {
            for (var i = 0; i < data.Triangles.Count; i += 3)
                (data.Triangles[i + 1], data.Triangles[i + 2]) = (data.Triangles[i + 2], data.Triangles[i + 1]);
        }
        return data;
    }

    static void Cap(MeshData data, int start, int sides, Ring ring, Matrix4x4 transform, bool top)
    {
        var centre = data.Vertices.Count;
        data.Vertices.Add(transform.MultiplyPoint3x4(new Vector3(0f, ring.Y, ring.Z)));
        for (var s = 0; s < sides; s++)
        {
            var a = start + s;
            var b = start + (s + 1) % sides;
            if (top) data.Triangles.AddRange(new[] { centre, a, b });
            else data.Triangles.AddRange(new[] { centre, b, a });
        }
    }

    /// <summary>A box whose top face is shrunk by <paramref name="taper"/>, for wedge shapes.</summary>
    static MeshData Box(Vector3 centre, Vector3 size, Vector3 euler, float taper = 1f)
    {
        var h = size * 0.5f;
        var top = new Vector2(h.x * taper, h.z * taper);
        var transform = Rotated(centre, euler);
        var data = new MeshData();
        Vector3[] corners =
        {
            new(-h.x, -h.y, -h.z), new(h.x, -h.y, -h.z), new(h.x, -h.y, h.z), new(-h.x, -h.y, h.z),
            new(-top.x, h.y, -top.y), new(top.x, h.y, -top.y), new(top.x, h.y, top.y), new(-top.x, h.y, top.y),
        };
        foreach (var corner in corners) data.Vertices.Add(transform.MultiplyPoint3x4(corner));

        data.Quad(0, 1, 2, 3); // bottom
        data.Quad(4, 7, 6, 5); // top
        data.Quad(0, 4, 5, 1); // back
        data.Quad(2, 6, 7, 3); // front
        data.Quad(1, 5, 6, 2); // right
        data.Quad(3, 7, 4, 0); // left
        return data;
    }

    static MeshData Merge(params MeshData[] parts)
    {
        var data = new MeshData();
        foreach (var part in parts)
        {
            var offset = data.Vertices.Count;
            data.Vertices.AddRange(part.Vertices);
            foreach (var index in part.Triangles) data.Triangles.Add(index + offset);
        }
        return data;
    }
}
