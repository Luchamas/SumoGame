using NUnit.Framework;
using SumoGame;
using UnityEngine;

public class SumoSimTests
{
    const float Dt = 0.02f;
    static readonly FighterInput None = default;
    static readonly FighterInput Grab = new FighterInput { Grab = true };
    static readonly FighterInput Strike = new FighterInput { Strike = true };
    static readonly FighterInput WalkRight = new FighterInput { Lateral = 1f };

    SumoSim sim;
    Fighter p1, p2;

    [SetUp]
    public void SetUp()
    {
        sim = new SumoSim();
        p1 = sim.Fighters[0];
        p2 = sim.Fighters[1];
        Run(sim.Rules.IntroTime + Dt, None, None);
        Assert.AreEqual(MatchPhase.Fighting, sim.Phase);
    }

    void Run(float seconds, FighterInput a, FighterInput b)
    {
        for (var t = 0f; t < seconds; t += Dt) sim.Step(Dt, a, b);
    }

    void InRange()
    {
        p1.Position = new Vector2(-0.8f, 0f);
        p2.Position = new Vector2(0.8f, 0f);
    }

    void Lock()
    {
        if (Vector2.Distance(p1.Position, p2.Position) > sim.Rules.GrabRange) InRange();
        sim.Step(Dt, Grab, None);
        Run(sim.Rules.GrabStartup + Dt, None, None);
        Assert.AreEqual(FighterState.Pushing, p1.State);
        Assert.AreEqual(FighterState.Grabbed, p2.State);
    }

    // Presses Grab at a rate for each player while the grab lasts.
    void Mash(float seconds, float p1Rate, float p2Rate)
    {
        for (var t = 0f; t < seconds && p1.IsLocked; t += Dt)
        {
            var a = new FighterInput { Grab = p1Rate > 0f && (int)(t * p1Rate * 2f) % 2 == 0 };
            var b = new FighterInput { Grab = p2Rate > 0f && (int)(t * p2Rate * 2f) % 2 == 0 };
            sim.Step(Dt, a, b);
        }
    }

    [Test]
    public void NothingHappensDuringTheIntro()
    {
        sim = new SumoSim();
        Run(sim.Rules.IntroTime - 0.1f, new FighterInput { Lateral = 1f, Grab = true }, None);
        Assert.AreEqual(MatchPhase.Intro, sim.Phase);
        Assert.AreEqual(-sim.Rules.StartDistance * 0.5f, sim.Fighters[0].Position.x, 0.001f);
    }

    // Movement

    [Test]
    public void WrestlersWalkInToFightingDistanceByThemselves()
    {
        p1.Position = new Vector2(-1.5f, 0f);
        p2.Position = new Vector2(1.5f, 0f);
        Run(2f, None, None);
        Assert.AreEqual(sim.Rules.EngageDistance, Vector2.Distance(p1.Position, p2.Position), 0.05f);
        Assert.AreEqual(FighterState.Idle, p1.State);
        Assert.AreEqual(FighterState.Idle, p2.State);
    }

    [Test]
    public void GroundLostToAPushStaysLost()
    {
        p1.Position = new Vector2(0.5f, 0f);
        p2.Position = new Vector2(3f, 0f);
        Run(1.5f, None, None);
        Assert.AreEqual(3f, p2.Position.x, 0.01f, "the wrestler by the rope should not walk back in");
        Assert.AreEqual(sim.Rules.EngageDistance, Vector2.Distance(p1.Position, p2.Position), 0.05f);
    }

    [Test]
    public void HoldingADirectionCirclesTheOpponent()
    {
        Run(2f, None, None);
        var distance = Vector2.Distance(p1.Position, p2.Position);
        var start = p1.Position;
        Run(0.6f, new FighterInput { Lateral = 1f }, None);
        Assert.AreEqual(FighterState.Moving, p1.State);
        Assert.Greater(Vector2.Distance(p1.Position, start), 0.5f);
        Assert.AreEqual(distance, Vector2.Distance(p1.Position, p2.Position), 0.2f);
    }

    [Test]
    public void CirclingLeavesTheOpponentTurningOnTheSpot()
    {
        Run(2f, None, None);
        var distance = Vector2.Distance(p1.Position, p2.Position);
        var held = p2.Position;
        for (var t = 0f; t < 3f; t += Dt)
        {
            sim.Step(Dt, new FighterInput { Lateral = 1f }, None);
            Assert.AreEqual(distance, Vector2.Distance(p1.Position, p2.Position), 0.02f, $"the gap changed at {t:0.00}s");
            Assert.AreNotEqual(FighterState.Moving, p2.State, $"the opponent walked at {t:0.00}s");
        }
        Assert.Less(Vector2.Distance(held, p2.Position), 0.01f, "the opponent should only turn");
        Assert.AreEqual(1f, Vector2.Dot(p2.Facing, (p1.Position - p2.Position).normalized), 0.01f);
    }

    [Test]
    public void TheSideKeysMoveLeftAndRightOfTheWrestler()
    {
        Run(2f, None, None);
        var y = p1.Position.y;
        Run(0.3f, new FighterInput { Lateral = 1f }, None);
        Assert.Less(p1.Position.y, y, "facing +x, right is -y");
    }

