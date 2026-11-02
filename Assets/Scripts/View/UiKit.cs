using UnityEngine;
using UnityEngine.UI;

namespace SumoGame
{
    /// <summary>Small helpers shared by the HUD and the menus.</summary>
    public static class UiKit
    {
        public static readonly Color Paper = new Color(0.97f, 0.93f, 0.84f);
        public static readonly Color Ink = new Color(0.08f, 0.07f, 0.06f, 0.82f);
        public static readonly Color Accent = new Color(1f, 0.74f, 0.3f);

        static Font display, body;

        /// <summary>Condensed capitals for titles and round calls.</summary>
        public static Font Display => display != null ? display : display = Resources.Load<Font>("Fonts/BebasNeue-Regular");

        /// <summary>Readable text for everything smaller.</summary>
        public static Font Body => body != null ? body : body = Resources.Load<Font>("Fonts/BarlowCondensed-SemiBold");

        public static Text Text(string name, Transform parent, int size, TextAnchor anchor, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = Body;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        public static Image Image(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static void Place(RectTransform rect, Vector2 anchor, Vector2 size, Vector2 offset)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.sizeDelta = size;
            rect.anchoredPosition = offset;
        }

        /// <summary>Text on a dark panel that fits it, and hides when there is nothing to say.</summary>
        public class Banner
        {
            const float PaddingX = 36f;
            const float PaddingY = 18f;

            public readonly Text Label;
            readonly RectTransform back;
            string shown;

            public Banner(string name, Transform parent, Font font, int size, Color color, Vector2 anchor, Vector2 pivot, Vector2 offset)
            {
                var image = Image(name, parent, new Color(Ink.r, Ink.g, Ink.b, 0.92f));
                back = image.rectTransform;
                back.anchorMin = back.anchorMax = anchor;
                back.pivot = pivot;
                back.anchoredPosition = offset;

                Label = Text("Text", back, size, TextAnchor.MiddleCenter, color);
                Label.font = font;
                Label.rectTransform.anchorMin = Label.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                Label.rectTransform.sizeDelta = new Vector2(1600f, 400f);
                back.gameObject.SetActive(false);
            }

            public void Set(string text)
            {
                if (text == shown) return;
                shown = text;

                back.gameObject.SetActive(text.Length > 0);
                if (text.Length == 0) return;

                Label.text = text;
                back.sizeDelta = new Vector2(Label.preferredWidth + PaddingX * 2f, Label.preferredHeight + PaddingY * 2f);
            }

            public void MoveTo(Vector2 anchor) => back.anchorMin = back.anchorMax = anchor;
        }

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
