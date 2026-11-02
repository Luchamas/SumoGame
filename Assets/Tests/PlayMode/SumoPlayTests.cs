using System.Collections;
using System.IO;
using NUnit.Framework;
using SumoGame;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>Plays the real scene through a virtual keyboard, saving screenshots to Logs/Shots.</summary>
public class SumoPlayTests : InputTestFixture
{
    Keyboard keyboard;
    SumoGameController game;

    public override void Setup()
    {
        base.Setup();
        keyboard = InputSystem.AddDevice<Keyboard>();

        // The test editor is never the focused window, which would otherwise disable the keyboard.
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    }

    // The fixture's clock does not follow the player loop and it can lose events that land
    // around a slow frame, so keep it ahead of real time and confirm each change arrived.
    IEnumerator Hold(Key key)
    {
        while (!keyboard[key].isPressed)
        {
            AdvanceClock();
            Press(keyboard[key]);
            yield return null;
            yield return null;
        }
    }

    IEnumerator LetGo(Key key)
    {
        while (keyboard[key].isPressed)
        {
            AdvanceClock();
            Release(keyboard[key]);
            yield return null;
            yield return null;
        }
    }

    void AdvanceClock() => currentTime = System.Math.Max(currentTime, Time.realtimeSinceStartupAsDouble) + 0.1;

    IEnumerator LoadGame()
    {
        yield return SceneManager.LoadSceneAsync("Sumo");
        yield return null;
        game = Object.FindAnyObjectByType<SumoGameController>();
        Assert.NotNull(game);
    }

    IEnumerator Tap(Key key)
    {
        yield return Hold(key);
        yield return LetGo(key);
        yield return null;
    }

    IEnumerator ShoveAndWait()
    {
        yield return Tap(Key.Space);
        yield return new WaitForSeconds(0.2f);
    }

    IEnumerator Shot(string name)
    {
        yield return new WaitForEndOfFrame();
        Directory.CreateDirectory("Logs/Shots");
        var texture = ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes($"Logs/Shots/{name}.png", texture.EncodeToPNG());
        Object.Destroy(texture);
    }

    IEnumerator StartVersus()
    {
        yield return LoadGame();
        yield return Tap(Key.S);
        Assert.AreEqual("Versus", game.Menu.SelectedLabel);
        yield return Tap(Key.Enter);
        Assert.False(game.InMenu);
        Assert.AreEqual(GameMode.Versus, game.Mode);
        yield return new WaitUntil(() => game.Sim.Phase == MatchPhase.Fighting);
    }

    IEnumerator StartSingleplayer()
    {
        yield return LoadGame();
        yield return Tap(Key.Enter);
        Assert.AreEqual(GameMode.Singleplayer, game.Mode);
    }

    IEnumerator WaitForEngage()
    {
        var sim = game.Sim;
        yield return new WaitUntil(() => sim.Phase != MatchPhase.Fighting
            || Vector2.Distance(sim.Fighters[0].Position, sim.Fighters[1].Position) <= sim.Rules.EngageDistance + 0.05f);
    }

    IEnumerator Grab()
    {
        var sim = game.Sim;
        var p1 = sim.Fighters[0];
        yield return WaitForEngage();
        yield return new WaitUntil(() => sim.Phase != MatchPhase.Fighting
            || (p1.RegrabCooldown <= 0f && (p1.State == FighterState.Idle || p1.State == FighterState.Moving)));
        if (sim.Phase != MatchPhase.Fighting) yield break;

        yield return Tap(Key.Space);
        yield return new WaitUntil(() => p1.IsLocked || p1.State == FighterState.Recovering || sim.Phase != MatchPhase.Fighting);
    }