    [Test]
    public void WalkingInDoesNotFlickerBetweenStates()
    {
        Run(2f, None, None);
        var changes = 0;
        var last = p1.State;
        for (var t = 0f; t < 1.5f; t += Dt)
        {
            var wobble = Mathf.Sin(t * 40f) * 0.04f;
            p2.Position += new Vector2(wobble, 0f);
            sim.Step(Dt, None, None);
            if (p1.State != last) changes++;
            last = p1.State;
        }
        Assert.LessOrEqual(changes, 2);
    }

    [Test]
    public void WalkingCannotLeaveTheRing()
    {
        p1.Position = new Vector2(0f, sim.Rules.RingRadius - 0.3f);
        p2.Position = new Vector2(0f, sim.Rules.RingRadius - 1.8f);
        Run(3f, new FighterInput { Lateral = 1f }, None);
        Run(3f, new FighterInput { Lateral = -1f }, None);
        Assert.AreEqual(MatchPhase.Fighting, sim.Phase);
    }

    [Test]
    public void FightersKeepFacingEachOther()
    {
        Run(1f, new FighterInput { Lateral = 1f }, None);
        Assert.AreEqual(1f, Vector2.Dot(p1.Facing, (p2.Position - p1.Position).normalized), 0.01f);
    }

    [Test]
    public void MovementIsRestrictedDuringGrabsAndStuns()
    {
        Lock();
        var held = p2.Position;
        Run(0.2f, None, new FighterInput { Lateral = 1f });
        Assert.AreEqual(held, p2.Position);
    }

    // Grabbing

    [Test]
    public void TheAttackerCannotGrabAgainStraightAfterAGrab()
    {
        Lock();
        Run(sim.Rules.MaxHold + 0.1f, None, None);
        Assert.False(p1.IsLocked);
        Run(sim.Rules.RecoilTime, None, None);
        sim.Step(Dt, Grab, None);
        Assert.AreNotEqual(FighterState.GrabStartup, p1.State, "still on cooldown");

        Run(sim.Rules.RegrabDelay, None, None);
        sim.Step(Dt, Grab, None);
        Assert.AreEqual(FighterState.GrabStartup, p1.State);
    }

    [Test]
    public void TheDefenderCanGrabBackStraightAfterAGrab()
    {
        Lock();
        Run(sim.Rules.MaxHold + sim.Rules.RecoilTime + 0.1f, None, None);
        Assert.Greater(p1.RegrabCooldown, 0f);
        InRange();
        sim.Step(Dt, None, Grab);
        Assert.AreEqual(FighterState.GrabStartup, p2.State);
    }

    [Test]
    public void AMissedGrabAlsoStartsTheCooldown()
    {
        InRange();
        sim.Step(Dt, Grab, WalkRight);
        Run(sim.Rules.GrabStartup + Dt, None, WalkRight);
        Assert.Greater(p1.RegrabCooldown, 0f);
    }

    [Test]
    public void GrabOutOfRangeDoesNothing()
    {
        p1.Position = new Vector2(-1.5f, 0f);
        p2.Position = new Vector2(1.5f, 0f);
        sim.Step(Dt, Grab, None);
        Assert.AreNotEqual(FighterState.GrabStartup, p1.State);
    }

    [Test]
    public void GrabStartupTakesAQuarterOfASecond()
    {
        InRange();
        sim.Step(Dt, Grab, None);
        Assert.AreEqual(FighterState.GrabStartup, p1.State);
        Run(0.2f, None, None);
        Assert.AreEqual(FighterState.GrabStartup, p1.State);
        Run(0.08f, None, None);
        Assert.AreEqual(FighterState.Pushing, p1.State);
    }

    [Test]
    public void TheGrabThrowsBothApartAtTheTimeLimitIfNobodyWins()
    {
        Lock();
        Mash(4f, 4f, 4f);
        Assert.False(p1.IsLocked);
        Assert.AreEqual(FighterState.Recoiling, p1.State);
        Assert.AreEqual(FighterState.Recoiling, p2.State);
        Assert.AreEqual(sim.Rules.MaxHold, sim.HoldTime, 0.05f);

        var gap = Vector2.Distance(p1.Position, p2.Position);
        Run(0.4f, None, None);
        Assert.Greater(Vector2.Distance(p1.Position, p2.Position), gap + 0.5f);
        Run(sim.Rules.RecoilTime, None, None);
        Assert.AreNotEqual(FighterState.Recoiling, p1.State);
    }

    [Test]
    public void TheGrabAlsoBreaksIfNobodyPressesAnything()
    {
        Lock();
        Run(sim.Rules.MaxHold + 0.1f, None, None);
        Assert.False(p1.IsLocked);
    }

    [Test]
    public void AttackerReachingFullStruggleShovesAndStunsTheDefender()
    {
        Lock();
        var before = p2.Position.x;
        Mash(3.2f, 5f, 0f);
        Assert.AreEqual(FighterState.Reeling, p2.State);
        Assert.AreEqual(FighterState.Driving, p1.State);
        Run(sim.Rules.DriveTime, None, None);
        Assert.AreEqual(FighterState.Stunned, p2.State);
        Assert.Greater(p2.Position.x, before + 1.5f);
        Assert.Less(sim.HoldTime, sim.Rules.MaxHold);
    }

