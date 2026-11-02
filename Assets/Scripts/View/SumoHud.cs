using UnityEngine;
using UnityEngine.UI;

namespace SumoGame
{
    /// <summary>Everything drawn over the fight. The struggle stays hidden, as the wrestlers show it.</summary>
    public class SumoHud : MonoBehaviour
    {
        static readonly Color P1Color = new Color(1f, 0.38f, 0.32f);
        static readonly Color P2Color = new Color(0.42f, 0.66f, 1f);
        static readonly Color EmptyPip = new Color(1f, 1f, 1f, 0.18f);

        const int MaxPips = 3;
        const float PromptTime = 3f;

        GameObject root, divider;
        UiKit.Banner p1Label, p2Label, call, p1Prompt, p2Prompt;
        Image[] p1Pips, p2Pips;

        public void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            root = new GameObject("Fight", typeof(RectTransform));
            root.transform.SetParent(transform, false);
            UiKit.Stretch((RectTransform)root.transform);

            var line = UiKit.Image("Divider", root.transform, UiKit.Ink);
            divider = line.gameObject;
            line.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            line.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            line.rectTransform.sizeDelta = new Vector2(8f, 0f);

            p1Label = BuildLabel(P1Color, -1f);
            p2Label = BuildLabel(P2Color, 1f);
            p1Pips = BuildPips(-1f);
            p2Pips = BuildPips(1f);

            var centre = new Vector2(0.5f, 0.5f);
            p1Prompt = new UiKit.Banner("Prompt", root.transform, UiKit.Body, 34, UiKit.Paper, new Vector2(0.25f, 0.06f), centre, Vector2.zero);
            p2Prompt = new UiKit.Banner("Prompt", root.transform, UiKit.Body, 34, UiKit.Paper, new Vector2(0.75f, 0.06f), centre, Vector2.zero);
            call = new UiKit.Banner("Call", root.transform, UiKit.Display, 120, UiKit.Paper, new Vector2(0.5f, 0.72f), centre, Vector2.zero);
        }

        /// <summary>Only what can be pressed right now, as the original minigame does.</summary>
        static string PromptFor(SumoSim sim, Fighter self, string left, string right, string grab, string strike)
        {
            if (sim.Phase != MatchPhase.Fighting) return "";
            if (self.IsLocked) return $"{grab}: {(self.State == FighterState.Pushing ? "Push" : "Resist")}";

            var free = self.State == FighterState.Idle || self.State == FighterState.Moving;
            if (!free) return "";

            var grabPrompt = self.RegrabCooldown > 0f ? "" : $"{grab}: Grab     ";
            return $"< {left}   {right} >     {grabPrompt}{strike}: Strike";
        }

        UiKit.Banner BuildLabel(Color color, float side)
        {
            var corner = side < 0f ? 0f : 1f;
            var offset = new Vector2(-side * 24f, -24f);
            return new UiKit.Banner("Label", root.transform, UiKit.Body, 36, color, new Vector2(corner, 1f), new Vector2(corner, 1f), offset);
        }

        Image[] BuildPips(float side)
        {
            var anchor = new Vector2(side < 0f ? 0f : 1f, 1f);
            var pips = new Image[MaxPips];
            for (var i = 0; i < pips.Length; i++)
            {
                pips[i] = UiKit.Image("Pip", root.transform, EmptyPip);
                UiKit.Place(pips[i].rectTransform, anchor, new Vector2(36f, 36f), new Vector2(-side * (42f + i * 50f), -116f));
            }
            return pips;
        }

        public void Sync(SumoGameController game)
        {
            root.SetActive(!game.InMenu);
            if (game.InMenu) return;

            var sim = game.Sim;
            var single = game.Mode == GameMode.Singleplayer;

            divider.SetActive(!single);
            p1Label.Set(single ? $"YOU   Wins {game.Run.Wins}   Best {Mathf.Max(GameSettings.BestRun, game.Run.Wins)}" : "P1");
            p2Label.Set(single ? game.Run.Rival.Name.ToUpperInvariant() : "P2");

            SyncPips(p1Pips, sim.Rules.RoundsToWin, sim.Fighters[0].Score, P1Color);
            SyncPips(p2Pips, sim.Rules.RoundsToWin, sim.Fighters[1].Score, P2Color);
            call.Set(game.Paused ? "" : CallFor(game));
            p1Prompt.MoveTo(new Vector2(single ? 0.5f : 0.25f, 0.06f));
            p1Prompt.Set(game.Paused ? "" : PromptFor(sim, sim.Fighters[0], "A", "D", "Space", "Shift"));
            p2Prompt.Set(game.Paused || single ? "" : PromptFor(sim, sim.Fighters[1], "J", "L", "I", "K"));
        }

        static void SyncPips(Image[] pips, int count, int score, Color color)
        {
            for (var i = 0; i < pips.Length; i++)
            {
                pips[i].gameObject.SetActive(i < count);
                pips[i].color = i < score ? color : EmptyPip;
            }
        }

        static string CallFor(SumoGameController game)
        {
            var sim = game.Sim;
            var single = game.Mode == GameMode.Singleplayer;
            var hakkeyoi = single ? $"HAKKEYOI...\n<size=52>VS {game.Run.Rival.Name.ToUpperInvariant()}</size>" : "HAKKEYOI...";
            switch (sim.Phase)
            {
                case MatchPhase.Intro:
                    return sim.PhaseTime >= PromptTime ? hakkeyoi : "";
                case MatchPhase.Countdown:
                    return hakkeyoi;
                case MatchPhase.Fighting:
                    return sim.PhaseTime < 0.7f ? "NOKOTTA!" : "";
                case MatchPhase.RoundOver:
                    if (single) return sim.RoundWinner == 0 ? "YOU TAKE THE ROUND" : "YOU LOSE THE ROUND";
                    return $"P{sim.RoundWinner + 1} TAKES THE ROUND";
                case MatchPhase.MatchOver:
                    if (!single) return $"P{sim.MatchWinner + 1} IS YOKOZUNA!\n<size=46>ENTER TO REMATCH     ESC FOR MENU</size>";
                    if (game.Run.Complete) return "YOU ARE YOKOZUNA!\n<size=46>EVERY RIVAL DEFEATED\nENTER FOR A NEW RUN     ESC FOR MENU</size>";
                    var best = game.NewBest ? "NEW BEST!" : $"BEST {GameSettings.BestRun}";
                    return $"RUN OVER\n<size=46>{game.Run.Wins} WINS     {best}\nENTER TO TRY AGAIN     ESC FOR MENU</size>";
                default:
                    return "";
            }
        }
    }
}
