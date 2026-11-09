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
        /// <summary>Action, then the keys for P1 and P2. Keys within a cell are split by '|'.</summary>
        static readonly string[,] ControlsTable =
        {
            { "Andar de lado", "A|D", "←|→" },
            { "Agarrar", "Espaço", "↑" },
            { "Golpear", "Shift esquerdo", "↓" },
            { "Pausar", "Esc", "Esc" },
        };

        const string CreditsText =
            "<b>Sumo Game</b>\n\n" +
            "Arena: \"sumo arena\" de unForastero, CC0, OpenGameArt.org\n" +
            "Fontes: Bebas Neue e Barlow Condensed, SIL Open Font License\n" +
            "Feito por Lucas Macedo, Otávio Salomão, João Pucci e Matheus Ferrarezi\n" +
            "Feito com Unity 6";

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
            AddItem(title, "Um jogador", game.StartSingleplayer);
            AddItem(title, "Versus", game.StartVersus);
            AddItem(title, "Configurações", () => Show(settings));
            AddItem(title, "Controles", () => Show(controls));
            AddItem(title, "Créditos", () => Show(credits));

            settings = NewPage("Settings", "CONFIGURAÇÕES", 96, null);
            AddOption(settings, () => "Tela cheia: " + OnOff(GameSettings.Fullscreen), _ => GameSettings.ToggleFullscreen());
            AddOption(settings, () => "VSync: " + OnOff(GameSettings.VSync), _ => GameSettings.ToggleVSync());
            AddOption(settings, () => "Tremor da câmera: " + OnOff(GameSettings.CameraShake), _ => GameSettings.ToggleCameraShake());
            AddOption(settings, () => $"Volume: {GameSettings.VolumePercent}%", GameSettings.CycleVolume);
            AddOption(settings, () => "Rodadas para vencer no versus: " + GameSettings.VersusRounds, GameSettings.CycleVersusRounds);
            AddItem(settings, "Voltar", Back);

            controls = NewPage("Controls", "CONTROLES", 96, null);
            BuildControlsTable(controls);
            AddItem(controls, "Voltar", Back);

            credits = NewPage("Credits", "CRÉDITOS", 96, CreditsText, 0.5f);
            AddItem(credits, "Voltar", Back);

            pause = NewPage("Pause", "PAUSADO", 96, null);
            AddItem(pause, "Continuar", game.Resume);
            AddItem(pause, "Sair para o menu", game.ShowMenu);

            LayOut(pause, 0.55f);
            LayOut(title, 0.5f);
            LayOut(settings, 0.64f);
            LayOut(controls, 0.2f);
            LayOut(credits, 0.22f);
        }

        void BuildEventSystem()
        {
            var system = new GameObject("EventSystem").AddComponent<EventSystem>();
            system.sendNavigationEvents = false;
            system.gameObject.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        static string OnOff(bool value) => value ? "Ligado" : "Desligado";

        void BuildControlsTable(Page page)
        {
            const float actionWidth = 420f, keysWidth = 440f, rowHeight = 92f, top = 0.7f;
            var width = actionWidth + 2f * keysWidth;
            var left = -width / 2f;
            var anchor = new Vector2(0.5f, top);

            AddCell(page, "J1", UiKit.Display, 56, SumoHud.P1Color, anchor, new Vector2(left + actionWidth + keysWidth / 2f, 0f), keysWidth);
            AddCell(page, "J2", UiKit.Display, 56, SumoHud.P2Color, anchor, new Vector2(left + actionWidth + keysWidth * 1.5f, 0f), keysWidth);

            for (var row = 0; row < ControlsTable.GetLength(0); row++)
            {
                var y = -(row + 1) * rowHeight;
                var stripe = UiKit.Image("Row", page.Root.transform, new Color(UiKit.Paper.r, UiKit.Paper.g, UiKit.Paper.b, row % 2 == 0 ? 0.08f : 0.03f));
                UiKit.Place(stripe.rectTransform, anchor, new Vector2(width, rowHeight - 6f), new Vector2(0f, y));

                AddCell(page, ControlsTable[row, 0], UiKit.Display, 50, UiKit.Paper, anchor, new Vector2(left + actionWidth / 2f, y), actionWidth);
                for (var player = 0; player < 2; player++)
                {
                    var centre = new Vector2(left + actionWidth + keysWidth * (player + 0.5f), y);
                    AddKeys(page, ControlsTable[row, player + 1].Split('|'), anchor, centre);
                }
            }
        }

        static void AddCell(Page page, string label, Font font, int size, Color color, Vector2 anchor, Vector2 centre, float width)
        {
            var cell = UiKit.Text("Cell", page.Root.transform, size, TextAnchor.MiddleCenter, color);
            cell.font = font;
            cell.text = label;
            UiKit.Place(cell.rectTransform, anchor, new Vector2(width, 80f), centre);
        }

        /// <summary>Arrow keys, drawn as images because the fonts have no arrow glyphs. Value is the rotation in degrees.</summary>
        static readonly Dictionary<string, float> ArrowKeys = new Dictionary<string, float>
        {
            { "→", 0f }, { "↑", 90f }, { "←", 180f }, { "↓", 270f },
        };

        static Sprite arrow;

        /// <summary>Each key as a cap, laid side by side around the centre of its cell.</summary>
        static void AddKeys(Page page, string[] keys, Vector2 anchor, Vector2 centre)
        {
            const float gap = 14f, padding = 22f, height = 60f;
            var ink = new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 1f);
            var labels = new Text[keys.Length];
            var widths = new float[keys.Length];
            var total = gap * (keys.Length - 1);
            for (var i = 0; i < keys.Length; i++)
            {
                if (ArrowKeys.ContainsKey(keys[i]))
                {
                    widths[i] = height;
                }
                else
                {
                    labels[i] = UiKit.Text("Key", page.Root.transform, 38, TextAnchor.MiddleCenter, ink);
                    labels[i].text = keys[i];
                    widths[i] = Mathf.Max(height, labels[i].preferredWidth + padding * 2f);
                }
                total += widths[i];
            }

            var x = centre.x - total / 2f;
            for (var i = 0; i < keys.Length; i++)
            {
                var offset = new Vector2(x + widths[i] / 2f, centre.y);
                var cap = UiKit.Image("Cap", page.Root.transform, UiKit.Paper);
                UiKit.Place(cap.rectTransform, anchor, new Vector2(widths[i], height), offset);

                if (labels[i] == null)
                {
                    var icon = UiKit.Image("Arrow", page.Root.transform, ink);
                    icon.sprite = arrow != null ? arrow : arrow = BuildArrow();
                    icon.rectTransform.localEulerAngles = new Vector3(0f, 0f, ArrowKeys[keys[i]]);
                    UiKit.Place(icon.rectTransform, anchor, new Vector2(40f, 40f), offset);
                }
                else
                {
                    labels[i].transform.SetAsLastSibling();
                    UiKit.Place(labels[i].rectTransform, anchor, new Vector2(widths[i], height), offset);
                }
                x += widths[i] + gap;
            }
        }

        /// <summary>A right-pointing arrow, supersampled so its edges stay smooth.</summary>
        static Sprite BuildArrow()
        {
            const int size = 64, samples = 4;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (var py = 0; py < size; py++)
            {
                for (var px = 0; px < size; px++)
                {
                    var inside = 0;
                    for (var sy = 0; sy < samples; sy++)
                    for (var sx = 0; sx < samples; sx++)
                    {
                        var x = px + (sx + 0.5f) / samples;
                        var y = py + (sy + 0.5f) / samples;
                        var dy = Mathf.Abs(y - size / 2f);
                        var shaft = x >= 6f && x <= 36f && dy <= 6f;
                        var head = x >= 30f && x <= 60f && dy <= (60f - x) * 0.8f;
                        if (shaft || head) inside++;
                    }
                    pixels[py * size + px] = new Color32(255, 255, 255, (byte)(255 * inside / (samples * samples)));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        Page NewPage(string name, string heading, int size, string body, float bodyHeight = 0.46f)
        {
            var panel = UiKit.Image(name, game.Hud.transform, new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.88f));
            UiKit.Stretch(panel.rectTransform);

            var text = UiKit.Text("Heading", panel.transform, size, TextAnchor.MiddleCenter, UiKit.Paper);
            text.text = heading;
            text.font = UiKit.Display;
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
                item.Text = UiKit.Text("Item", page.Root.transform, 60, TextAnchor.MiddleCenter, UiKit.Paper);
                item.Text.font = UiKit.Display;
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

        public void HandleInput(Keyboard keyboard)
        {
            if (current == null) return;

            var up = keyboard.wKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame;
            var down = keyboard.sKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame;
            var left = keyboard.aKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame;
            var right = keyboard.dKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame;

            var confirm = keyboard.enterKey.wasPressedThisFrame;
            var back = keyboard.escapeKey.wasPressedThisFrame;

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