    [UnityTest, Timeout(20000)]
    public IEnumerator TitleOffersTheFiveModesAndPages()
    {
        yield return LoadGame();
        yield return new WaitForSeconds(0.3f);
        yield return Shot("01_title");

        Assert.AreEqual("Singleplayer", game.Menu.SelectedLabel);
        foreach (var expected in new[] { "Versus", "Settings", "Controls", "Credits", "Singleplayer" })
        {
            yield return Tap(Key.DownArrow);
            Assert.AreEqual(expected, game.Menu.SelectedLabel);
        }
        Assert.True(game.InMenu);
    }

    [UnityTest, Timeout(20000)]
    public IEnumerator MenuPagesOpenAndGoBack()
    {
        yield return LoadGame();

        yield return Tap(Key.S);
        yield return Tap(Key.S);
        yield return Tap(Key.Enter);
        Assert.AreEqual("Settings", game.Menu.CurrentPage);
        yield return Shot("02_settings");
        yield return Tap(Key.Escape);
        Assert.AreEqual("Title", game.Menu.CurrentPage);

        yield return Tap(Key.S);
        yield return Tap(Key.S);
        yield return Tap(Key.S);
        yield return Tap(Key.Enter);
        Assert.AreEqual("Controls", game.Menu.CurrentPage);
        yield return Shot("03_controls");
        yield return Tap(Key.Enter);
        Assert.AreEqual("Title", game.Menu.CurrentPage);
    }

    [UnityTest, Timeout(20000)]
    public IEnumerator SpaceDoesNotConfirmMenus()
    {
        yield return LoadGame();
        yield return Tap(Key.Space);
        Assert.True(game.InMenu);
    }

    [UnityTest, Timeout(20000)]
    public IEnumerator MouseCanHoverAndClickMenuRows()
    {
        var mouse = InputSystem.AddDevice<Mouse>();
        yield return LoadGame();
        yield return null;

        var credits = System.Array.Find(Object.FindObjectsByType<Text>(FindObjectsSortMode.None), t => t.text == "Credits");
        Assert.NotNull(credits);
        var position = RectTransformUtility.WorldToScreenPoint(null, credits.rectTransform.position);

        AdvanceClock();
        Set(mouse.position, position);
        yield return new WaitUntil(() => game.Menu.SelectedLabel == "Credits");

        AdvanceClock();
        Press(mouse.leftButton);
        yield return null;
        yield return null;
        AdvanceClock();
        Release(mouse.leftButton);
        yield return new WaitUntil(() => game.Menu.CurrentPage == "Credits");
    }

    [UnityTest, Timeout(20000)]
    public IEnumerator SettingsChangeAndKeepTheirValue()
    {
        yield return LoadGame();
        yield return Tap(Key.S);
        yield return Tap(Key.S);
        yield return Tap(Key.Enter);
        yield return Tap(Key.S);
        yield return Tap(Key.S);
        Assert.That(game.Menu.SelectedLabel, Does.StartWith("Camera shake"));

        var before = GameSettings.CameraShake;
        yield return Tap(Key.Enter);
        Assert.AreNotEqual(before, GameSettings.CameraShake);
        yield return Tap(Key.Enter);
        Assert.AreEqual(before, GameSettings.CameraShake);
    }

    [UnityTest, Timeout(30000)]
    public IEnumerator EscapePausesTheFightAndResumes()
    {
        yield return StartVersus();
        yield return WaitForEngage();
        yield return Tap(Key.Escape);
        Assert.True(game.Paused);
        Assert.AreEqual("Pause", game.Menu.CurrentPage);
        yield return Shot("04_pause");

        var phaseTime = game.Sim.PhaseTime;
        yield return new WaitForSeconds(0.3f);
        Assert.AreEqual(phaseTime, game.Sim.PhaseTime);

        yield return Tap(Key.Escape);
        Assert.False(game.Paused);
        yield return new WaitUntil(() => game.Sim.PhaseTime > phaseTime);

        yield return Tap(Key.Escape);
        yield return Tap(Key.S);
        yield return Tap(Key.Enter);
        Assert.True(game.InMenu);
        Assert.AreEqual("Title", game.Menu.CurrentPage);
    }