    [Test]
    public void AnUnresistedGrabFromTheCentreLeavesTheDefenderNearTheEdgeButInside()
    {
        p1.Position = new Vector2(-1.1f, 0f);
        p2.Position = new Vector2(0.4f, 0f);
        Lock();
        Mash(3.2f, 5f, 0f);
        Run(sim.Rules.DriveTime + 0.3f, None, None);
        Assert.AreEqual(MatchPhase.Fighting, sim.Phase);
        Assert.Greater(p2.Position.magnitude, sim.Rules.RingRadius - 1.5f);
        Assert.Less(p2.Position.magnitude, sim.Rules.RingRadius);
    }

    [Test]
    public void DefenderDrainingTheStruggleEscapesAndTheAttackerRecovers()
    {
        Lock();
        Mash(2.4f, 0f, 5f);
        Assert.AreEqual(FighterState.Recovering, p1.State);
        Assert.AreEqual(sim.Rules.EscapeRecovery, p1.RecoveryTime);
        Assert.False(p2.IsLocked);
    }

    [Test]
    public void AfterAnEscapeTheDefenderActsAtOnceAndTheAttackerCannot()
    {
        Lock();
        Mash(2.4f, 0f, 5f);
        Assert.AreEqual(FighterState.Idle, p2.State);

        sim.Step(Dt, new FighterInput { Strike = true }, new FighterInput { Strike = true });
        Assert.AreEqual(FighterState.Striking, p2.State);
        Assert.AreEqual(FighterState.Recovering, p1.State);

        sim.Step(Dt, new FighterInput { Grab = true }, None);
        Assert.AreEqual(FighterState.Recovering, p1.State);
    }

    [Test]
    public void DefenderCannotEscapeBeforeTheMinimumHold()
    {
        Lock();
        Mash(0.45f, 0f, 20f);
        Assert.True(p2.IsLocked);
    }

    [Test]
    public void TheFinishingDriveCanCarryThemOverTheRope()
    {
        p1.Position = new Vector2(1.6f, 0f);
        p2.Position = new Vector2(3.1f, 0f);
        Lock();
        Mash(3.2f, 5f, 0f);
        Run(sim.Rules.DriveTime, None, None);
        Assert.AreEqual(MatchPhase.RoundOver, sim.Phase);
        Assert.AreEqual(0, sim.RoundWinner);
    }

    [Test]
    public void NobodyCanActDuringTheFinishingDrive()
    {
        Lock();
        Mash(3.2f, 5f, 0f);
        Assert.AreEqual(FighterState.Reeling, p2.State);
        sim.Step(Dt, new FighterInput { Strike = true, Grab = true }, new FighterInput { Strike = true, Grab = true, Lateral = 1f });
        Assert.AreEqual(FighterState.Reeling, p2.State);
        Assert.AreEqual(FighterState.Driving, p1.State);
    }

    [Test]
    public void ImpactsAreReportedForEffects()
    {
        var seen = new System.Collections.Generic.List<SumoImpact>();
        sim.Impact += (impact, where) => seen.Add(impact);

        InRange();
        Run(sim.Rules.StrikeStartup + Dt * 2f, Strike, None);
        Assert.Contains(SumoImpact.Strike, seen);

        Run(1.5f, None, None);
        Lock();
        Assert.Contains(SumoImpact.Grab, seen);
        Mash(3.2f, 5f, 0f);
        Assert.Contains(SumoImpact.Shove, seen);
        Assert.Contains(SumoImpact.Drive, seen);
    }

    [Test]
    public void EverySoundIsRealAudio()
    {
        foreach (SumoImpact impact in System.Enum.GetValues(typeof(SumoImpact)))
        {
            var samples = SumoAudio.Synthesize(impact);
            Assert.Greater(samples.Length, 1000, impact.ToString());
            var peak = 0f;
            foreach (var sample in samples) peak = Mathf.Max(peak, Mathf.Abs(sample));
            Assert.Greater(peak, 0.1f, $"{impact} is silent");
            Assert.LessOrEqual(peak, 1f, $"{impact} clips");
            Assert.AreEqual(0f, samples[samples.Length - 1], 0.02f, $"{impact} should fade out, not click");
        }
    }

    [Test]
    public void MatchStartDrumShipsWithTheGame()
    {
        var drum = Resources.Load<AudioClip>("Audio/MatchStartDrum");
        Assert.IsNotNull(drum, "Assets/Resources/Audio/MatchStartDrum.ogg is missing");
        Assert.That(drum.length, Is.InRange(1f, 2f));
    }

    [Test]
    public void PushesCarryFurtherInALongRound()
    {
        Lock();
        var start = p2.Position.x;
        sim.Step(Dt, Grab, None);
        Run(sim.Rules.ShoveTime, None, None);
        var normal = p2.Position.x - start;

        sim = new SumoSim();
        p1 = sim.Fighters[0];
        p2 = sim.Fighters[1];
        while (sim.Phase != MatchPhase.Fighting || sim.PhaseTime < sim.Rules.FatigueStart + 10f)
        {
            sim.Step(Dt, None, None);
        }
        Lock();
        start = p2.Position.x;
        sim.Step(Dt, Grab, None);
        Run(sim.Rules.ShoveTime, None, None);
        Assert.Greater(p2.Position.x - start, normal * 1.4f);
    }

