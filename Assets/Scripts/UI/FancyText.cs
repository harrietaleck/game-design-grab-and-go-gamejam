using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Headline text using an "MK Easy Text" baked effect font (gold bevel / cyan neon / purple gloss) on a legacy UI Text.
/// Those fonts are pre-rendered bitmaps (native size 140), so size is applied with scale, not fontSize.
/// Falls back to a coloured TextMeshPro label when the font is missing.
/// </summary>
public class FancyText : MonoBehaviour
{
    private Text _legacy;
    private TextMeshProUGUI _tmp;
    private UITheme.Fancy _style;
    private float _size;
    private Vector2 _box;

    public static FancyText Create(Transform parent, string name, string text, float size, UITheme.Fancy style,
                                   Vector2 anchor, Vector2 box, Vector2 offset)
    {
        var rt = RuntimeUI.CreateRect(parent, name);
        RuntimeUI.Place(rt, anchor, box, offset);
        var fancy = rt.gameObject.AddComponent<FancyText>();
        fancy._size = size;
        fancy._box = box;
        fancy.Build(style);
        fancy.SetText(text);
        return fancy;
    }

    private void Build(UITheme.Fancy style)
    {
        _style = style;
        var theme = UITheme.Active;
        var font = theme != null ? theme.FancyFont(style) : null;
        if (font != null)
        {
            if (_tmp != null) { Destroy(_tmp.gameObject); _tmp = null; }
            if (_legacy == null)
            {
                var child = RuntimeUI.CreateRect(transform, "MK Easy Text");
                _legacy = child.gameObject.AddComponent<Text>();
                _legacy.alignment = TextAnchor.MiddleCenter;
                _legacy.horizontalOverflow = HorizontalWrapMode.Overflow;
                _legacy.verticalOverflow = VerticalWrapMode.Overflow;
                _legacy.supportRichText = false;
                _legacy.raycastTarget = false;
            }
            _legacy.font = font;
            _legacy.material = null;                 // use the font's own baked-effect material
            _legacy.color = Color.white;
            int native = Mathf.Max(1, font.fontSize > 0 ? font.fontSize : 140);
            _legacy.fontSize = native;
            float scale = (_size * RuntimeUI.F) / native;
            var rt = _legacy.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.localScale = Vector3.one * scale;
            rt.sizeDelta = (_box * RuntimeUI.U) / Mathf.Max(0.0001f, scale);
            rt.anchoredPosition = Vector2.zero;
        }
        else
        {
            if (_legacy != null) { Destroy(_legacy.gameObject); _legacy = null; }
            if (_tmp == null)
            {
                _tmp = RuntimeUI.CreateText(transform, "Text", "", _size, TextAlignmentOptions.Center, Color.white, FontStyles.Bold);
                RuntimeUI.Stretch(_tmp.rectTransform);
            }
            _tmp.color = style == UITheme.Fancy.Gold ? RuntimeUI.Gold : style == UITheme.Fancy.Neon ? RuntimeUI.Accent : new Color(0.85f, 0.45f, 1f);
        }
    }

    public void SetText(string text)
    {
        if (_legacy != null) _legacy.text = text;
        if (_tmp != null) _tmp.text = text;
    }

    public void SetStyle(UITheme.Fancy style)
    {
        if (style != _style) Build(style);
    }
}