    [UnityTest, Timeout(40000)]
    public IEnumerator P1KeysDriveP1Only()
    {
        yield return StartVersus();
        yield return WaitForEngage();
        var p1 = game.Sim.Fighters[0];
        var p2 = game.Sim.Fighters[1];
        yield return new WaitUntil(() => p2.State == FighterState.Idle);

        var y = p1.Position.y;
        var p2Start = p2.Position;
        yield return Hold(Key.A);
        yield return new WaitUntil(() => p1.State == FighterState.Moving && Mathf.Abs(p1.Position.y - y) > 0.3f);
        yield return LetGo(Key.A);
        Assert.AreEqual(p2Start, p2.Position, "P2 only turns to follow, and steps in when P1 drifts away");

        yield return new WaitUntil(() => p1.State == FighterState.Idle);
        yield return Hold(Key.LeftShift);
        yield return new WaitUntil(() => p1.IsStriking);
        yield return LetGo(Key.LeftShift);
    }

    [UnityTest, Timeout(40000)]
    public IEnumerator P2KeysDriveP2Only()
    {
        yield return StartVersus();
        yield return WaitForEngage();
        var p1 = game.Sim.Fighters[0];
        var p2 = game.Sim.Fighters[1];
        yield return new WaitUntil(() => p1.State == FighterState.Idle);

        var y = p2.Position.y;
        yield return Hold(Key.RightArrow);
        yield return new WaitUntil(() => p2.State == FighterState.Moving && Mathf.Abs(p2.Position.y - y) > 0.3f);
        yield return LetGo(Key.RightArrow);

        yield return new WaitUntil(() => p2.State == FighterState.Idle);
        yield return Hold(Key.DownArrow);
        yield return new WaitUntil(() => p2.IsStriking);
        yield return LetGo(Key.DownArrow);

        yield return new WaitUntil(() => p2.State == FighterState.Idle);
        yield return Hold(Key.UpArrow);
        yield return new WaitUntil(() => p2.State == FighterState.GrabStartup || p2.IsLocked);
        yield return LetGo(Key.UpArrow);
    }

    [UnityTest, Timeout(30000)]
    public IEnumerator GamepadDrivesP1()
    {
        var pad = InputSystem.AddDevice<Gamepad>();
        yield return StartVersus();
        yield return WaitForEngage();
        var p1 = game.Sim.Fighters[0];
        yield return new WaitUntil(() => p1.State == FighterState.Idle);
        var y = p1.Position.y;

        AdvanceClock();
        Set(pad.leftStick, Vector2.left);
        yield return new WaitUntil(() => Mathf.Abs(p1.Position.y - y) > 0.3f);
        AdvanceClock();
        Set(pad.leftStick, Vector2.zero);
        yield return new WaitUntil(() => p1.State == FighterState.Idle);

        AdvanceClock();
        Press(pad.buttonWest);
        yield return new WaitUntil(() => p1.IsStriking);
    }

    [UnityTest, Timeout(30000)]
    public IEnumerator DKeyMovesRightOnScreen()
    {
        yield return StartVersus();
        yield return WaitForEngage();
        var p1 = game.Sim.Fighters[0];
        yield return new WaitUntil(() => p1.State == FighterState.Idle);
        var cameraRight = game.P1Camera.transform.right;
        var start = p1.Position;

        yield return Hold(Key.D);
        yield return new WaitUntil(() => Vector2.Distance(p1.Position, start) > 0.4f);
        yield return LetGo(Key.D);

        var moved = p1.Position - start;
        Assert.Greater(moved.x * cameraRight.x + moved.y * cameraRight.z, 0.2f);
    }