    [Test]
    public void AttackerSpamDoesNothingDuringTheCooldown()
    {
        Lock();
        Mash(sim.Rules.PushCooldown - 0.1f, 25f, 0f);
        Assert.AreEqual(0.5f + sim.Rules.ShoveStep, sim.Struggle, 0.001f);
    }

    [Test]
    public void SpammingCannotBeatTheCooldown()
    {
        Lock();
        Mash(1.6f, 12.5f, 0f);
        var shoves = Mathf.Floor(1.6f / sim.Rules.PushCooldown) + 1f;
        Assert.LessOrEqual(sim.Struggle, 0.5f + shoves * sim.Rules.ShoveStep + 0.001f);
    }

    [Test]
    public void TheNextShoveWorksOnceTheCooldownEnds()
    {
        Lock();
        sim.Step(Dt, Grab, None);
        Run(sim.Rules.PushCooldown + Dt, None, None);
        sim.Step(Dt, Grab, None);
        Assert.AreEqual(0.5f + sim.Rules.ShoveStep * 2f, sim.Struggle, 0.001f);
    }

    [Test]
    public void ADefenderMashingAgainstASpammerSurvivesTheHold()
    {
        Lock();
        Mash(4f, 12.5f, 4f);
        Assert.False(p1.IsLocked);
        Assert.AreNotEqual(FighterState.Stunned, p2.State, "resisting should stop the win");
    }

    [Test]
    public void ExtraResistPressesAreQueuedNotLost()
    {
        Lock();
        Mash(0.2f, 0f, 25f);
        Assert.AreEqual(0.5f - sim.Rules.ResistStep, sim.Struggle, 0.001f);
        Run(0.1f, None, None);
        Assert.AreEqual(0.5f - sim.Rules.ResistStep * 2f, sim.Struggle, 0.001f);
    }

    [Test]
    public void ResistingSlowsThePushButNeverReversesIt()
    {
        Lock();
        var start = p1.Position.x;
        Mash(1f, 4f, 4f);
        var resisted = p1.Position.x - start;
        Assert.Greater(resisted, 0f);
        Assert.Less(resisted, sim.Rules.PushSpeed * sim.Rules.ShoveTime * 2f * sim.Rules.ResistedPush + 0.05f);
    }

    [Test]
    public void AttackerCanOnlySteerAPushSlightly()
    {
        Lock();
        for (var t = 0f; t < 1f; t += Dt)
        {
            var press = (int)(t * 8f) % 2 == 0;
            sim.Step(Dt, new FighterInput { Grab = press, Lateral = -1f }, None);
        }
        var turned = Vector2.Angle(Vector2.right, p1.Facing);
        Assert.Greater(turned, 5f);
        Assert.LessOrEqual(turned, sim.Rules.PushSteering + 1f);
    }

    [Test]
    public void TheDefenderCannotActWhileHeld()
    {
        Lock();
        var before = p2.State;
        sim.Step(Dt, None, new FighterInput { Strike = true, Lateral = 1f });
        Assert.AreEqual(before, p2.State);
    }

    // Rock paper scissors

    [Test]
    public void ThereIsNoSidestepMove()
    {
        Run(2f, None, None);
        var start = p1.Position;
        Run(0.06f, WalkRight, None);
        sim.Step(Dt, None, None);
        sim.Step(Dt, None, None);
        Assert.AreEqual(FighterState.Idle, p1.State);
        Assert.Less(Vector2.Distance(p1.Position, start), 0.3f, "a tap is only a short walk");
    }

    [Test]
    public void WalkingSidewaysBeatsGrab()
    {
        InRange();
        sim.Step(Dt, Grab, WalkRight);
        Assert.AreEqual(FighterState.GrabStartup, p1.State);
        Run(sim.Rules.GrabStartup + Dt, None, WalkRight);
        Assert.AreEqual(FighterState.Recovering, p1.State);
        Assert.False(p2.IsLocked);
    }

    [Test]
    public void AMissedGrabLeavesTimeToPunishIt()
    {
        InRange();
        sim.Step(Dt, Grab, WalkRight);
        Run(sim.Rules.GrabStartup + Dt, None, WalkRight);
        Assert.AreEqual(FighterState.Recovering, p1.State);
        Assert.Greater(p1.RecoveryTime - p1.StateTime, sim.Rules.StrikeStartup + 0.1f);
    }

    void WhiffPastP2()
    {
        InRange();
        sim.Step(Dt, Grab, WalkRight);
        Run(sim.Rules.GrabStartup + Dt, None, WalkRight);
        Assert.AreEqual(FighterState.Recovering, p1.State);
    }

    [Test]
    public void AMissedGrabKeepsFacingTheWayItLunged()
    {
        WhiffPastP2();
        var lunge = p1.LungeDirection;
        Run(sim.Rules.WhiffRecovery - 0.1f, None, WalkRight);
        Assert.AreEqual(FighterState.Recovering, p1.State);
        Assert.AreEqual(0f, Vector2.Angle(lunge, p1.Facing), 0.01f);
    }

    [Test]
    public void AMissedGrabStumblesOnPastTheTarget()
    {
        WhiffPastP2();
        var x = p1.Position.x;
        Run(0.5f, None, None);
        Assert.Greater(p1.Position.x, x + 0.3f);
    }

