using UnityEngine;
using UnityEngine.InputSystem;

namespace SumoGame
{
    /// <summary>Key layout for one player. Keys are physical positions, so layouts like AZERTY still work.</summary>
    public readonly struct PlayerKeys
    {
        public static readonly PlayerKeys P1 = new PlayerKeys(Key.A, Key.D, Key.Space, Key.LeftShift);
        public static readonly PlayerKeys P2 = new PlayerKeys(Key.LeftArrow, Key.RightArrow, Key.UpArrow, Key.DownArrow);

        readonly Key left, right, grab, strike;

        PlayerKeys(Key left, Key right, Key grab, Key strike)
        {
            this.left = left;
            this.right = right;
            this.grab = grab;
            this.strike = strike;
        }

        public FighterInput Read(Keyboard keyboard) => new FighterInput
        {
            Lateral = (keyboard[right].isPressed ? 1f : 0f) - (keyboard[left].isPressed ? 1f : 0f),
            Grab = keyboard[grab].isPressed,
            Strike = keyboard[strike].isPressed,
        };

        public static FighterInput Read(Gamepad pad) => new FighterInput
        {
            Lateral = pad.leftStick.ReadValue().x,
            Grab = pad.buttonSouth.isPressed,
            Strike = pad.buttonWest.isPressed,
        };
    }

    public enum GameMode { Singleplayer, Versus }

    /// <summary>Connects keyboard, gamepads, menus, simulation, views and HUD.</summary>
    public class SumoGameController : MonoBehaviour
    {
        const float FullFieldOfView = 58f;
        const float SplitFieldOfView = 66f;
        const float StruggleRumble = 0.35f;
        public const float RivalScale = 1.3f;
        const float StrikeHitStop = 0.07f;
        const float DriveHitStop = 0.1f;
        static readonly Rect FullView = new Rect(0f, 0f, 1f, 1f);
        static readonly Rect LeftView = new Rect(0f, 0f, 0.5f, 1f);
        static readonly Rect RightView = new Rect(0.5f, 0f, 0.5f, 1f);

        public WrestlerView P1View;
        public WrestlerView P2View;
        public SumoCamera P1Camera;
        public SumoCamera P2Camera;
        public SumoHud Hud;
        public SumoMenu Menu;
        public ImpactEffects Effects;
        public SumoAudio Sounds;

        public SumoSim Sim { get; private set; }
        public SumoRun Run { get; private set; }
        public GameMode Mode { get; private set; }
        public bool InMenu { get; private set; } = true;
        public bool Paused { get; private set; }
        public bool NewBest { get; private set; }

        readonly SumoRules rules = new SumoRules();
        SumoBot bot;
        int botSeed;

        FighterInput p1Input, p2Input;
        FighterInput p1Held, p2Held;
        FighterState p1State, p2State;
        MatchPhase lastPhase;
        float hitStop;

        void Awake()
        {
            GameSettings.Load();
            Sim = new SumoSim(rules);
            Sim.Impact += OnImpact;
            Hud.Build();
            Menu.Build(this);
            ShowMenu();
        }

        void OnDisable() => SetRumble(0f);

        void OnImpact(SumoImpact impact, Vector2 where)
        {
            Sounds.Play(impact);
            Effects.Burst(impact, where);

            if (impact == SumoImpact.Strike) hitStop = StrikeHitStop;
            if (impact == SumoImpact.Drive || impact == SumoImpact.RingOut) hitStop = DriveHitStop;
            if (impact == SumoImpact.Drive || impact == SumoImpact.RingOut)
            {
                P1Camera.Shake(0.16f);
                P2Camera.Shake(0.16f);
            }
        }

        public void ShowMenu()
        {
            InMenu = true;
            Paused = false;
            Sim.StartMatch();
            P1Camera.SetView(true, FullView, FullFieldOfView);
            P2Camera.SetView(false, FullView, FullFieldOfView);
            P1Camera.ShowWideShot();
            Menu.Open();
        }

        public void StartSingleplayer()
        {
            Mode = GameMode.Singleplayer;
            rules.RoundsToWin = 2;
            Run = new SumoRun();
            NewBest = false;
            P2View.transform.localScale = Vector3.one * RivalScale;
            NextBotFight();
            BeginPlay(FullView, FullFieldOfView, false);
        }

        public void StartVersus()
        {
            Mode = GameMode.Versus;
            rules.RoundsToWin = GameSettings.VersusRounds;
            Run = null;
            P2View.transform.localScale = Vector3.one;
            Sim.StartMatch();
            BeginPlay(LeftView, SplitFieldOfView, true);
        }