    [UnityTest, Timeout(30000)]
    public IEnumerator AMissedGrabLooksLikeAMiss()
    {
        yield return StartVersus();
        yield return WaitForEngage();
        var p1 = game.Sim.Fighters[0];
        var p2 = game.Sim.Fighters[1];

        yield return Hold(Key.A);
        yield return Hold(Key.UpArrow);
        yield return new WaitUntil(() => p2.State == FighterState.Recovering);
        yield return LetGo(Key.UpArrow);
        yield return LetGo(Key.A);
        yield return new WaitForSeconds(0.35f);
        yield return Shot("13_whiff");

        var lunge = new Vector3(p2.LungeDirection.x, 0f, p2.LungeDirection.y);
        var toTarget = new Vector3(p1.Position.x - p2.Position.x, 0f, p1.Position.y - p2.Position.y).normalized;
        Assert.AreEqual(FighterState.Recovering, p2.State);
        Assert.Greater(Vector3.Dot(game.P2View.transform.forward, lunge), 0.97f, "the body should keep facing the lunge");
        Assert.Less(Vector3.Dot(game.P2View.transform.forward, toTarget), 0.9f, "the attacker should not be tracking the target");
    }

    [UnityTest, Timeout(30000)]
    public IEnumerator HitsThrowSparks()
    {
        yield return StartVersus();
        yield return WaitForEngage();
        var particles = game.Effects.GetComponent<ParticleSystem>();
        yield return Hold(Key.LeftShift);
        yield return LetGo(Key.LeftShift);
        yield return new WaitUntil(() => particles.particleCount > 0);
        yield return Shot("15_spark");
        yield return new WaitForSeconds(0.04f);
        yield return Shot("15_spark2");
        yield return new WaitForSeconds(0.06f);
        yield return Shot("15_spark3");
    }

    // Stages an exchange in one full-screen view, sized like Singleplayer, like the footage.
    IEnumerator StageExchange()
    {
        yield return StartVersus();
        yield return WaitForEngage();
        game.P2Camera.SetView(false, new Rect(0f, 0f, 1f, 1f), 58f);
        game.P1Camera.SetView(true, new Rect(0f, 0f, 1f, 1f), 58f);
        game.P2View.transform.localScale = Vector3.one * SumoGameController.RivalScale;
        yield return new WaitForSeconds(0.9f);
    }

    IEnumerator CaptureSequence(string name)
    {
        for (var i = 0; i < 8; i++)
        {
            yield return Shot($"ex_{name}_{i}");
            yield return new WaitForSeconds(0.14f);
        }
    }

    [UnityTest, Timeout(30000)]
    public IEnumerator ExchangeGrabVersusGrab()
    {
        yield return StageExchange();
        yield return Hold(Key.Space);
        yield return Hold(Key.UpArrow);
        yield return CaptureSequence("grab_vs_grab");
        Assert.False(game.Sim.Fighters[0].IsLocked);
        Assert.False(game.Sim.Fighters[1].IsLocked);
    }

    [UnityTest, Timeout(30000)]
    public IEnumerator ExchangeStrikeVersusGrab()
    {
        yield return StageExchange();
        var p1 = game.Sim.Fighters[0];
        yield return Hold(Key.DownArrow);
        yield return new WaitUntil(() => game.Sim.Fighters[1].IsStriking);
        yield return Hold(Key.Space);
        yield return CaptureSequence("strike_vs_grab");
        Assert.True(p1.IsLocked, "the grab should land after the swing");
    }

    [UnityTest, Timeout(30000)]
    public IEnumerator ExchangeGrabVersusSidewalk()
    {
        yield return StageExchange();
        yield return Hold(Key.A);
        yield return new WaitUntil(() => game.Sim.Fighters[0].IsSidewalking);
        yield return Tap(Key.UpArrow);
        yield return CaptureSequence("grab_vs_sidewalk");
        Assert.False(game.Sim.Fighters[1].IsLocked);
    }

