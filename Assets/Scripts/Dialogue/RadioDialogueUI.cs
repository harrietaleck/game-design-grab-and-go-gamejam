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

    // Authoring size (1920x1080 units); RuntimeUI.Place converts it. Never re-use rt.sizeDelta (already converted).
    private static readonly Vector2 BoxSize = new Vector2(780f, 270f);
    private static readonly Vector2 DockedTop = new Vector2(20f, -112f);
    private static readonly Vector2 DockedBelowTimer = new Vector2(20f, -210f); // on narrow screens the timer panel reaches this far left
    private const float TimerPanelHalfWidth = 130f; // canvas units

    private GameObject _root;
    private TextMeshProUGUI _speaker;
    private TextMeshProUGUI _line;
    private TextMeshProUGUI _options;
    private CanvasGroup _group;
    private string _fullText = "";
    private float _shownAt;
    private float _hideAt;
    private AudioDirector _audio;
    private Image _liveDot;
    private TextMeshProUGUI _liveViewers;
    private float _viewers = 240f;
    private const float ViewersPerSecond = 45f;
    private AudioSource _static;
    private AudioClip _policeRadio;
    private AudioClip _socialSound;
    private const float SocialSoundVolume = 0.9f;
    private const float PoliceRadioTalking = 1f; // the recording is quiet
    private const float PoliceRadioIdle = 0.6f;
    private float _replyTalkUntil;
    private const float StaticTalking = 0.2f;
    private const float StaticIdle = 0.06f;
    private const float ReplyTalkSeconds = 1.2f;

    public void Build(Transform canvasRoot)
    {
        var panel = RuntimeUI.CreateHudPanel(canvasRoot, "RadioDialogue (runtime)");
        // Compact, top-left (under the status text) so it never covers the road or Claire.
        RuntimeUI.Place(panel.rectTransform, new Vector2(0f, 1f), BoxSize, DockedTop);
        _group = panel.gameObject.AddComponent<CanvasGroup>();
        _group.blocksRaycasts = false;

        var stripe = RuntimeUI.CreatePanel(panel.transform, "Stripe", RuntimeUI.Accent);
        stripe.raycastTarget = false;
        // Everything sits inside the frame art, which has a margin of about 30 units around its edge.
        stripe.rectTransform.anchorMin = new Vector2(0f, 0f);
        stripe.rectTransform.anchorMax = new Vector2(0f, 1f);
        stripe.rectTransform.pivot = new Vector2(0f, 0.5f);
        stripe.rectTransform.offsetMin = new Vector2(30f, 30f) * RuntimeUI.U;
        stripe.rectTransform.offsetMax = new Vector2(38f, -30f) * RuntimeUI.U;

        _speaker = RuntimeUI.CreateText(panel.transform, "Speaker", "AJ  •  RADIO", 20f, TextAlignmentOptions.Left, RuntimeUI.Accent, FontStyles.Bold);
        RuntimeUI.Place(_speaker.rectTransform, new Vector2(0f, 1f), new Vector2(690f, 34f), new Vector2(54f, -32f));

        _line = RuntimeUI.CreateText(panel.transform, "Line", "", 22f, TextAlignmentOptions.TopLeft, Color.white);
        RuntimeUI.Place(_line.rectTransform, new Vector2(0f, 1f), new Vector2(690f, 110f), new Vector2(54f, -68f));

        _options = RuntimeUI.CreateText(panel.transform, "Options", "", 19f, TextAlignmentOptions.BottomLeft, RuntimeUI.Gold);
        RuntimeUI.Place(_options.rectTransform, new Vector2(0f, 0f), new Vector2(690f, 62f), new Vector2(54f, 28f));

        BuildLivePhone(panel.rectTransform);

        _root = panel.gameObject;
        _root.SetActive(false);
        _audio = FindFirstObjectByType<AudioDirector>();
    }

    /// <summary>A small sideways phone hanging under the radio box, showing AJ streaming the getaway.</summary>
    private void BuildLivePhone(RectTransform box)
    {
        var body = RuntimeUI.CreatePanel(box, "AJ Live Phone", new Color(0.05f, 0.05f, 0.08f, 1f));
        body.raycastTarget = false;
        var rt = body.rectTransform;
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(330f, 104f) * RuntimeUI.U;
        rt.anchoredPosition = new Vector2(44f, 26f) * RuntimeUI.U;

        var screen = RuntimeUI.CreatePanel(body.transform, "Screen", new Color(0.24f, 0.08f, 0.36f, 1f));
        screen.raycastTarget = false;
        RuntimeUI.Stretch(screen.rectTransform, 9f);

        _liveDot = RuntimeUI.CreatePanel(screen.transform, "Live Badge", new Color(0.95f, 0.1f, 0.2f, 1f));
        _liveDot.raycastTarget = false;
        RuntimeUI.Place(_liveDot.rectTransform, new Vector2(0f, 1f), new Vector2(92f, 38f), new Vector2(10f, -8f));
        var live = RuntimeUI.CreateText(_liveDot.transform, "Text", "LIVE", 20f, TextAlignmentOptions.Center, Color.white, FontStyles.Bold);
        RuntimeUI.Stretch(live.rectTransform);
        RuntimeUI.FitOnOneLine(live);

        _liveViewers = RuntimeUI.CreateText(screen.transform, "Viewers", "", 19f, TextAlignmentOptions.Right, Color.white, FontStyles.Bold);
        RuntimeUI.Place(_liveViewers.rectTransform, new Vector2(1f, 1f), new Vector2(180f, 38f), new Vector2(-10f, -8f));
        RuntimeUI.FitOnOneLine(_liveViewers);

        var handle = RuntimeUI.CreateText(screen.transform, "Handle", "AJ is live on his phone", 18f, TextAlignmentOptions.Left, new Color(1f, 0.8f, 0.95f));
        RuntimeUI.Place(handle.rectTransform, new Vector2(0f, 0f), new Vector2(290f, 32f), new Vector2(10f, 6f));
        RuntimeUI.FitOnOneLine(handle);
    }

    private void UpdateLivePhone()
    {
        if (_liveDot == null) return;
        Color c = _liveDot.color;
        c.a = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 3.5f));
        _liveDot.color = c;

        // Viewers pile in as the chase goes on.
        _viewers += Time.unscaledDeltaTime * ViewersPerSecond * Random.Range(0.4f, 1.6f);
        int shown = Mathf.FloorToInt(_viewers);
        _liveViewers.text = shown >= 1000 ? $"{shown / 1000f:0.0}K watching" : $"{shown} watching";
    }

    /// <summary>Keeps the box clear of the timer panel, whatever the screen shape.</summary>
    private void PlaceClearOfTimer()
    {
        var rt = (RectTransform)_root.transform;
        float canvasWidth = rt.parent is RectTransform parent ? parent.rect.width : 800f;
        float boxRight = (DockedTop.x + BoxSize.x) * RuntimeUI.U;
        bool fits = boxRight < canvasWidth * 0.5f - TimerPanelHalfWidth;
        RuntimeUI.Place(rt, new Vector2(0f, 1f), BoxSize, fits ? DockedTop : DockedBelowTimer);
    }

    public void Show(string speaker, string line)
    {
        if (_root == null || string.IsNullOrEmpty(line)) return;
        PlaceClearOfTimer();
        _speaker.text = $"{speaker.ToUpperInvariant()}  •  RADIO";
        _fullText = line;
        _line.text = line;
        _line.maxVisibleCharacters = 0;
        _options.text = "";
        _shownAt = Time.unscaledTime;
        _hideAt = _shownAt + Mathf.Clamp(minShowSeconds + line.Length * secondsPerCharacter, minShowSeconds, maxShowSeconds);
        _root.SetActive(true);
        _root.transform.SetAsLastSibling();
        ProceduralSfx.Play(ProceduralSfx.RadioOpen, 0.75f);
        if (_socialSound == null) _socialSound = Resources.Load<AudioClip>("social-media-sound");
        ProceduralSfx.Play(_socialSound, SocialSoundVolume); // AJ's phone going off as he streams
        StartStatic();
        if (_audio != null) _audio.SetAmbienceDuck(ambienceDuck);
    }

    private void StartStatic()
    {
        if (_static == null)
        {
            _static = gameObject.AddComponent<AudioSource>();
            _policeRadio = Resources.Load<AudioClip>("police-radio");
            _static.clip = _policeRadio != null ? _policeRadio : ProceduralSfx.RadioStatic;
            _static.loop = true;
            _static.playOnAwake = false;
            _static.spatialBlend = 0f;
            _static.ignoreListenerPause = true;
        }
        if (_static.isPlaying) return; // a new line during a conversation carries on from the same chatter

        _static.volume = 0f;
        // Each conversation picks up somewhere different in the recording, so it never sounds repeated.
        if (_policeRadio != null) _static.time = Random.Range(0f, Mathf.Max(0f, _policeRadio.length - 1f));
        _static.Play();
    }

    private void UpdateStatic()
    {
        if (_static == null) return;
        bool talking = _line.maxVisibleCharacters < _fullText.Length || Time.unscaledTime < _replyTalkUntil;
        float level = _policeRadio != null ? (talking ? PoliceRadioTalking : PoliceRadioIdle) : (talking ? StaticTalking : StaticIdle);
        float target = level * GameSettings.SfxVolume * _group.alpha;
        _static.volume = Mathf.MoveTowards(_static.volume, target, Time.unscaledDeltaTime * 2f);
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
        _replyTalkUntil = Time.unscaledTime + ReplyTalkSeconds;
        ProceduralSfx.Play(ProceduralSfx.RadioOpen, 0.75f);
    }

    public void Hide()
    {
        if (_root == null || !_root.activeSelf) return;
        _root.SetActive(false);
        if (_static != null) _static.Stop();
        ProceduralSfx.Play(ProceduralSfx.RadioClose, 0.6f);
        if (_audio != null) _audio.SetAmbienceDuck(1f);
    }

    private void Update()
    {
        if (!IsShowing) return;
        float t = Time.unscaledTime - _shownAt;
        _line.maxVisibleCharacters = Mathf.Min(_fullText.Length, Mathf.FloorToInt(t * charactersPerSecond));
        _group.alpha = Mathf.Clamp01(t / 0.15f) * Mathf.Clamp01((_hideAt - Time.unscaledTime) / 0.3f);
        UpdateStatic();
        UpdateLivePhone();
        if (Time.unscaledTime >= _hideAt)
            Hide();
    }

    private void OnDestroy()
    {
        if (_audio != null) _audio.SetAmbienceDuck(1f);
    }
}