        public void Resume()
        {
            Paused = false;
            Menu.Close();
        }

        void Pause()
        {
            Paused = true;
            Menu.OpenPause();
        }

        void NextBotFight()
        {
            bot = new SumoBot(Run.Rival, ++botSeed);
            Sim.StartMatch();
        }

        void BeginPlay(Rect p1Viewport, float fieldOfView, bool split)
        {
            InMenu = false;
            Menu.Close();
            P1Camera.SetView(true, p1Viewport, fieldOfView);
            P2Camera.SetView(split, RightView, fieldOfView);
            SyncCameras(0f);
        }

        void SyncCameras(float dt)
        {
            P1Camera.Sync(Sim, 0, dt);
            P2Camera.Sync(Sim, 1, dt);
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null) ReadControls(keyboard);

            if (!Paused)
            {
                P1View.Sync(Sim, 0, Time.deltaTime);
                P2View.Sync(Sim, 1, Time.deltaTime);
                if (!InMenu) SyncCameras(Time.deltaTime);
            }
            Hud.Sync(this);
            SetRumble(!InMenu && !Paused && Sim.Fighters[0].IsLocked ? StruggleRumble : 0f);
        }

        void ReadControls(Keyboard keyboard)
        {
            var pad = Gamepad.current;
            if (InMenu || Paused)
            {
                Menu.HandleInput(keyboard, pad);
                return;
            }

            // Latch between fixed steps so a quick tap is never lost at high frame rates.
            p1Held = ReadPlayer(PlayerKeys.P1, keyboard, 0);
            p2Held = ReadPlayer(PlayerKeys.P2, keyboard, 1);
            p1Input = Latch(p1Input, p1Held);
            p2Input = Latch(p2Input, p2Held);

            if (keyboard.escapeKey.wasPressedThisFrame || (pad != null && pad.startButton.wasPressedThisFrame))
            {
                Pause();
                return;
            }

            if (!keyboard.enterKey.wasPressedThisFrame && !(pad != null && pad.buttonSouth.wasPressedThisFrame)) return;

            Sim.SkipRoundDelay();
            if (Sim.Phase != MatchPhase.MatchOver) return;

            if (Mode == GameMode.Singleplayer) StartSingleplayer();
            else StartVersus();
        }

        static FighterInput ReadPlayer(PlayerKeys keys, Keyboard keyboard, int index)
        {
            var input = keys.Read(keyboard);
            var pads = Gamepad.all;
            return index < pads.Count ? Latch(input, PlayerKeys.Read(pads[index])) : input;
        }

        static FighterInput Latch(FighterInput held, FighterInput current) => new FighterInput
        {
            Lateral = Mathf.Abs(current.Lateral) > Mathf.Abs(held.Lateral) ? current.Lateral : held.Lateral,
            Grab = held.Grab || current.Grab,
            Strike = held.Strike || current.Strike,
        };

        void FixedUpdate()
        {
            if (InMenu || Paused) return;

            var dt = Time.fixedDeltaTime;
            if (hitStop > 0f)
            {
                hitStop -= dt;
                return;
            }

            var opponent = Mode == GameMode.Singleplayer ? bot.Think(Sim, 1, dt) : p2Input;
            Sim.Step(dt, p1Input, opponent);
            p1Input = p1Held;
            p2Input = p2Held;

            ShakeOnImpact(Sim.Fighters[0], ref p1State, P1Camera);
            ShakeOnImpact(Sim.Fighters[1], ref p2State, P2Camera);
            WatchPhase();
        }

        void WatchPhase()
        {
            if (Sim.Phase == lastPhase) return;
            lastPhase = Sim.Phase;

            if (Sim.Phase == MatchPhase.Countdown) SyncCameras(0f);
            if (Sim.Phase == MatchPhase.Fighting) Sounds.PlayClap();
            if (Sim.Phase != MatchPhase.MatchOver || Mode != GameMode.Singleplayer) return;

            if (Sim.MatchWinner == 0) Run.RecordWin();

            if (Sim.MatchWinner == 0 && !Run.Complete)
            {
                NextBotFight();
            }
            else if (Run.Wins > GameSettings.BestRun)
            {
                GameSettings.BestRun = Run.Wins;
                NewBest = true;
            }
        }

        void ShakeOnImpact(Fighter fighter, ref FighterState lastState, SumoCamera view)
        {
            if (fighter.State == FighterState.Stunned && lastState != FighterState.Stunned) view.Shake(0.14f);
            lastState = fighter.State;
        }

        static void SetRumble(float amount)
        {
            foreach (var pad in Gamepad.all) pad.SetMotorSpeeds(amount * 0.5f, amount);
        }
    }
}