    [UnityTest, Timeout(30000)]
    public IEnumerator ExchangeGrabEnding()
    {
        yield return StageExchange();
        yield return Tap(Key.Space);
        yield return new WaitUntil(() => game.Sim.Fighters[0].IsLocked);
        yield return new WaitUntil(() => game.Sim.HoldTime > game.Sim.Rules.MaxHold - 0.15f);
        yield return CaptureSequence("grab_break");
    }

    void AssertCentredBehind(SumoCamera view, WrestlerView own, WrestlerView other, float clearance)
    {
        var camera = view.GetComponent<Camera>();
        var self = camera.WorldToViewportPoint(own.Spine.position);
        var head = camera.WorldToViewportPoint(own.Head.position);
        var foe = camera.WorldToViewportPoint(other.Head.position);
        Debug.Log($"BEHIND {view.name} selfX={self.x:F3} ownHeadY={head.y:F3} foeHeadY={foe.y:F3}");
        Assert.AreEqual(0.5f, self.x, 0.04f, view.name + " wrestler should be centred");
        Assert.Greater(foe.y, head.y + clearance, view.name + " opponent's head should clear the wrestler's own");
    }

    [UnityTest, Timeout(30000)]
    public IEnumerator VersusCentresEachWrestlerAndShowsTheOpponentInAGrab()
    {
        yield return StartVersus();
        yield return WaitForEngage();
        yield return new WaitForSeconds(0.4f);
        yield return Shot("05_versus_neutral");
        yield return Grab();
        Assert.True(game.Sim.Fighters[0].IsLocked);
        yield return new WaitForSeconds(0.5f);
        yield return Shot("06_versus_grab");

        AssertCentredBehind(game.P1Camera, game.P1View, game.P2View, 0.05f);
        AssertCentredBehind(game.P2Camera, game.P2View, game.P1View, 0.05f);
    }

    [UnityTest, Timeout(180000)]
    public IEnumerator VersusIsSplitAndPlaysToTheMatchEnd()
    {
        yield return LoadGame();
        var rounds = GameSettings.VersusRounds;
        yield return Tap(Key.S);
        yield return Tap(Key.Enter);
        yield return new WaitForSeconds(0.2f);

        var left = game.P1Camera.GetComponent<Camera>();
        var right = game.P2Camera.GetComponent<Camera>();
        Assert.True(left.enabled && right.enabled);
        Assert.AreEqual(new Rect(0f, 0f, 0.5f, 1f), left.rect);
        Assert.AreEqual(new Rect(0.5f, 0f, 0.5f, 1f), right.rect);

        var sim = game.Sim;
        while (sim.Phase != MatchPhase.MatchOver)
        {
            yield return new WaitUntil(() => sim.Phase == MatchPhase.Fighting);
            var score = sim.Fighters[0].Score;
            while (sim.Fighters[0].Score == score)
            {
                yield return Grab();
                while (sim.Fighters[0].IsLocked) yield return ShoveAndWait();
                if (sim.Fighters[1].State == FighterState.Reeling)
                {
                    yield return new WaitForSeconds(0.3f);
                    yield return Shot("14_finishing_drive");
                }
                yield return new WaitForSeconds(0.7f);
            }
            yield return new WaitForSeconds(0.8f);
            if (score == 0) yield return Shot("07_versus_ringout");
            Assert.AreEqual(FighterState.Falling, sim.Fighters[1].State);
            if (sim.Phase == MatchPhase.RoundOver) yield return Tap(Key.Enter);
            yield return new WaitUntil(() => sim.Phase != MatchPhase.RoundOver);
        }

        Assert.AreEqual(0, sim.MatchWinner);
        Assert.AreEqual(rounds, sim.Fighters[0].Score);
        yield return Tap(Key.Enter);
        Assert.AreEqual(MatchPhase.Intro, sim.Phase);
        Assert.AreEqual(0, sim.Fighters[0].Score);
    }