    [Test]
    public void ASidesteppedGrabVeersAwayFromTheSidestepper()
    {
        float GapAfterMiss(float veer)
        {
            SetUp();
            sim.Rules.WhiffVeer = veer;
            WhiffPastP2();
            Run(0.6f, None, None);
            return Vector2.Distance(p1.Position, p2.Position);
        }

        var straight = GapAfterMiss(0f);
        var veered = GapAfterMiss(new SumoRules().WhiffVeer);
        Assert.Greater(veered, straight + 0.25f, $"straight {straight:0.00}m, veered {veered:0.00}m");
    }

    [Test]
    public void AfterAMissTheAttackerTurnsBackRatherThanSnapping()
    {
        WhiffPastP2();
        Run(0.5f, None, None);
        while (p1.State == FighterState.Recovering) sim.Step(Dt, None, None);

        var before = p1.Facing;
        sim.Step(Dt, None, None);
        Assert.LessOrEqual(Vector2.Angle(before, p1.Facing), sim.Rules.TurnSpeed * Dt + 0.5f);
        Run(0.6f, None, None);
        Assert.AreEqual(1f, Vector2.Dot(p1.Facing, (p2.Position - p1.Position).normalized), 0.02f);
    }

    [Test]
    public void StumblingAfterAMissCannotCrossTheRope()
    {
        p1.Position = new Vector2(sim.Rules.RingRadius - 1.4f, 0f);
        p2.Position = new Vector2(sim.Rules.RingRadius - 0.2f, 0f);
        sim.Step(Dt, Grab, WalkRight);
        Run(1f, None, WalkRight);
        Assert.AreEqual(MatchPhase.Fighting, sim.Phase);
        Assert.Greater(p1.Position.magnitude, 3f);
    }

    [Test]
    public void GrabbingSomeoneByTheRopeDoesNotThrowThemOverIt()
    {
        p1.Position = new Vector2(sim.Rules.RingRadius - 2f, 0f);
        p2.Position = new Vector2(sim.Rules.RingRadius - 0.1f, 0f);
        var before = p2.Position;
        sim.Step(Dt, Grab, None);
        Run(sim.Rules.GrabStartup + Dt, None, None);
        Assert.AreEqual(FighterState.Grabbed, p2.State);
        Assert.AreEqual(before, p2.Position);
        Assert.AreEqual(MatchPhase.Fighting, sim.Phase);
    }

    [Test]
    public void AGrabCatchesAnyoneStandingStill()
    {
        InRange();
        sim.Step(Dt, Grab, None);
        Run(sim.Rules.GrabStartup + Dt, None, None);
        Assert.AreEqual(FighterState.Grabbed, p2.State);
    }

    [Test]
    public void StrikeBeatsWalkingSideways()
    {
        InRange();
        for (var t = 0f; t < sim.Rules.StrikeStartup + Dt * 3f; t += Dt)
        {
            sim.Step(Dt, new FighterInput { Strike = t < Dt }, WalkRight);
        }
        Assert.AreEqual(FighterState.Stunned, p2.State);
    }

    [Test]
    public void StrikeReachesASidewalkerAtLongerRange()
    {
        p1.Position = new Vector2(-1.3f, 0f);
        p2.Position = new Vector2(1.3f, 0f);
        for (var t = 0f; t < sim.Rules.StrikeStartup + Dt * 3f; t += Dt)
        {
            sim.Step(Dt, new FighterInput { Strike = t < Dt }, WalkRight);
        }
        Assert.AreEqual(FighterState.Stunned, p2.State);
    }

    [Test]
    public void GrabBeatsStrike()
    {
        InRange();
        sim.Step(Dt, Grab, None);
        sim.Step(Dt, None, Strike);
        Assert.AreEqual(FighterState.Striking, p2.State);
        Run(1.1f, None, None);
        Assert.AreEqual(FighterState.Pushing, p1.State);
        Assert.AreEqual(FighterState.Grabbed, p2.State);
    }

    // Strikes

    [Test]
    public void StrikeStunsForTheSpecDurationThenAllowsAFollowUpGrab()
    {
        InRange();
        Run(sim.Rules.StrikeStartup + Dt * 2f, Strike, None);
        Assert.AreEqual(FighterState.Stunned, p2.State);
        while (p1.State == FighterState.Striking) sim.Step(Dt, None, None);
        Assert.AreEqual(FighterState.Stunned, p2.State, "attacker should recover first");

        sim.Step(Dt, Grab, None);
        Run(sim.Rules.GrabStartup + Dt, None, None);
        Assert.AreEqual(FighterState.Pushing, p1.State);
    }

    [Test]
    public void StrikesCannotStunLock()
    {
        InRange();
        Run(sim.Rules.StrikeStartup + Dt * 2f, Strike, None);
        Assert.AreEqual(FighterState.Stunned, p2.State);
        for (var t = 0f; t < sim.Rules.StunTime + 0.1f; t += Dt) sim.Step(Dt, None, None);
        Assert.AreEqual(FighterState.Idle, p2.State);

        p1.Position = new Vector2(p2.Position.x - 1f, p2.Position.y);
        Run(sim.Rules.StrikeStartup + Dt * 2f, Strike, None);
        Assert.AreNotEqual(FighterState.Stunned, p2.State);
    }

