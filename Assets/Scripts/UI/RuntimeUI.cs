using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Small helpers for building uGUI + TextMeshPro elements from code,
/// so new HUD/menu pieces need no hand edits to MainScene.
/// </summary>
public static class RuntimeUI
{
    public static readonly Color PanelDark = new Color(0.03f, 0.05f, 0.12f, 0.88f);
    public static readonly Color Accent = new Color(0.1f, 0.9f, 1f, 1f);
    public static readonly Color Gold = new Color(1f, 0.82f, 0.25f, 1f);
    public static readonly Color Danger = new Color(1f, 0.3f, 0.25f, 1f);

    public static RectTransform CreateRect(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    public static void Stretch(RectTransform rt, float inset = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }

    /// <summary>Anchors a fixed-size rect at a normalised anchor point (pivot matches anchor).</summary>
    public static void Place(RectTransform rt, Vector2 anchor, Vector2 size, Vector2 offset)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.sizeDelta = size;
        rt.anchoredPosition = offset;
    }

    public static Image CreatePanel(Transform parent, string name, Color color)
    {
        var rt = CreateRect(parent, name);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = true;
        return img;
    }

    public static TextMeshProUGUI CreateText(Transform parent, string name, string text, float size,
        TextAlignmentOptions align = TextAlignmentOptions.Center, Color? color = null, FontStyles style = FontStyles.Normal)
    {
        var rt = CreateRect(parent, name);
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = align;
        tmp.color = color ?? Color.white;
        tmp.fontStyle = style;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        return tmp;
    }

    public static Button CreateButton(Transform parent, string name, string label, UnityAction onClick, Color? color = null)
    {
        var img = CreatePanel(parent, name, color ?? Accent);
        var btn = img.gameObject.AddComponent<Button>();
        var colors = btn.colors;
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        btn.colors = colors;
        if (onClick != null) btn.onClick.AddListener(onClick);
        var text = CreateText(img.transform, "Label", label, 30f, TextAlignmentOptions.Center, new Color(0.02f, 0.05f, 0.1f), FontStyles.Bold);
        Stretch(text.rectTransform);
        return btn;
    }

    /// <summary>Horizontal fill bar (background + fill). Returns the fill image (Filled, horizontal).</summary>
    public static Image CreateBar(Transform parent, string name, Color background, Color fill)
    {
        var bg = CreatePanel(parent, name, background);
        bg.raycastTarget = false;
        var fillImg = CreatePanel(bg.transform, "Fill", fill);
        fillImg.raycastTarget = false;
        Stretch(fillImg.rectTransform, 3f);
        fillImg.type = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Horizontal;
        fillImg.fillOrigin = 0;
        // Filled images need a sprite to render the fill amount correctly.
        fillImg.sprite = WhiteSprite;
        return fillImg;
    }

    private static Sprite s_white;
    public static Sprite WhiteSprite
    {
        get
        {
            if (s_white == null)
            {
                var tex = Texture2D.whiteTexture;
                s_white = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }
            return s_white;
        }
    }

    public static string FormatTime(float seconds)
    {
        int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
        return $"{total / 60:00}:{total % 60:00}";
    }
}
