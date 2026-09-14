using System;
using ANL.Core.Content;
using ANL.Runtime.Assets;
using ANL.Runtime.Persistence;
using ANL.Runtime.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ANL.UI
{
    public sealed class UiKit
    {
        public TMP_FontAsset Font;
        public static TMP_FontAsset DefaultFont;
        public static MonoBehaviour CoroutineHost;
        public static int TextSpeedIndex = 1;
        public CatalogAssetProvider Assets;

        public Transform BackgroundLayer;
        public Transform PortraitLayer;
        public Transform ContentLayer;
        public Transform DialogueLayer;
        public Transform HudLayer;
        public Transform ModalLayer;
        public Transform DevLayer;

        public UiKit(Transform background, Transform portrait, Transform content, Transform dialogue, Transform hud, Transform modal, Transform dev, TMP_FontAsset font)
        {
            BackgroundLayer = background;
            PortraitLayer = portrait;
            ContentLayer = content;
            DialogueLayer = dialogue;
            HudLayer = hud;
            ModalLayer = modal;
            DevLayer = dev;
            Font = font;
            if (font != null) DefaultFont = font;
        }

        public static float CharsPerSecond
        {
            get
            {
                switch (TextSpeedIndex)
                {
                    case 0: return 22f;
                    case 2: return 90f;
                    default: return 45f;
                }
            }
        }

        public Sprite Sprite(string id)
        {
            return Assets != null ? Assets.Get(id) : null;
        }

        public AssetEntry Entry(string id)
        {
            return Assets != null ? Assets.GetEntry(id) : null;
        }

        public static string Ui(IContentIndex content, string id, string fallback)
        {
            if (content == null) return fallback;
            var text = content.Ui(id);
            if (string.IsNullOrEmpty(text) || text == id) return fallback;
            return text;
        }

        public static GameObject Panel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.preserveAspect = false;
            return go;
        }

        public static GameObject Panel(Transform parent, string name, Sprite sprite, Color tint)
        {
            var go = Panel(parent, name, tint);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = false;
            return go;
        }

        public static TextMeshProUGUI Label(Transform parent, string text, float size, Color color, TextAlignmentOptions align = TextAlignmentOptions.TopLeft)
        {
            return Label(parent, text, size, color, align, true);
        }

        public static TextMeshProUGUI Label(Transform parent, string text, float size, Color color, TextAlignmentOptions align, bool wrap)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            if (DefaultFont != null) tmp.font = DefaultFont;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Truncate;
            tmp.richText = true;
            return tmp;
        }

        public static Button TextButton(Transform parent, string label, float size, Action onClick, Color? background = null)
        {
            return TextButton(parent, label, size, onClick, background, 260f, 64f);
        }

        public static Button TextButton(Transform parent, string label, float size, Action onClick, Color? background, float width, float height)
        {
            var go = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = background.HasValue ? background.Value : new Color(0.14f, 0.16f, 0.22f, 0.96f);
            image.preserveAspect = false;
            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.86f, 0.89f, 1f, 1f);
            colors.pressedColor = new Color(0.7f, 0.74f, 0.86f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.55f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            var text = Label(go.transform, label, size, Color.white, TextAlignmentOptions.Center);
            Stretch(text.gameObject);
            text.rectTransform.offsetMin = new Vector2(10f, 6f);
            text.rectTransform.offsetMax = new Vector2(-10f, -6f);
            if (onClick != null) button.onClick.AddListener(() => onClick());
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(width, height);
            return button;
        }

        public static Button HitArea(GameObject go, Action onClick)
        {
            var button = go.GetComponent<Button>();
            if (button == null) button = go.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            if (button.targetGraphic == null) button.targetGraphic = go.GetComponent<Graphic>();
            if (onClick != null) button.onClick.AddListener(() => onClick());
            return button;
        }

        public static GameObject SetRect(GameObject go, float x, float y, float width, float height)
        {
            var rect = go.GetComponent<RectTransform>();
            if (rect == null) rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, -y);
            return go;
        }

        public static void Stretch(GameObject go)
        {
            var rect = go.GetComponent<RectTransform>();
            if (rect == null) rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        public static void Cover(GameObject go, float areaWidth, float areaHeight)
        {
            var image = go.GetComponent<Image>();
            var rect = go.GetComponent<RectTransform>();
            if (rect == null) return;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            if (image == null || image.sprite == null)
            {
                rect.sizeDelta = new Vector2(areaWidth, areaHeight);
                return;
            }
            var size = image.sprite.rect.size;
            if (size.x <= 0f || size.y <= 0f)
            {
                rect.sizeDelta = new Vector2(areaWidth, areaHeight);
                return;
            }
            float scale = Mathf.Max(areaWidth / size.x, areaHeight / size.y);
            rect.sizeDelta = new Vector2(size.x * scale, size.y * scale);
            image.preserveAspect = false;
        }

        public static void Contain(GameObject go, float maxWidth, float maxHeight)
        {
            var image = go.GetComponent<Image>();
            var rect = go.GetComponent<RectTransform>();
            if (rect == null) return;
            if (image == null || image.sprite == null)
            {
                rect.sizeDelta = new Vector2(maxWidth, maxHeight);
                return;
            }
            var size = image.sprite.rect.size;
            if (size.x <= 0f || size.y <= 0f)
            {
                rect.sizeDelta = new Vector2(maxWidth, maxHeight);
                return;
            }
            float scale = Mathf.Min(maxWidth / size.x, maxHeight / size.y);
            rect.sizeDelta = new Vector2(size.x * scale, size.y * scale);
            image.preserveAspect = false;
        }

        public static ScrollRect Scroll(Transform parent, string name, out RectTransform content)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(ScrollRect));
            go.transform.SetParent(parent, false);
            Stretch(go);

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewport.transform.SetParent(go.transform, false);
            Stretch(viewport);
            var viewportImage = viewport.GetComponent<Image>();
            viewportImage.color = Color.white;
            var mask = viewport.GetComponent<Mask>();
            mask.showMaskGraphic = false;

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(viewport.transform, false);
            content = contentGo.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;

            var layout = contentGo.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 8f;
            layout.padding = new RectOffset(12, 12, 12, 12);
            var fitter = contentGo.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = go.GetComponent<ScrollRect>();
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            return scroll;
        }

        public static void SetFontSize(TextMeshProUGUI text, float size)
        {
            if (text != null) text.fontSize = size;
        }

        public static void ClearChildren(Transform parent)
        {
            if (parent == null) return;
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i).gameObject;
                child.transform.SetParent(null, false);
                UnityEngine.Object.Destroy(child);
            }
        }

        public static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public static GameObject Dim(Transform parent, string name, Action onClick = null)
        {
            var go = Panel(parent, name, new Color(0f, 0f, 0f, 0.72f));
            Stretch(go);
            if (onClick != null) HitArea(go, onClick);
            return go;
        }
    }

    internal static class SaveUi
    {
        public static bool IsCorrupt(LoadOutcome outcome)
        {
            return outcome == LoadOutcome.Corrupt;
        }

        public static bool IsIncompatible(LoadOutcome outcome)
        {
            return outcome == LoadOutcome.Incompatible;
        }

        public static string Describe(LoadOutcome outcome)
        {
            switch (outcome)
            {
                case LoadOutcome.Missing: return "missing";
                case LoadOutcome.Corrupt: return "corrupt";
                case LoadOutcome.Incompatible: return "incompatible";
                case LoadOutcome.UnknownIds: return "unknown_ids";
                default: return "";
            }
        }

        public static string SavedAt(GameSession session, string slotId)
        {
            if (session == null || session.Saves == null) return "";
            return session.Saves.ReadSlotTimestamp(slotId);
        }
    }
}