    void StunP2AndRecover()
    {
        InRange();
        Run(sim.Rules.StrikeStartup + Dt * 2f, Strike, None);
        Assert.AreEqual(FighterState.Stunned, p2.State);
        Assert.AreEqual(0f, p2.StunImmunity, "immunity should wait for the stun to end");
        while (p2.State == FighterState.Stunned) sim.Step(Dt, None, None);
        while (p1.State != FighterState.Idle && p1.State != FighterState.Moving) sim.Step(Dt, None, None);
    }

    [Test]
    public void ImmunityStartsWhenTheStunEnds()
    {
        InRange();
        Run(sim.Rules.StrikeStartup + Dt * 2f, Strike, None);
        Assert.AreEqual(0f, p2.StunImmunity);
        while (p2.State == FighterState.Stunned) sim.Step(Dt, None, None);
        Assert.AreEqual(sim.Rules.StunImmunity, p2.StunImmunity, 0.001f);
    }

    [Test]
    public void AnImmuneOpponentIsStillHitAndPushedButNotStunned()
    {
        StunP2AndRecover();
        p1.Position = new Vector2(p2.Position.x - 1f, p2.Position.y);
        var before = p2.Position.x;
        Run(sim.Rules.StrikeStartup + Dt * 8f, Strike, None);
        Assert.AreNotEqual(FighterState.Stunned, p2.State);
        Assert.Greater(p2.Position.x, before);
    }

    [Test]
    public void ImmunityWearsOffSoAStrikeCanStunAgain()
    {
        StunP2AndRecover();
        Run(sim.Rules.StunImmunity + Dt, None, None);
        Assert.AreEqual(0f, p2.StunImmunity);
        p1.Position = new Vector2(p2.Position.x - 1f, p2.Position.y);
        Run(sim.Rules.StrikeStartup + Dt * 2f, Strike, None);
        Assert.AreEqual(FighterState.Stunned, p2.State);
    }

    [Test]
    public void SimultaneousStrikesStunBothPlayers()
    {
        InRange();
        Run(0.25f, Strike, Strike);
        Assert.AreEqual(FighterState.Stunned, p1.State);
        Assert.AreEqual(FighterState.Stunned, p2.State);
    }

    [Test]
    public void SimultaneousGrabsThrowBothApart()
    {
        InRange();
        Run(sim.Rules.GrabStartup + 0.05f, Grab, Grab);
        Assert.AreEqual(FighterState.Recoiling, p1.State);
        Assert.AreEqual(FighterState.Recoiling, p2.State);
        var gap = Vector2.Distance(p1.Position, p2.Position);
        Run(0.3f, None, None);
        Assert.Greater(Vector2.Distance(p1.Position, p2.Position), gap + 0.3f);
    }

    [Test]
    public void GrabsThatOverlapAreStillADraw()
    {
        InRange();
        sim.Step(Dt, Grab, None);
        Run(0.15f, None, None);
        sim.Step(Dt, None, Grab);
        Assert.AreEqual(FighterState.GrabStartup, p2.State);
        Run(0.15f, None, None);
        Assert.AreEqual(FighterState.Recoiling, p1.State);
        Assert.False(p2.IsLocked);
    }

    [Test]
    public void AStrikeSwingsOverAGrabAndTheGrabLandsAfterIt()
    {
        var seen = new System.Collections.Generic.List<SumoImpact>();
        sim.Impact += (impact, where) => seen.Add(impact);

        InRange();
        sim.Step(Dt, Grab, None);
        sim.Step(Dt, None, Strike);
        Run(sim.Rules.StrikeStartup + Dt * 2f, None, None);
        Assert.Contains(SumoImpact.Ducked, seen);
        Assert.AreEqual(FighterState.GrabStartup, p1.State, "still low while the swing passes");
        Assert.AreEqual(FighterState.Striking, p2.State);

        Run(sim.Rules.StrikeActive + sim.Rules.StrikeRecovery, None, None);
        Assert.AreEqual(FighterState.GrabStartup, p1.State, "the striker is still overextended");
        Run(sim.Rules.DuckedStrikeLag + Dt, None, None);
        Assert.AreEqual(FighterState.Pushing, p1.State);
        Assert.AreEqual(FighterState.Grabbed, p2.State);
    }

    // Matches

    void RingOutP2()
    {
        p2.Position = new Vector2(sim.Rules.RingRadius + 0.1f, 0f);
        sim.Step(Dt, None, None);
    }

    [Test]
    public void CrossingTheBoundaryEndsTheRound()
    {
        p2.Position = new Vector2(sim.Rules.RingRadius - 0.05f, 0f);
        sim.Step(Dt, None, None);
        Assert.AreEqual(MatchPhase.Fighting, sim.Phase);
        RingOutP2();
        Assert.AreEqual(MatchPhase.RoundOver, sim.Phase);
        Assert.AreEqual(FighterState.Falling, p2.State);
        Assert.AreEqual(FighterState.Victory, p1.State);
    }

