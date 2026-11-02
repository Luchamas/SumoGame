using UnityEngine;

namespace SumoGame
{
    /// <summary>How well an opponent fights. New enemy types can be added as more profiles.</summary>
    public class BotProfile
    {
        static readonly string[] Ranks =
        {
            "Jonokuchi", "Jonidan", "Sandanme", "Makushita", "Juryo",
            "Maegashira", "Komusubi", "Sekiwake", "Ozeki", "Yokozuna",
        };

        public string Name;
        public float Reaction;     // seconds before it notices what the player did
        public float ReadChance;   // odds of answering that with the right counter
        public float ThinkTime;    // seconds between choices when nothing is happening
        public float MashRate;     // grab presses per second in a struggle
        public float GrabFocus;    // odds of grabbing instead of a riskier choice when in range
        public float Adaptation;   // odds of stepping aside early once the player keeps repeating grabs

        public static int RivalCount => Ranks.Length;

        public static BotProfile ForWins(int wins)
        {
            var t = Mathf.Clamp01(wins / (float)(RivalCount - 1));
            return new BotProfile
            {
                Name = Ranks[Mathf.Min(wins, Ranks.Length - 1)],
                Reaction = Mathf.Lerp(0.42f, 0.13f, t),
                ReadChance = Mathf.Lerp(0.15f, 0.85f, t),
                ThinkTime = Mathf.Lerp(1.5f, 0.4f, t),
                MashRate = Mathf.Lerp(1.5f, 3.9f, Mathf.Sqrt(t)),
                GrabFocus = Mathf.Lerp(0.5f, 0.65f, t),
                Adaptation = Mathf.Clamp01(0.1f + t * 3.6f),
            };
        }
    }

    /// <summary>Produces the same inputs a player would, so it plays by exactly the same rules.</summary>
    public class SumoBot
    {
        const float EdgeMargin = 1.2f;
        const float SteerHold = 0.4f;
        const float SidewalkHold = 0.45f;
        const int GrabsBeforeReading = 2;
        const float StreakMemory = 6f; // longer than a full grab and its cooldown

        readonly BotProfile profile;
        readonly System.Random random;

        FighterState watched;
        float reactIn;
        bool reacted;
        float thinkIn;
        float mashClock;
        float steerFor, steerSign;
        int grabStreak;
        float streakAge;
        bool wantGrab, wantStrike;

        public SumoBot(BotProfile profile, int seed)
        {
            this.profile = profile;
            random = new System.Random(seed);
        }

        bool Chance(float odds) => random.NextDouble() < odds;

        public FighterInput Think(SumoSim sim, int index, float dt)
        {
            var self = sim.Fighters[index];
            var other = sim.Fighters[1 - index];
            if (sim.Phase != MatchPhase.Fighting)
            {
                // Every round starts with a moment to size up the opponent, never a free move.
                thinkIn = profile.ThinkTime;
                return default;
            }

            if (self.IsLocked)
            {
                mashClock += dt * profile.MashRate * 2f;
                var steering = self.State == FighterState.Pushing ? PushSteering(self, other) : 0f;
                return new FighterInput { Grab = (int)mashClock % 2 == 0, Lateral = steering };
            }

            Observe(self, other, sim, dt);
            Plan(self, other, sim, dt);
            return Act(self, other, sim, dt);
        }

        void Observe(Fighter self, Fighter other, SumoSim sim, float dt)
        {
            if (other.State != watched)
            {
                watched = other.State;
                reactIn = profile.Reaction;
                reacted = false;
                TrackHabit();
            }

            streakAge += dt;
            if (streakAge > StreakMemory) grabStreak = 0;

            reactIn -= dt;
            if (reacted || reactIn > 0f) return;
            reacted = true;

            var punishing = watched == FighterState.Recovering && !self.IsLocked;
            var read = punishing ? Mathf.Max(profile.ReadChance, profile.Adaptation) : profile.ReadChance;
            if (!Chance(read)) return;

            var distance = Vector2.Distance(self.Position, other.Position);
            switch (watched)
            {
                case FighterState.GrabStartup:
                    Sidewalk(self);
                    break;

                case FighterState.Moving:
                    wantStrike = other.IsSidewalking && distance <= sim.Rules.StrikeReachVsSidewalk;
                    break;

                case FighterState.Striking:
                case FighterState.Recovering:
                case FighterState.Stunned:
                    wantGrab = true;
                    break;
            }
        }

