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
    /// <summary>
    /// Runtime UI is authored in 1920x1080 units; the gameplay canvas uses an 800x600 reference
    /// (see RunOverlayUI.MakeCanvasResponsive), so every size, offset and font is multiplied by this.
    /// </summary>
    public const float U = 0.42f;
    /// <summary>Font multiplier (slightly larger than U so text matches the original 16-24pt HUD fonts).</summary>
    public const float F = 0.6f;

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
        rt.offsetMin = new Vector2(inset, inset) * U;
        rt.offsetMax = new Vector2(-inset, -inset) * U;
    }

    /// <summary>Anchors a fixed-size rect at a normalised anchor point (pivot matches anchor).</summary>
    public static void Place(RectTransform rt, Vector2 anchor, Vector2 size, Vector2 offset)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.sizeDelta = size * U;
        rt.anchoredPosition = offset * U;
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
        tmp.fontSize = size * F;
        tmp.alignment = align;
        tmp.color = color ?? Color.white;
        tmp.fontStyle = style;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        ApplyThemeFont(tmp);
        return tmp;
    }

    /// <summary>Switches a TMP text to the Sci-Fi UI font when the theme provides it.</summary>
    public static void ApplyThemeFont(TMP_Text text)
    {
        if (text == null) return;
        var theme = UITheme.Active;
        var font = theme != null ? theme.HudFontAsset : null;
        if (font != null) text.font = font;
    }

    /// <summary>
    /// Sci-Fi glass window (9-sliced frame + dark backing for readability). Falls back to a flat panel.
    /// Children added to the returned image draw on top of the window.
    /// </summary>
    public static Image CreateWindow(Transform parent, string name, Color? glassTint = null, float backingAlpha = 0.55f)
    {
        var theme = UITheme.Active;
        var sprite = theme != null ? theme.window.GetSprite() : null;
        if (sprite == null)
            return CreatePanel(parent, name, PanelDark);

        var frame = CreatePanel(parent, name, glassTint ?? new Color(0.75f, 0.92f, 1f, 1f));
        frame.sprite = sprite;
        frame.type = Image.Type.Sliced;
        frame.pixelsPerUnitMultiplier = theme.window.borderScale;
        var backing = CreatePanel(frame.transform, "Backing", new Color(0.01f, 0.03f, 0.07f, backingAlpha));
        backing.raycastTarget = false;
        Stretch(backing.rectTransform, 22f);
        return frame;
    }

    public static Button CreateButton(Transform parent, string name, string label, UnityAction onClick, Color? color = null)
    {
        var theme = UITheme.Active;
        var normal = theme != null ? theme.button.GetSprite() : null;
        var img = CreatePanel(parent, name, color ?? Accent);
        var btn = img.gameObject.AddComponent<Button>();
        Color labelColor = new Color(0.02f, 0.05f, 0.1f);
        if (normal != null)
        {
            // Sci-Fi UI button art with the active state on hover/press/selection.
            img.sprite = normal;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = theme.button.borderScale;
            img.color = color.HasValue && color.Value != Accent ? Color.Lerp(Color.white, color.Value, 0.35f) : Color.white;
            var active = theme.buttonActive.GetSprite();
            btn.transition = Selectable.Transition.SpriteSwap;
            btn.spriteState = new SpriteState { highlightedSprite = active, pressedSprite = active, selectedSprite = active };
            labelColor = Color.white;
        }
        else
        {
            var colors = btn.colors;
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            btn.colors = colors;
        }
        if (onClick != null) btn.onClick.AddListener(onClick);
        var text = CreateText(img.transform, "Label", label, 30f, TextAlignmentOptions.Center, labelColor, FontStyles.Bold);
        Stretch(text.rectTransform, normal != null ? 14f : 0f);
        return btn;
    }

    /// <summary>Horizontal fill bar (background + fill). Returns the fill image (Filled, horizontal).</summary>
    public static Image CreateBar(Transform parent, string name, Color background, Color fill)
    {
        var theme = UITheme.Active;
        var frameSprite = theme != null ? theme.barFrame.GetSprite() : null;
        var fillSprite = theme != null ? theme.barFill.GetSprite() : null;

        var bg = CreatePanel(parent, name, frameSprite != null ? Color.white : background);
        bg.raycastTarget = false;
        if (frameSprite != null)
        {
            bg.sprite = frameSprite;
            bg.type = Image.Type.Sliced;
            bg.pixelsPerUnitMultiplier = theme.barFrame.borderScale;
        }
        var fillImg = CreatePanel(bg.transform, "Fill", fill);
        fillImg.raycastTarget = false;
        Stretch(fillImg.rectTransform, frameSprite != null ? 6f : 3f);
        fillImg.type = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Horizontal;
        fillImg.fillOrigin = 0;
        // Filled images need a sprite; the theme's light capsule tints nicely to any colour.
        fillImg.sprite = fillSprite != null ? fillSprite : WhiteSprite;
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