    [Test]
    public void BestOfThreeThenTheMatchEnds()
    {
        RingOutP2();
        sim.SkipRoundDelay();
        sim.Step(Dt, None, None);
        Assert.AreEqual(MatchPhase.Countdown, sim.Phase);
        Assert.AreEqual(-sim.Rules.StartDistance * 0.5f, p1.Position.x, 0.001f);
        Run(sim.Rules.CountdownTime + Dt, None, None);

        RingOutP2();
        Run(sim.Rules.RoundOverTime + Dt, None, None);
        Assert.AreEqual(MatchPhase.MatchOver, sim.Phase);
        Assert.AreEqual(0, sim.MatchWinner);

        sim.StartMatch();
        Assert.AreEqual(0, p1.Score);
        Assert.AreEqual(MatchPhase.Intro, sim.Phase);
    }

    // Bots and runs

    static int BotWins(int rivalWins, int benchmarkWins, int fights, int salt = 1, bool swapSeeds = false)
    {
        // Spread the seeds out: System.Random streams from neighbouring seeds are correlated.
        var seeds = new System.Random(salt);
        var wins = 0;
        var p1Wins = 0;
        var unfinished = 0;
        for (var seed = 0; seed < fights; seed++)
        {
            var fight = new SumoSim(new SumoRules { RoundsToWin = 1 });
            var first = seeds.Next();
            var second = seeds.Next();
            var rival = new SumoBot(BotProfile.ForWins(rivalWins), swapSeeds ? second : first);
            var benchmark = new SumoBot(BotProfile.ForWins(benchmarkWins), swapSeeds ? first : second);

            for (var t = 0f; t < 300f && fight.Phase != MatchPhase.MatchOver; t += Dt)
            {
                fight.Step(Dt, benchmark.Think(fight, 0, Dt), rival.Think(fight, 1, Dt));
            }
            if (fight.MatchWinner == 1) wins++;
            else if (fight.MatchWinner == 0) p1Wins++;
            else unfinished++;
        }
        if (unfinished > 0) Debug.Log($"BALANCE tally rivals {rivalWins} v {benchmarkWins}: p2={wins} p1={p1Wins} unfinished={unfinished}");
        return wins;
    }

    static int scriptUnfinished;
    static float scriptAverage;

    [Test]
    public void EveryRivalsMatchesFinishInAReasonableTime()
    {
        for (var rank = 0; rank < BotProfile.RivalCount; rank += 3)
        {
            ScriptWins(rank, false, 40);
            Assert.LessOrEqual(scriptUnfinished, 4, $"rank {rank} matches should finish even against pure spam");
            Assert.Less(scriptAverage, 180f, $"rank {rank} matches should not drag on");
        }
    }

    static bool InStrikeReach(SumoSim fight) =>
        Vector2.Distance(fight.Fighters[0].Position, fight.Fighters[1].Position) <= fight.Rules.StrikeReach;

    // The strike-only tactic: strafe past grabs, punish openings with strikes, resist when held.
    static FighterInput StrafeAndStrike(SumoSim fight, bool press)
    {
        var rival = fight.Fighters[1];
        if (fight.Fighters[0].IsLocked) return new FighterInput { Grab = press };
        if (rival.State == FighterState.GrabStartup) return new FighterInput { Lateral = 1f };

        var opening = rival.State == FighterState.Recovering || rival.State == FighterState.Stunned;
        return opening ? new FighterInput { Strike = press && InStrikeReach(fight) } : new FighterInput { Lateral = 1f };
    }

    // Plays a best-of-three against a rival, pressing one button over and over.
    static int ScriptWins(int rivalWins, bool strikes, int fights)
    {
        var seeds = new System.Random(7);
        var wins = 0;
        var unfinished = 0;
        var drives = 0;
        var total = 0f;
        var longest = 0f;
        for (var seed = 0; seed < fights; seed++)
        {
            var fight = new SumoSim();
            fight.Impact += (impact, where) => { if (impact == SumoImpact.Drive) drives++; };
            var rival = new SumoBot(BotProfile.ForWins(rivalWins), seeds.Next());
            var t = 0f;
            for (; t < 300f && fight.Phase != MatchPhase.MatchOver; t += Dt)
            {
                var press = (int)(t / 0.13f) % 2 == 0;
                var script = strikes ? StrafeAndStrike(fight, press) : new FighterInput { Grab = press };
                fight.Step(Dt, script, rival.Think(fight, 1, Dt));
            }
            if (fight.MatchWinner == 0) wins++;
            else if (fight.MatchWinner < 0) unfinished++;
            longest = Mathf.Max(longest, t);
            total += t;
        }
        Debug.Log($"BALANCE script strikes={strikes} rank {rivalWins}: won={wins} unfinished={unfinished} avgMatch={total / fights:F0}s longest={longest:F0}s drives={drives / (float)fights:F1}/match");
        scriptUnfinished = unfinished;
        scriptAverage = total / fights;
        return wins;
    }

    [Test]
    public void SpammingGrabOnlyWorksOnTheFirstRivals()
    {
        var wins = new int[10];
        for (var rank = 0; rank < wins.Length; rank++) wins[rank] = ScriptWins(rank, false, 100);
        Debug.Log("BALANCE grab spam best-of-3 of 100: " + string.Join(" ", wins));
        Assert.Greater(wins[0], 50, "the first rival should still teach the grab");
        Assert.Less(wins[2], 75);
        Assert.Less(wins[3], 30);
        Assert.Less(wins[5], 10);
        Assert.LessOrEqual(wins[9], 2);
    }

