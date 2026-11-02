using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace SumoGame
{
    /// <summary>Title screen and its pages. Works with the keyboard or the mouse.</summary>
    public class SumoMenu : MonoBehaviour
    {
        const string ControlsText =
            "Hold the middle of the ring. If you are driven back, walk sideways to circle round.\n" +
            "Walking sideways beats grab.   Grab beats strike.   Strike beats walking sideways.\n\n" +
            "A grab lasts 3 seconds. Shoves and grabbing again both have cooldowns, so do not spam.\n" +
            "If you are the one grabbed, press Grab fast to resist.";

        static readonly string[,] ControlsTable =
        {
            { "", "P1", "P2", "Gamepad" },
            { "Walk sideways", "A  D", "J  L", "Left stick" },
            { "Grab", "Space", "I", "A / Cross" },
            { "Strike", "Left Shift", "K", "X / Square" },
            { "Pause", "Esc", "Esc", "Start" },
        };

        const string CreditsText =
            "<b>Sumo Game</b>\n\n" +
            "Arena: \"sumo arena\" by unForastero, CC0, OpenGameArt.org\n" +
            "Made with Unity 6";

        class Item
        {
            public Func<string> Label;
            public Action Pick;
            public Action<int> Adjust;
            public Text Text;
        }

        class Page
        {
            public GameObject Root;
            public readonly List<Item> Items = new List<Item>();
        }

        Page title, settings, controls, credits, pause, current;
        SumoGameController game;
        int selected;

        public string CurrentPage => current == null ? "" : current.Root.name;
        public string SelectedLabel => current.Items[selected].Label();

        public void Build(SumoGameController controller)
        {
            game = controller;
            BuildEventSystem();

            title = NewPage("Title", "SUMO GAME", 150, null);
            AddItem(title, "Singleplayer", game.StartSingleplayer);
            AddItem(title, "Versus", game.StartVersus);
            AddItem(title, "Settings", () => Show(settings));
            AddItem(title, "Controls", () => Show(controls));
            AddItem(title, "Credits", () => Show(credits));

            settings = NewPage("Settings", "SETTINGS", 96, null);
            AddOption(settings, () => "Fullscreen: " + OnOff(GameSettings.Fullscreen), _ => GameSettings.ToggleFullscreen());
            AddOption(settings, () => "VSync: " + OnOff(GameSettings.VSync), _ => GameSettings.ToggleVSync());
            AddOption(settings, () => "Camera shake: " + OnOff(GameSettings.CameraShake), _ => GameSettings.ToggleCameraShake());
            AddOption(settings, () => $"Volume: {GameSettings.VolumePercent}%", GameSettings.CycleVolume);
            AddOption(settings, () => "Versus rounds to win: " + GameSettings.VersusRounds, GameSettings.CycleVersusRounds);
            AddItem(settings, "Back", Back);

            controls = NewPage("Controls", "CONTROLS", 96, ControlsText, 0.27f);
            BuildControlsTable(controls);
            AddItem(controls, "Back", Back);

            credits = NewPage("Credits", "CREDITS", 96, CreditsText);
            AddItem(credits, "Back", Back);

            pause = NewPage("Pause", "PAUSED", 96, null);
            AddItem(pause, "Resume", game.Resume);
            AddItem(pause, "Quit to title", game.ShowMenu);

            LayOut(pause, 0.55f);
            LayOut(title, 0.5f);
            LayOut(settings, 0.64f);
            LayOut(controls, 0.1f);
            LayOut(credits, 0.3f);
        }

        void BuildEventSystem()
        {
            var system = new GameObject("EventSystem").AddComponent<EventSystem>();
            system.sendNavigationEvents = false;
            system.gameObject.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        static string OnOff(bool value) => value ? "On" : "Off";

        void BuildControlsTable(Page page)
        {
            const float columnWidth = 260f, rowHeight = 58f;
            for (var row = 0; row < ControlsTable.GetLength(0); row++)
            {
                for (var column = 0; column < ControlsTable.GetLength(1); column++)
                {
                    var cell = UiKit.Text("Cell", page.Root.transform, 40, TextAnchor.MiddleCenter, UiKit.Paper);
                    cell.text = ControlsTable[row, column];
                    cell.fontStyle = row == 0 || column == 0 ? FontStyle.Bold : FontStyle.Normal;
                    var offset = new Vector2((column - 1.5f) * columnWidth, -row * rowHeight);
                    UiKit.Place(cell.rectTransform, new Vector2(0.5f, 0.72f), new Vector2(columnWidth, rowHeight), offset);
                }
            }
        }

        Page NewPage(string name, string heading, int size, string body, float bodyHeight = 0.46f)
        {
            var panel = UiKit.Image(name, game.Hud.transform, new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.88f));
            UiKit.Stretch(panel.rectTransform);

            var text = UiKit.Text("Heading", panel.transform, size, TextAnchor.MiddleCenter, UiKit.Paper);
            text.text = heading;
            text.fontStyle = FontStyle.Bold;
            UiKit.Place(text.rectTransform, new Vector2(0.5f, 0.84f), new Vector2(1400f, 200f), Vector2.zero);

            if (body != null)
            {
                var copy = UiKit.Text("Body", panel.transform, 42, TextAnchor.MiddleCenter, UiKit.Paper);
                copy.text = body;
                UiKit.Place(copy.rectTransform, new Vector2(0.5f, bodyHeight), new Vector2(1500f, 200f), Vector2.zero);
            }

            var page = new Page { Root = panel.gameObject };
            page.Root.SetActive(false);
            return page;
        }

        void AddItem(Page page, string label, Action pick) =>
            page.Items.Add(new Item { Label = () => label, Pick = pick });

        void AddOption(Page page, Func<string> label, Action<int> adjust) =>
            page.Items.Add(new Item { Label = label, Pick = () => adjust(1), Adjust = adjust });

        void LayOut(Page page, float top)
        {
            const float rowHeight = 92f;
            for (var i = 0; i < page.Items.Count; i++)
            {
                var item = page.Items[i];
                var row = i;
                item.Text = UiKit.Text("Item", page.Root.transform, 54, TextAnchor.MiddleCenter, UiKit.Paper);
                item.Text.raycastTarget = true;
                UiKit.Place(item.Text.rectTransform, new Vector2(0.5f, top), new Vector2(900f, rowHeight), new Vector2(0f, -i * rowHeight));

                item.Text.gameObject.AddComponent<Button>().onClick.AddListener(() => item.Pick());
                item.Text.gameObject.AddComponent<MenuHover>().Entered = () => Select(row);
            }
        }

        public void Open()
        {
            Show(title);
            selected = 0;
            Refresh();
        }

        public void OpenPause() => Show(pause);

        public void Close()
        {
            if (current != null) current.Root.SetActive(false);
            current = null;
        }

        void Show(Page page)
        {
            if (current != null) current.Root.SetActive(false);
            current = page;
            current.Root.SetActive(true);
            selected = 0;
            Refresh();
        }

        void Back() => Show(title);

        void Select(int index)
        {
            selected = index;
            Refresh();
        }

        void Refresh()
        {
            for (var i = 0; i < current.Items.Count; i++)
            {
                var item = current.Items[i];
                if (item.Text == null) continue;
                var label = item.Label();
                item.Text.text = i == selected ? $"> {label} <" : label;
                item.Text.color = i == selected ? UiKit.Accent : UiKit.Paper;
            }
        }

        public void HandleInput(Keyboard keyboard, Gamepad pad)
        {
            if (current == null) return;

            var up = keyboard.wKey.wasPressedThisFrame || keyboard.iKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame;
            var down = keyboard.sKey.wasPressedThisFrame || keyboard.kKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame;
            var left = keyboard.aKey.wasPressedThisFrame || keyboard.jKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame;
            var right = keyboard.dKey.wasPressedThisFrame || keyboard.lKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame;

            var confirm = keyboard.enterKey.wasPressedThisFrame;
            var back = keyboard.escapeKey.wasPressedThisFrame;

            if (pad != null)
            {
                up |= pad.dpad.up.wasPressedThisFrame;
                down |= pad.dpad.down.wasPressedThisFrame;
                left |= pad.dpad.left.wasPressedThisFrame;
                right |= pad.dpad.right.wasPressedThisFrame;
                confirm |= pad.buttonSouth.wasPressedThisFrame;
                back |= pad.buttonEast.wasPressedThisFrame;
            }

            var count = current.Items.Count;
            if (up) Select((selected + count - 1) % count);
            if (down) Select((selected + 1) % count);

            var item = current.Items[selected];
            if ((left || right) && item.Adjust != null)
            {
                item.Adjust(right ? 1 : -1);
                Refresh();
            }

            if (confirm)
            {
                item.Pick();
                if (current != null) Refresh();
            }

            if (back && current != null)
            {
                if (current == title) Application.Quit();
                else if (current == pause) game.Resume();
                else Back();
            }
        }
    }

    /// <summary>Lets a menu row highlight when the mouse moves over it.</summary>
    public class MenuHover : MonoBehaviour, IPointerEnterHandler
    {
        public Action Entered;

        public void OnPointerEnter(PointerEventData eventData) => Entered?.Invoke();
    }
}
