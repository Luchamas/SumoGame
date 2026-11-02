using SumoGame;
using UnityEngine;

/// <summary>Assembles the placeholder wrestler out of Unity primitives. Pivot is at the feet, +Z forward.</summary>
public static class WrestlerPuppetBuilder
{
    public struct Palette
    {
        public Material Skin, Hair, Mawashi, Eye;
    }

    public static WrestlerView Build(string name, Transform parent, Palette palette)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent, false);

        var pelvis = Joint("Pelvis", root.transform, new Vector3(0f, 0.74f, 0f));
        var spine = Joint("Spine", pelvis, Vector3.zero);

        Part(PrimitiveType.Sphere, "Belly", spine, new Vector3(0f, 0.2f, 0.07f), new Vector3(1.08f, 0.98f, 1.02f), palette.Skin);
        Part(PrimitiveType.Sphere, "Chest", spine, new Vector3(0f, 0.62f, 0f), new Vector3(0.92f, 0.74f, 0.74f), palette.Skin);

        Part(PrimitiveType.Cylinder, "MawashiBelt", spine, new Vector3(0f, -0.1f, 0.03f), new Vector3(1.02f, 0.17f, 0.95f), palette.Mawashi);
        Part(PrimitiveType.Cube, "MawashiFront", spine, new Vector3(0f, -0.3f, 0.47f), new Vector3(0.24f, 0.42f, 0.06f), palette.Mawashi);
        Part(PrimitiveType.Sphere, "MawashiKnot", spine, new Vector3(0f, 0f, -0.48f), new Vector3(0.24f, 0.24f, 0.2f), palette.Mawashi);

        var head = Joint("Head", spine, new Vector3(0f, 1f, 0.08f));
        Part(PrimitiveType.Sphere, "Skull", head, Vector3.zero, new Vector3(0.44f, 0.48f, 0.46f), palette.Skin);
        Part(PrimitiveType.Sphere, "HairCap", head, new Vector3(0f, 0.11f, -0.04f), new Vector3(0.47f, 0.34f, 0.46f), palette.Hair);
        Part(PrimitiveType.Capsule, "Topknot", head, new Vector3(0f, 0.3f, 0.04f), new Vector3(0.11f, 0.13f, 0.11f), palette.Hair, new Vector3(80f, 0f, 0f));
        Part(PrimitiveType.Sphere, "EyeL", head, new Vector3(-0.09f, 0.02f, 0.21f), Vector3.one * 0.06f, palette.Eye);
        Part(PrimitiveType.Sphere, "EyeR", head, new Vector3(0.09f, 0.02f, 0.21f), Vector3.one * 0.06f, palette.Eye);

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

    static (Transform, Transform) Arm(string side, Transform spine, float sign, Material skin)
    {
        var shoulder = Joint("Arm" + side, spine, new Vector3(sign * 0.54f, 0.76f, 0f));
        Part(PrimitiveType.Capsule, "Upper", shoulder, new Vector3(0f, -0.26f, 0f), new Vector3(0.26f, 0.32f, 0.26f), skin);
        var forearm = Joint("Forearm" + side, shoulder, new Vector3(0f, -0.54f, 0f));
        Part(PrimitiveType.Capsule, "Lower", forearm, new Vector3(0f, -0.22f, 0f), new Vector3(0.21f, 0.27f, 0.21f), skin);
        Part(PrimitiveType.Sphere, "Hand", forearm, new Vector3(0f, -0.48f, 0f), Vector3.one * 0.23f, skin);
        return (shoulder, forearm);
    }

    static (Transform, Transform, Transform) Leg(string side, Transform pelvis, float sign, Material skin)
    {
        var thigh = Joint("Thigh" + side, pelvis, new Vector3(sign * 0.3f, 0f, 0f));
        Part(PrimitiveType.Capsule, "Thigh", thigh, new Vector3(0f, -0.19f, 0f), new Vector3(0.4f, 0.28f, 0.4f), skin);
        var shin = Joint("Shin" + side, thigh, new Vector3(0f, -0.38f, 0f));
        Part(PrimitiveType.Capsule, "Shin", shin, new Vector3(0f, -0.17f, 0f), new Vector3(0.27f, 0.23f, 0.27f), skin);
        var foot = Joint("Foot" + side, shin, new Vector3(0f, -0.36f, 0f));
        Part(PrimitiveType.Sphere, "Foot", foot, new Vector3(0f, 0f, 0.08f), new Vector3(0.26f, 0.12f, 0.42f), skin);
        return (thigh, shin, foot);
    }

    static Transform Joint(string name, Transform parent, Vector3 position)
    {
        var joint = new GameObject(name).transform;
        joint.SetParent(parent, false);
        joint.localPosition = position;
        return joint;
    }

    static void Part(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale, Material material, Vector3 euler = default)
    {
        var part = GameObject.CreatePrimitive(type);
        part.name = name;
        Object.DestroyImmediate(part.GetComponent<Collider>());
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localEulerAngles = euler;
        part.transform.localScale = scale;
        part.GetComponent<Renderer>().sharedMaterial = material;
    }
}