    [Test]
    public void ARoundCanBeWonWithStrikesAlone()
    {
        var fight = new SumoSim(new SumoRules { RoundsToWin = 1 });
        var time = 0f;
        for (; time < 120f && fight.Phase != MatchPhase.MatchOver; time += Dt)
        {
            var press = (int)(time / 0.13f) % 2 == 0;
            fight.Step(Dt, new FighterInput { Strike = press && InStrikeReach(fight) }, None);
        }
        Debug.Log($"BALANCE strikes alone vs passive took {time:F1}s winner={fight.MatchWinner}");
        Debug.Log($"BALANCE strikes only best-of-3 of 100: r0={ScriptWins(0, true, 100)} r2={ScriptWins(2, true, 100)} r5={ScriptWins(5, true, 100)}");
        Assert.AreEqual(0, fight.MatchWinner);
    }

    [Test]
    public void RivalsNeverActOnTheFirstInstantOfARound()
    {
        var fight = new SumoSim();
        var rival = new SumoBot(BotProfile.ForWins(9), 3);
        while (fight.Phase != MatchPhase.Fighting) fight.Step(Dt, None, rival.Think(fight, 1, Dt));

        for (var t = 0f; t < BotProfile.ForWins(9).ThinkTime * 0.6f; t += Dt)
        {
            var input = rival.Think(fight, 1, Dt);
            Assert.False(input.Grab || input.Strike, "acted before the player could react");
            fight.Step(Dt, None, input);
        }
    }

    [Test]
    public void BotsFinishTheirFights()
    {
        var fight = new SumoSim(new SumoRules { RoundsToWin = 1 });
        var a = new SumoBot(BotProfile.ForWins(5), 1);
        var b = new SumoBot(BotProfile.ForWins(5), 2);
        var longest = 0f;
        for (var t = 0f; t < 300f && fight.Phase != MatchPhase.MatchOver; t += Dt)
        {
            if (fight.Fighters[0].IsLocked) longest = Mathf.Max(longest, fight.HoldTime);
            fight.Step(Dt, a.Think(fight, 0, Dt), b.Think(fight, 1, Dt));
        }
        Assert.AreEqual(MatchPhase.MatchOver, fight.Phase);
        Assert.LessOrEqual(longest, fight.Rules.MaxHold + Dt);
    }

    [Test]
    public void OpponentsGetHarderWithEveryFewWins()
    {
        const int benchmark = 5;
        var wins = new int[11];
        for (var rank = 0; rank <= 10; rank += 2) wins[rank] = BotWins(rank, benchmark, 100);
        Debug.Log($"BALANCE out of 100 vs mid bot: r0={wins[0]} r2={wins[2]} r4={wins[4]} r6={wins[6]} r8={wins[8]} r10={wins[10]}");
        for (var rank = 2; rank <= 10; rank += 2)
        {
            Assert.Greater(wins[rank], wins[rank - 2] - 12, $"rank {rank} should not be clearly weaker than rank {rank - 2}");
        }
        Assert.Greater(wins[10], wins[2] + 30, "the peak should be clearly harder than the early ranks");

        var top = new int[11];
        for (var rank = 6; rank <= 10; rank += 2) top[rank] = BotWins(rank, 9, 100);
        Debug.Log($"BALANCE out of 100 vs top bot: r6={top[6]} r8={top[8]} r10={top[10]}");
    }

    [Test]
    public void MirrorMatchesFavourNeitherSide()
    {
        var wins = BotWins(5, 5, 1500);
        var swapped = BotWins(5, 5, 1500, 1, true);
        var early = BotWins(2, 2, 1500, 11);
        var late = BotWins(8, 8, 750, 3);
        Debug.Log($"BALANCE mirror: P2 won r5={wins}/1500 swapped={swapped}/1500 r2={early}/1500 r8={late}/750");
        Assert.That(wins, Is.InRange(690, 810));
        Assert.That(swapped, Is.InRange(690, 810));
        Assert.That(early, Is.InRange(690, 810));
        Assert.That(late, Is.InRange(335, 415));
    }

    [Test]
    public void ProfilesScaleUpAndThenLevelOff()
    {
        var first = BotProfile.ForWins(0);
        var later = BotProfile.ForWins(6);
        var peak = BotProfile.ForWins(10);
        var beyond = BotProfile.ForWins(40);

        Assert.Less(later.Reaction, first.Reaction);
        Assert.Greater(later.ReadChance, first.ReadChance);
        Assert.Greater(later.MashRate, first.MashRate);
        Assert.AreEqual(peak.ReadChance, beyond.ReadChance);
        Assert.AreEqual("Yokozuna", beyond.Name);
    }

    [Test]
    public void ARunCountsWinsAndRaisesTheRival()
    {
        var run = new SumoRun();
        var start = run.Rival.ReadChance;
        run.RecordWin();
        run.RecordWin();
        Assert.AreEqual(2, run.Wins);
        Assert.Greater(run.Rival.ReadChance, start);
    }

    [Test]
    public void ARunCompletesOnlyAfterEveryRivalIsBeaten()
    {
        var run = new SumoRun();
        Assert.AreEqual(10, BotProfile.RivalCount);
        for (var i = 0; i < BotProfile.RivalCount - 1; i++) run.RecordWin();
        Assert.False(run.Complete);
        Assert.AreEqual("Yokozuna", run.Rival.Name);
        run.RecordWin();
        Assert.True(run.Complete);
    }
}