    [UnityTest, Timeout(40000)]
    public IEnumerator SingleplayerUsesTheSameCameraBehindThePlayer()
    {
        yield return StartSingleplayer();
        Assert.False(game.P2Camera.GetComponent<Camera>().enabled);
        var camera = game.P1Camera.GetComponent<Camera>();
        Assert.AreEqual(new Rect(0f, 0f, 1f, 1f), camera.rect);

        yield return new WaitForSeconds(0.5f);
        yield return Shot("08_intro_start");
        yield return new WaitUntil(() => game.Sim.Phase == MatchPhase.Fighting);
        yield return WaitForEngage();
        yield return new WaitForSeconds(0.2f);
        Assert.AreEqual(FighterState.Idle, game.Sim.Fighters[1].State, "the rival should still be sizing you up");
        yield return Shot("09_single_neutral");

        var p1 = game.Sim.Fighters[0];
        AssertCentredBehind(game.P1Camera, game.P1View, game.P2View, 0.05f);
        var behind = camera.transform.position - new Vector3(p1.Position.x, camera.transform.position.y, p1.Position.y);
        Assert.Less(Vector3.Dot(behind.normalized, new Vector3(p1.Facing.x, 0f, p1.Facing.y)), -0.95f);
    }

    [UnityTest, Timeout(60000)]
    public IEnumerator BeatingTheLastRivalCompletesTheRunAndARoundLossDoesNotEndIt()
    {
        var best = GameSettings.BestRun;
        yield return StartSingleplayer();
        var sim = game.Sim;
        var run = game.Run;
        for (var i = 0; i < BotProfile.RivalCount - 1; i++) run.RecordWin();
        var outside = new Vector2(sim.Rules.RingRadius + 0.2f, 0f);

        // Lose a round, then win two: same run, last rival beaten.
        yield return new WaitUntil(() => sim.Phase == MatchPhase.Fighting);
        sim.Fighters[0].Position = -outside;
        yield return new WaitUntil(() => sim.Phase == MatchPhase.RoundOver);
        yield return Tap(Key.Enter);
        Assert.AreSame(run, game.Run);
        Assert.AreEqual(BotProfile.RivalCount - 1, run.Wins);

        for (var round = 0; round < 2; round++)
        {
            yield return new WaitUntil(() => sim.Phase == MatchPhase.Fighting);
            sim.Fighters[1].Position = outside;
            yield return new WaitUntil(() => sim.Phase == MatchPhase.RoundOver);
            yield return Tap(Key.Enter);
        }

        yield return new WaitUntil(() => run.Complete);
        yield return new WaitForSeconds(0.3f);
        Assert.AreEqual(MatchPhase.MatchOver, sim.Phase);
        yield return Shot("10_run_complete");

        yield return Tap(Key.Enter);
        Assert.AreNotSame(run, game.Run);
        Assert.AreEqual(0, game.Run.Wins);
        GameSettings.BestRun = best;
    }

    [UnityTest, Timeout(180000)]
    public IEnumerator SingleplayerRunEndsWhenTheMatchIsLost()
    {
        yield return StartSingleplayer();

        // Standing still loses to the bot sooner or later.
        yield return new WaitUntil(() => game.Sim.Fighters[0].IsLocked);
        yield return new WaitForSeconds(0.6f);
        yield return Shot("11_single_grab");
        yield return new WaitUntil(() => game.Sim.Phase == MatchPhase.MatchOver);
        yield return new WaitForSeconds(0.2f);
        yield return Shot("12_run_over");
        Assert.AreEqual(0, game.Run.Wins);
        Assert.AreEqual(1, game.Sim.MatchWinner);

        yield return Tap(Key.Enter);
        Assert.AreEqual(MatchPhase.Intro, game.Sim.Phase);
        yield return Tap(Key.Escape);
        yield return Tap(Key.S);
        yield return Tap(Key.Enter);
        Assert.True(game.InMenu);
        Assert.AreEqual("Title", game.Menu.CurrentPage);
    }
}
