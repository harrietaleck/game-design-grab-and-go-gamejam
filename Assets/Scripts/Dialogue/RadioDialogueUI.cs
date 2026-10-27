using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Radio subtitle box (Task 4.1). Shows one AJ line at a time with a typewriter effect,
/// optional Claire reply options, a radio blip, and ducks the ambience while visible.
/// Built at runtime; uses unscaled time so result-screen commentary still animates.
/// </summary>
public class RadioDialogueUI : MonoBehaviour
{
    public float charactersPerSecond = 55f;
    public float minShowSeconds = 3f;
    public float maxShowSeconds = 7f;
    public float secondsPerCharacter = 0.045f;
    [Range(0f, 1f)] public float ambienceDuck = 0.55f;

    public bool IsShowing => _root != null && _root.activeSelf;

    private GameObject _root;
    private TextMeshProUGUI _speaker;
    private TextMeshProUGUI _line;
    private TextMeshProUGUI _options;
    private CanvasGroup _group;
    private string _fullText = "";
    private float _shownAt;
    private float _hideAt;
    private AudioDirector _audio;

    public void Build(Transform canvasRoot)
    {
        var panel = RuntimeUI.CreatePanel(canvasRoot, "RadioDialogue (runtime)", new Color(0.02f, 0.04f, 0.1f, 0.82f));
        panel.raycastTarget = false;
        // Compact, top-left (under the status text) so it never covers the road or Claire.
        RuntimeUI.Place(panel.rectTransform, new Vector2(0f, 1f), new Vector2(900f, 235f), new Vector2(20f, -78f));
        _group = panel.gameObject.AddComponent<CanvasGroup>();
        _group.blocksRaycasts = false;

        var stripe = RuntimeUI.CreatePanel(panel.transform, "Stripe", RuntimeUI.Accent);
        stripe.raycastTarget = false;
        stripe.rectTransform.anchorMin = new Vector2(0f, 0f);
        stripe.rectTransform.anchorMax = new Vector2(0f, 1f);
        stripe.rectTransform.pivot = new Vector2(0f, 0.5f);
        stripe.rectTransform.sizeDelta = new Vector2(8f, 0f);

        _speaker = RuntimeUI.CreateText(panel.transform, "Speaker", "AJ  •  RADIO", 20f, TextAlignmentOptions.Left, RuntimeUI.Accent, FontStyles.Bold);
        RuntimeUI.Place(_speaker.rectTransform, new Vector2(0f, 1f), new Vector2(860f, 30f), new Vector2(24f, -8f));

        _line = RuntimeUI.CreateText(panel.transform, "Line", "", 22f, TextAlignmentOptions.TopLeft, Color.white);
        RuntimeUI.Place(_line.rectTransform, new Vector2(0f, 1f), new Vector2(860f, 118f), new Vector2(24f, -40f));

        _options = RuntimeUI.CreateText(panel.transform, "Options", "", 19f, TextAlignmentOptions.BottomLeft, RuntimeUI.Gold);
        RuntimeUI.Place(_options.rectTransform, new Vector2(0f, 0f), new Vector2(860f, 66f), new Vector2(24f, 8f));

        _root = panel.gameObject;
        _root.SetActive(false);
        _audio = FindFirstObjectByType<AudioDirector>();
    }

    /// <summary>Moves the box to the top of the screen (used on the results screen so it never covers buttons).</summary>
    public void DockTop(bool top)
    {
        if (_root == null) return;
        var rt = (RectTransform)_root.transform;
        if (top) RuntimeUI.Place(rt, new Vector2(0.5f, 1f), rt.sizeDelta, new Vector2(0f, -20f));
        else RuntimeUI.Place(rt, new Vector2(0f, 1f), rt.sizeDelta, new Vector2(20f, -78f));
    }

    public void Show(string speaker, string line)
    {
        if (_root == null || string.IsNullOrEmpty(line)) return;
        _speaker.text = $"{speaker.ToUpperInvariant()}  •  RADIO";
        _fullText = line;
        _line.text = line;
        _line.maxVisibleCharacters = 0;
        _options.text = "";
        _shownAt = Time.unscaledTime;
        _hideAt = _shownAt + Mathf.Clamp(minShowSeconds + line.Length * secondsPerCharacter, minShowSeconds, maxShowSeconds);
        _root.SetActive(true);
        _root.transform.SetAsLastSibling();
        ProceduralSfx.Play(ProceduralSfx.RadioBlip, 0.5f);
        if (_audio != null) _audio.SetAmbienceDuck(ambienceDuck);
    }

    /// <summary>Shows Claire's reply options under the current line (Task 4.10).</summary>
    public void ShowOptions(string optionsText)
    {
        if (_options != null) _options.text = optionsText ?? "";
    }

    /// <summary>Adds Claire's chosen reply under AJ's line.</summary>
    public void ShowClaireReply(string reply)
    {
        if (_options == null) return;
        _options.text = $"<color=#7FE9FF>CLAIRE:</color> {reply}";
        _hideAt = Mathf.Max(_hideAt, Time.unscaledTime + 2f);
    }

    public void Hide()
    {
        if (_root == null || !_root.activeSelf) return;
        _root.SetActive(false);
        if (_audio != null) _audio.SetAmbienceDuck(1f);
    }

    private void Update()
    {
        if (!IsShowing) return;
        float t = Time.unscaledTime - _shownAt;
        _line.maxVisibleCharacters = Mathf.Min(_fullText.Length, Mathf.FloorToInt(t * charactersPerSecond));
        _group.alpha = Mathf.Clamp01(t / 0.15f) * Mathf.Clamp01((_hideAt - Time.unscaledTime) / 0.3f);
        if (Time.unscaledTime >= _hideAt)
            Hide();
    }

    private void OnDestroy()
    {
        if (_audio != null) _audio.SetAmbienceDuck(1f);
    }
}
