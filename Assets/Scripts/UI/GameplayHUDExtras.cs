using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Extra HUD built at runtime under the existing HUD panel: police-pressure meter,
/// Distraction Pulse indicator and full-screen hit/ability flashes.
/// Lives on the canvas object (always active) so it never misses events while the HUD is hidden.
/// </summary>
public class GameplayHUDExtras : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        GameStartFlow.RegisterBootstrap(flow =>
        {
            if (flow.GetComponent<GameplayHUDExtras>() == null)
                flow.gameObject.AddComponent<GameplayHUDExtras>();
        });
    }

    public static GameplayHUDExtras Instance { get; private set; }

    [Header("Feedback")]
    [Tooltip("Alpha of the red screen flash when Claire is hit (0 disables).")]
    [Range(0f, 1f)] public float hitFlashAlpha = 0.28f;
    public float flashDuration = 0.25f;

    private GameStartFlow _flow;
    private Transform _hudRoot;
    private Image _pressureFill;
    private TextMeshProUGUI _pressureLabel;
    private Image _abilityCooldownFill;
    private TextMeshProUGUI _abilityText;
    private Image _flash;
    private float _flashUntil;
    private Color _flashColor;

    private void Awake()
    {
        Instance = this;
        _flow = GetComponent<GameStartFlow>();
        _hudRoot = _flow != null && _flow.hudPanel != null ? _flow.hudPanel.transform : transform;
        Build();
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }
    private void OnEnable() => GameEvents.OnEvent += OnGameEvent;
    private void OnDisable() => GameEvents.OnEvent -= OnGameEvent;

    private void Build()
    {
        // Pressure meter: top centre, under the timer.
        var group = RuntimeUI.CreateRect(_hudRoot, "PolicePressure (runtime)");
        RuntimeUI.Place(group, new Vector2(0.5f, 1f), new Vector2(440f, 62f), new Vector2(0f, -105f));
        _pressureLabel = RuntimeUI.CreateText(group, "Label", "POLICE PRESSURE", 22f, TextAlignmentOptions.Center, Color.white, FontStyles.Bold);
        RuntimeUI.Place(_pressureLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(440f, 26f), Vector2.zero);
        _pressureFill = RuntimeUI.CreateBar(group, "Bar", new Color(0f, 0f, 0f, 0.55f), Color.green);
        RuntimeUI.Place((RectTransform)_pressureFill.transform.parent, new Vector2(0.5f, 0f), new Vector2(440f, 26f), Vector2.zero);

        // Distraction indicator: bottom right, above the controls hint.
        var ability = RuntimeUI.CreatePanel(_hudRoot, "DistractionPulse (runtime)", new Color(0f, 0f, 0f, 0.45f));
        ability.raycastTarget = false;
        RuntimeUI.Place(ability.rectTransform, new Vector2(1f, 0f), new Vector2(330f, 64f), new Vector2(-24f, 110f));
        _abilityCooldownFill = RuntimeUI.CreatePanel(ability.transform, "Cooldown", new Color(0.1f, 0.9f, 1f, 0.25f));
        _abilityCooldownFill.raycastTarget = false;
        RuntimeUI.Stretch(_abilityCooldownFill.rectTransform);
        _abilityCooldownFill.type = Image.Type.Filled;
        _abilityCooldownFill.fillMethod = Image.FillMethod.Horizontal;
        _abilityCooldownFill.sprite = RuntimeUI.WhiteSprite;
        _abilityText = RuntimeUI.CreateText(ability.transform, "Text", "", 24f, TextAlignmentOptions.Center, Color.white, FontStyles.Bold);
        RuntimeUI.Stretch(_abilityText.rectTransform, 4f);

        // Full-screen flash (top of the canvas, never blocks clicks).
        _flash = RuntimeUI.CreatePanel(transform, "ScreenFlash (runtime)", new Color(1f, 0f, 0f, 0f));
        _flash.raycastTarget = false;
        RuntimeUI.Stretch(_flash.rectTransform);
    }

    private void OnGameEvent(GameEventData e)
    {
        switch (e.type)
        {
            case GameEventType.ClaireCollision:
                Flash(new Color(1f, 0.1f, 0.05f, hitFlashAlpha));
                break;
            case GameEventType.DistractionUsed:
                Flash(new Color(0.1f, 0.9f, 1f, hitFlashAlpha * 0.8f));
                break;
        }
    }

    public void Flash(Color c)
    {
        if (c.a <= 0f) return;
        _flashColor = c;
        _flashUntil = Time.unscaledTime + flashDuration;
    }

    private void Update()
    {
        // Pressure meter
        var pressure = PolicePressure.Instance;
        float p = pressure != null ? pressure.Normalized : 0f;
        _pressureFill.fillAmount = Mathf.MoveTowards(_pressureFill.fillAmount, p, Time.unscaledDeltaTime * 2f);
        Color c = p < 0.5f ? Color.Lerp(new Color(0.2f, 0.9f, 0.3f), new Color(1f, 0.75f, 0.1f), p / 0.5f)
                           : Color.Lerp(new Color(1f, 0.75f, 0.1f), new Color(1f, 0.15f, 0.1f), (p - 0.5f) / 0.5f);
        if (pressure != null && pressure.IsHigh)
            c = Color.Lerp(c, Color.white, 0.35f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 12f)));
        _pressureFill.color = c;
        if (pressure != null)
            _pressureLabel.text = pressure.GainSuppressed ? "POLICE DISTRACTED" : pressure.IsHigh ? "POLICE PRESSURE  HIGH!" : "POLICE PRESSURE";

        // Ability indicator
        var pulse = DistractionPulse.Instance;
        if (pulse != null)
        {
            string dots = new string('●', Mathf.Max(0, pulse.ChargesLeft)) + new string('○', Mathf.Max(0, pulse.chargesPerRun - pulse.ChargesLeft));
            _abilityText.text = pulse.CooldownRemaining > 0f
                ? $"DISTRACT  {dots}  <size=80%>{pulse.CooldownRemaining:0.0}s</size>"
                : $"[SPACE] DISTRACT  {dots}";
            _abilityText.color = pulse.ChargesLeft > 0 ? Color.white : new Color(1f, 1f, 1f, 0.4f);
            _abilityCooldownFill.fillAmount = 1f - pulse.Cooldown01;
        }

        // Flash fade
        float k = Mathf.Clamp01((_flashUntil - Time.unscaledTime) / Mathf.Max(0.01f, flashDuration));
        var fc = _flashColor; fc.a *= k;
        _flash.color = fc;
    }
}
