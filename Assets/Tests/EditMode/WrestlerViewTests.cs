using NUnit.Framework;
using SumoGame;
using UnityEngine;

/// <summary>Checks the drawn wrestler, on a bare joint rig laid out like the real puppets.</summary>
public class WrestlerViewTests
{
    const float Dt = 0.02f;

    GameObject root;

    [TearDown]
    public void TearDown()
    {
        if (root != null) Object.DestroyImmediate(root);
    }

    [Test]
    public void ACircledWrestlerTurnsOnTheSpot()
    {
        var sim = new SumoSim();
        sim.StartMatch();
        while (sim.Phase != MatchPhase.Fighting) sim.Step(Dt, default, default);

        var view = Rig();
        for (var i = 0; i < 50; i++) Step(sim, view, default);

        var feet = view.transform.position;
        var furthest = 0f;
        for (var t = 0f; t < 5f; t += Dt)
        {
            Step(sim, view, new FighterInput { Lateral = 1f });
            var head = view.Head.position - feet;
            furthest = Mathf.Max(furthest, new Vector2(head.x, head.z).magnitude);
        }

        Assert.Less(Vector3.Distance(feet, view.transform.position), 0.01f, "the feet should stay put");
        // The head sits a little forward of the feet. Leaning out past the toes swung it about 0.29m round them.
        Assert.Less(furthest, 0.18f, "the body should turn over the feet, not swing round them");
    }

    [Test]
    public void AGrabSitsBackUnderASlapThenDrivesIn()
    {
        var sim = new SumoSim();
        sim.StartMatch();
        while (sim.Phase != MatchPhase.Fighting) sim.Step(Dt, default, default);

        var view = Rig();
        for (var i = 0; i < 50; i++) Step(sim, view, default);

        // P1 slaps and P2 grabs into it, as in the Mayor's ducked slap.
        var sat = 0f;
        for (var step = 0; step < 80; step++)
        {
            var p1 = step < 2 ? new FighterInput { Strike = true } : default;
            var p2 = step >= 3 && step < 5 ? new FighterInput { Grab = true } : default;
            sim.Step(Dt, p1, p2);
            view.Sync(sim, 1, Dt);

            var grabber = sim.Fighters[1];
            if (grabber.State != FighterState.GrabStartup || !sim.Fighters[0].IsStriking) continue;
            var drawn = new Vector2(view.transform.position.x, view.transform.position.z);
            sat = Mathf.Max(sat, Vector2.Dot(grabber.Position - drawn, grabber.Facing));
        }

        Assert.AreEqual(FighterState.Pushing, sim.Fighters[1].State, "the grab should still land");
        Assert.Greater(sat, 0.4f, "the grabber should be drawn sitting back while the slap passes");
        var landed = new Vector2(view.transform.position.x, view.transform.position.z);
        Assert.Less(Vector2.Distance(landed, sim.Fighters[1].Position), 0.03f, "once the grab lands the drawn body is back where the sim has it");
    }

    static void Step(SumoSim sim, WrestlerView view, FighterInput p1)
    {
        sim.Step(Dt, p1, default);
        view.Sync(sim, 1, Dt);
    }

    WrestlerView Rig()
    {
        root = new GameObject("Wrestler");
        var view = root.AddComponent<WrestlerView>();
        view.Pelvis = Joint("Pelvis", root.transform, new Vector3(0f, 0.74f, 0f));
        view.Spine = Joint("Spine", view.Pelvis, Vector3.zero);
        view.Head = Joint("Head", view.Spine, new Vector3(0f, 1f, 0.08f));
        view.ArmL = Joint("ArmL", view.Spine, new Vector3(-0.54f, 0.76f, 0f));
        view.ArmR = Joint("ArmR", view.Spine, new Vector3(0.54f, 0.76f, 0f));
        view.ForearmL = Joint("ForearmL", view.ArmL, new Vector3(0f, -0.54f, 0f));
        view.ForearmR = Joint("ForearmR", view.ArmR, new Vector3(0f, -0.54f, 0f));
        view.ThighL = Joint("ThighL", view.Pelvis, new Vector3(-0.3f, 0f, 0f));
        view.ThighR = Joint("ThighR", view.Pelvis, new Vector3(0.3f, 0f, 0f));
        view.ShinL = Joint("ShinL", view.ThighL, new Vector3(0f, -0.38f, 0f));
        view.ShinR = Joint("ShinR", view.ThighR, new Vector3(0f, -0.38f, 0f));
        view.FootL = Joint("FootL", view.ShinL, new Vector3(0f, -0.36f, 0f));
        view.FootR = Joint("FootR", view.ShinR, new Vector3(0f, -0.36f, 0f));
        return view;
    }

    static Transform Joint(string name, Transform parent, Vector3 position)
    {
        var joint = new GameObject(name).transform;
        joint.SetParent(parent, false);
        joint.localPosition = position;
        return joint;
    }
}
