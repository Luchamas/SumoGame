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
        Text p1Label, p2Label, call, p1Prompt, p2Prompt;
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

            p1Prompt = BuildPrompt(0.25f);
            p2Prompt = BuildPrompt(0.75f);

            call = UiKit.Text("Call", root.transform, 104, TextAnchor.MiddleCenter, UiKit.Paper);
            UiKit.Place(call.rectTransform, new Vector2(0.5f, 0.74f), new Vector2(1600f, 300f), Vector2.zero);
            var outline = call.gameObject.AddComponent<Outline>();
            outline.effectColor = UiKit.Ink;
            outline.effectDistance = new Vector2(5f, -5f);
        }

        Text BuildPrompt(float x)
        {
            var text = UiKit.Text("Prompt", root.transform, 36, TextAnchor.MiddleCenter, UiKit.Paper);
            UiKit.Place(text.rectTransform, new Vector2(x, 0.06f), new Vector2(900f, 56f), Vector2.zero);
            var outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = UiKit.Ink;
            outline.effectDistance = new Vector2(3f, -3f);
            return text;
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

        Text BuildLabel(Color color, float side)
        {
            var anchor = new Vector2(side < 0f ? 0f : 1f, 1f);
            var text = UiKit.Text("Label", root.transform, 38, side < 0f ? TextAnchor.UpperLeft : TextAnchor.UpperRight, color);
            text.fontStyle = FontStyle.Bold;
            UiKit.Place(text.rectTransform, anchor, new Vector2(560f, 56f), new Vector2(-side * 300f, -56f));
            text.gameObject.AddComponent<Outline>().effectColor = UiKit.Ink;
            return text;
        }

        Image[] BuildPips(float side)
        {
            var anchor = new Vector2(side < 0f ? 0f : 1f, 1f);
            var pips = new Image[MaxPips];
            for (var i = 0; i < pips.Length; i++)
            {
                pips[i] = UiKit.Image("Pip", root.transform, EmptyPip);
                UiKit.Place(pips[i].rectTransform, anchor, new Vector2(36f, 36f), new Vector2(-side * (42f + i * 50f), -112f));
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
            p1Label.text = single ? $"YOU   Wins {game.Run.Wins}   Best {Mathf.Max(GameSettings.BestRun, game.Run.Wins)}" : "P1";
            p2Label.text = single ? game.Run.Rival.Name.ToUpperInvariant() : "P2";

            SyncPips(p1Pips, sim.Rules.RoundsToWin, sim.Fighters[0].Score, P1Color);
            SyncPips(p2Pips, sim.Rules.RoundsToWin, sim.Fighters[1].Score, P2Color);
            call.text = game.Paused ? "" : CallFor(game);
            p1Prompt.rectTransform.anchorMin = p1Prompt.rectTransform.anchorMax = new Vector2(single ? 0.5f : 0.25f, 0.06f);
            p1Prompt.text = game.Paused ? "" : PromptFor(sim, sim.Fighters[0], "A", "D", "Space", "Shift");
            p2Prompt.text = game.Paused || single ? "" : PromptFor(sim, sim.Fighters[1], "J", "L", "I", "K");
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
            var hakkeyoi = single ? $"HAKKEYOI...\n<size=46>vs {game.Run.Rival.Name}</size>" : "HAKKEYOI...";
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
                    if (!single) return $"P{sim.MatchWinner + 1} IS YOKOZUNA!\n<size=46>ENTER to rematch   ESC for menu</size>";
                    if (game.Run.Complete) return "YOU ARE YOKOZUNA!\n<size=46>Every rival defeated\nENTER to start a new run   ESC for menu</size>";
                    var best = game.NewBest ? "New best!" : $"Best {GameSettings.BestRun}";
                    return $"RUN OVER\n<size=46>{game.Run.Wins} wins   {best}\nENTER to try again   ESC for menu</size>";
                default:
                    return "";
            }
        }
    }
}
