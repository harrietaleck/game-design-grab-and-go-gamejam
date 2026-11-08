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
    [Range(0f, 1f)] public float hitFlashAlpha = 0.75f;
    public float flashDuration = 0.35f;

    private GameStartFlow _flow;
    private Transform _hudRoot;
    private Image _pressureFill;
    private TextMeshProUGUI _pressureLabel;
    private Image _abilityCooldownFill;
    private TextMeshProUGUI _abilityText;
    private Image _flash;
    private float _flashUntil;
    private Color _flashColor;

    // Distances from the canvas edges (canvas units) for the scene's non-UI-anchored HUD groups.
    private const float HealthGroupLeft = 122f;
    private const float HealthGroupBottom = 32f;
    private const float LootGroupRight = 95f;
    private const float LootGroupTop = 67f;

    private Transform _healthGroup;
    private Transform _lootGroup;
    private Vector2 _laidOutSize;
    private Image _statusStripe;
    private Image _healthFill;
    private Texture2D _vignetteTexture;

    private void Awake()
    {
        Instance = this;
        _flow = GetComponent<GameStartFlow>();
        _hudRoot = _flow != null && _flow.hudPanel != null ? _flow.hudPanel.transform : transform;
        Build();
        AnchorSceneHud();
    }

    /// <summary>
    /// MainScene's HUD is positioned from the screen centre for one aspect ratio, so it slides off wider screens.
    /// Pins each element to its screen edge instead.
    /// </summary>
    private void AnchorSceneHud()
    {
        var hud = _flow != null ? _flow.hudController : null;
        if (hud == null) return;
        PinToEdge(hud.timerText, new Vector2(0.5f, 1f), new Vector2(0f, -5f));
        PinToEdge(hud.thiefStatusText, new Vector2(0f, 1f), new Vector2(40f, -6f));
        PinToEdge(hud.alertText, new Vector2(0f, 0f), new Vector2(12f, 72f));
        if (hud.timerText != null) hud.timerText.fontStyle = FontStyles.Bold;
        if (hud.thiefStatusText != null) { hud.thiefStatusText.fontSize = 19f; hud.thiefStatusText.fontStyle = FontStyles.Bold; }
        if (hud.alertText != null)
        {
            hud.alertText.fontStyle = FontStyles.Bold;
            hud.alertText.alignment = TextAlignmentOptions.BottomLeft;
            hud.alertText.rectTransform.sizeDelta = new Vector2(300f, hud.alertText.rectTransform.sizeDelta.y);
            SidePopup.Attach(hud.alertText.rectTransform, fromRight: false);
        }
        BuildHudPanels(hud);
        PinToEdge(hud.controlsHintText, new Vector2(1f, 0f), new Vector2(-12f, -12f));
        if (hud.controlsHintText != null) // wide enough for the whole hint on one line
        {
            hud.controlsHintText.rectTransform.sizeDelta = new Vector2(430f, hud.controlsHintText.rectTransform.sizeDelta.y);
            RuntimeUI.FitOnOneLine(hud.controlsHintText);
        }
        _healthGroup = hud.healthSlider != null ? ChildOfHudRoot(hud.healthSlider.transform) : null;
        _lootGroup = hud.lootText != null ? ChildOfHudRoot(hud.lootText.transform) : null;
    }

    /// <summary>Backing panels that give the timer, pressure meter and AJ's status a clear home on any background.</summary>
    private void BuildHudPanels(HUDController hud)
    {
        var theme = UITheme.Active;
        var top = RuntimeUI.CreateHudPanel(_hudRoot, "TopPanel (runtime)");
        RuntimeUI.Place(top.rectTransform, new Vector2(0.5f, 1f), new Vector2(620f, 200f), new Vector2(0f, 2f));
        top.transform.SetAsFirstSibling();
        if (theme != null)
        {
            RuntimeUI.CreateIcon(top.transform, "TimerIcon", theme.iconTimer, new Vector2(0.5f, 1f), 56f, new Vector2(-225f, -24f));
            var shield = RuntimeUI.CreateIcon(top.transform, "PressureIcon", theme.iconPressure, new Vector2(0.5f, 1f), 40f, new Vector2(-248f, -102f));
            if (shield != null) shield.color = new Color(0.8f, 0.72f, 1f);
        }

        var chip = RuntimeUI.CreateHudPanel(_hudRoot, "StatusChip (runtime)");
        RuntimeUI.Place(chip.rectTransform, new Vector2(0f, 1f), new Vector2(480f, 108f), new Vector2(2f, 6f));
        chip.transform.SetAsFirstSibling();
        if (theme != null)
            _statusStripe = RuntimeUI.CreateIcon(chip.transform, "StatusIcon", theme.iconStatus, new Vector2(0f, 0.5f), 54f, new Vector2(18f, 0f));

        if (hud.healthSlider != null && hud.healthSlider.fillRect != null)
            _healthFill = hud.healthSlider.fillRect.GetComponent<Image>();
    }

    private void UpdateHudColors()
    {
        var hud = _flow != null ? _flow.hudController : null;
        if (hud == null || _flow == null) return;

        bool caught = hud.thiefReference != null && hud.thiefReference.IsCaught;
        Color status = caught ? new Color(1f, 0.25f, 0.2f) : new Color(0.25f, 0.95f, 0.45f);
        if (_statusStripe != null) _statusStripe.color = status;
        if (hud.thiefStatusText != null) hud.thiefStatusText.color = caught ? status : Color.white;

        if (_healthFill != null)
        {
            float health = _flow.ClaireHealth / _flow.MaxHealth;
            _healthFill.color = health > 0.5f
                ? Color.Lerp(new Color(1f, 0.8f, 0.2f), new Color(0.35f, 0.95f, 0.45f), (health - 0.5f) * 2f)
                : Color.Lerp(new Color(1f, 0.2f, 0.15f), new Color(1f, 0.8f, 0.2f), health * 2f);
        }
    }

    /// <summary>A soft edge-only mask, so hit and low-health warnings frame the action instead of covering it.</summary>
    private Sprite CreateVignette()
    {
        const int size = 128;
        _vignetteTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f;
                float dy = (y + 0.5f) / size * 2f - 1f;
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1.25f, Mathf.Sqrt(dx * dx + dy * dy)));
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        _vignetteTexture.SetPixels32(pixels);
        _vignetteTexture.Apply();
        return Sprite.Create(_vignetteTexture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private static void PinToEdge(TMP_Text text, Vector2 anchor, Vector2 offset)
    {
        if (text == null) return;
        var rt = text.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = offset;
    }

    private Transform ChildOfHudRoot(Transform t)
    {
        while (t != null && t.parent != _hudRoot) t = t.parent;
        return t;
    }

    /// <summary>The health and loot groups are plain Transforms (no anchors), so they are placed from the canvas size.</summary>
    private void LayoutSceneGroups()
    {
        if (!(_hudRoot is RectTransform root)) return;
        Vector2 size = root.rect.size;
        if (size == _laidOutSize) return;
        _laidOutSize = size;
        if (_healthGroup != null)
            _healthGroup.localPosition = new Vector3(-size.x * 0.5f + HealthGroupLeft, -size.y * 0.5f + HealthGroupBottom, _healthGroup.localPosition.z);
        if (_lootGroup != null)
            _lootGroup.localPosition = new Vector3(size.x * 0.5f - LootGroupRight, size.y * 0.5f - LootGroupTop, _lootGroup.localPosition.z);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (_vignetteTexture != null) Destroy(_vignetteTexture);
    }
    private void OnEnable() => GameEvents.OnEvent += OnGameEvent;
    private void OnDisable() => GameEvents.OnEvent -= OnGameEvent;

    private void Build()
    {
        // Pressure meter: top centre, under the timer.
        var group = RuntimeUI.CreateRect(_hudRoot, "PolicePressure (runtime)");
        RuntimeUI.Place(group, new Vector2(0.5f, 1f), new Vector2(520f, 72f), new Vector2(0f, -106f));
        _pressureLabel = RuntimeUI.CreateText(group, "Label", "POLICE PRESSURE", 22f, TextAlignmentOptions.Center, Color.white, FontStyles.Bold);
        RuntimeUI.Place(_pressureLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(520f, 28f), Vector2.zero);
        _pressureFill = RuntimeUI.CreateBar(group, "Bar", new Color(0f, 0f, 0f, 0.55f), Color.green);
        RuntimeUI.Place((RectTransform)_pressureFill.transform.parent, new Vector2(0.5f, 0f), new Vector2(520f, 36f), Vector2.zero);

        // Distraction indicator: bottom right, above the controls hint.
        var ability = RuntimeUI.CreateHudPanel(_hudRoot, "DistractionPulse (runtime)");
        RuntimeUI.Place(ability.rectTransform, new Vector2(1f, 0f), new Vector2(560f, 104f), new Vector2(-16f, 96f));
        _abilityCooldownFill = RuntimeUI.CreatePanel(ability.transform, "Cooldown", new Color(0.1f, 0.9f, 1f, 0.25f));
        _abilityCooldownFill.raycastTarget = false;
        RuntimeUI.Stretch(_abilityCooldownFill.rectTransform, 22f);
        if (UITheme.Active != null)
            RuntimeUI.CreateIcon(ability.transform, "AbilityIcon", UITheme.Active.iconAbility, new Vector2(0f, 0.5f), 44f, new Vector2(30f, 0f));
        _abilityCooldownFill.type = Image.Type.Filled;
        _abilityCooldownFill.fillMethod = Image.FillMethod.Horizontal;
        _abilityCooldownFill.sprite = RuntimeUI.WhiteSprite;
        _abilityText = RuntimeUI.CreateText(ability.transform, "Text", "", 24f, TextAlignmentOptions.Center, Color.white, FontStyles.Bold);
        RuntimeUI.Stretch(_abilityText.rectTransform, 4f);
        _abilityText.rectTransform.offsetMin += new Vector2(70f * RuntimeUI.U, 0f);

        // Full-screen flash (top of the canvas, never blocks clicks).
        _flash = RuntimeUI.CreatePanel(transform, "ScreenFlash (runtime)", new Color(1f, 0f, 0f, 0f));
        _flash.raycastTarget = false;
        _flash.sprite = CreateVignette();
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
        if (c.a <= 0f || !GameSettings.ScreenFlash) return;
        _flashColor = c;
        _flashUntil = Time.unscaledTime + flashDuration;
    }

    private void Update()
    {
        LayoutSceneGroups();
        UpdateHudColors();

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
            string dots = new string('●', Mathf.Max(0, pulse.ChargesLeft)) + new string('○', Mathf.Max(0, pulse.TotalCharges - pulse.ChargesLeft));
            _abilityText.text = pulse.CooldownRemaining > 0f
                ? $"DISTRACT  {dots}  <size=80%>{pulse.CooldownRemaining:0.0}s</size>"
                : $"[{(GameInput.UsingGamepad ? "X" : "SPACE")}] DISTRACT  {dots}";
            _abilityText.color = pulse.ChargesLeft > 0 ? Color.white : new Color(1f, 1f, 1f, 0.4f);
            _abilityCooldownFill.fillAmount = 1f - pulse.Cooldown01;
        }

        // Flash fade
        float k = Mathf.Clamp01((_flashUntil - Time.unscaledTime) / Mathf.Max(0.01f, flashDuration));
        var fc = _flashColor; fc.a *= k;

        // Low health: a slow red pulse at the screen edges, under any brighter flash.
        if (GameSettings.ScreenFlash && GameStartFlow.IsGameplayActive && _flow.ClaireHealth <= _flow.MaxHealth * _flow.lowHealthFraction)
        {
            float lowHealthAlpha = 0.3f + 0.2f * Mathf.Sin(Time.unscaledTime * 5f);
            if (lowHealthAlpha > fc.a) fc = new Color(1f, 0.05f, 0.05f, lowHealthAlpha);
        }
        _flash.color = fc;
    }
}