        bool ExpectsGrab => grabStreak >= GrabsBeforeReading;

        /// <summary>Repeating one move is a habit, and the counter to grab habits is to step aside.</summary>
        void TrackHabit()
        {
            if (watched == FighterState.GrabStartup)
            {
                grabStreak++;
                streakAge = 0f;
            }
            else if (watched == FighterState.Striking)
            {
                grabStreak = 0;
            }
            else if (watched == FighterState.Recovering)
            {
                // The read paid off. Keep the memory of it, but it takes a new grab to be sure again.
                grabStreak = Mathf.Min(grabStreak, GrabsBeforeReading - 1);
            }
        }

        void Plan(Fighter self, Fighter other, SumoSim sim, float dt)
        {
            thinkIn -= dt;
            if (thinkIn > 0f) return;
            thinkIn = profile.ThinkTime * (0.7f + (float)random.NextDouble() * 0.6f);

            if (ExpectsGrab && Chance(profile.Adaptation))
            {
                Sidewalk(self);
                thinkIn = Mathf.Min(thinkIn, SidewalkHold);
                return;
            }

            if (Vector2.Distance(self.Position, other.Position) > sim.Rules.GrabRange) return;

            if (Chance(profile.GrabFocus)) wantGrab = true;
            else if (Chance(0.6f)) wantStrike = true;
            else Sidewalk(self);
        }

        FighterInput Act(Fighter self, Fighter other, SumoSim sim, float dt)
        {
            var input = new FighterInput { Lateral = Steer(self, sim, dt) };
            var free = self.State == FighterState.Idle || self.State == FighterState.Moving;
            if (!free) return input;

            var previous = self.Previous;
            if (wantStrike && !previous.Strike)
            {
                wantStrike = false;
                input.Strike = true;
            }
            else if (wantGrab && !previous.Grab && self.RegrabCooldown <= 0f && Vector2.Distance(self.Position, other.Position) <= sim.Rules.GrabRange)
            {
                wantGrab = false;
                input.Grab = true;
            }
            return input;
        }

        /// <summary>Walks sideways for a moment, and skilled wrestlers walk towards the middle.</summary>
        void Sidewalk(Fighter self)
        {
            var right = new Vector2(self.Facing.y, -self.Facing.x);
            var inward = -Mathf.Sign(Vector2.Dot(right, self.Position));
            steerSign = Chance(profile.ReadChance) ? inward : (Chance(0.5f) ? 1f : -1f);
            steerFor = SidewalkHold;
        }

        /// <summary>Skilled wrestlers aim the push at the nearest stretch of rope.</summary>
        float PushSteering(Fighter self, Fighter other)
        {
            var angle = Vector2.SignedAngle(self.Facing, other.Position);
            return -Mathf.Sign(angle) * profile.ReadChance;
        }

        /// <summary>Circles back towards the middle when backed up to the rope.</summary>
        float Steer(Fighter self, SumoSim sim, float dt)
        {
            steerFor -= dt;
            if (steerFor <= 0f)
            {
                if (self.Position.magnitude < sim.Rules.RingRadius - EdgeMargin) return 0f;

                // Only skilled wrestlers reliably work their way back off the rope.
                var right = new Vector2(self.Facing.y, -self.Facing.x);
                steerSign = Chance(profile.ReadChance) ? -Mathf.Sign(Vector2.Dot(right, self.Position)) : 0f;
                steerFor = SteerHold;
            }
            return steerSign;
        }
    }

    /// <summary>
    /// A singleplayer run through the rivals, kept apart from the match rules so rounds and
    /// matches can reset without touching it. A lost match ends it, and so does beating everyone.
    /// </summary>
    public class SumoRun
    {
        public int Wins { get; private set; }
        public BotProfile Rival { get; private set; } = BotProfile.ForWins(0);
        public bool Complete => Wins >= BotProfile.RivalCount;

        public void RecordWin()
        {
            Wins++;
            Rival = BotProfile.ForWins(Wins);
        }
    }
}
