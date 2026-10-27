using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Display-only HUD. All values (time, health, loot, state) are read from GameStartFlow,
/// which is the single source of truth. Every UI reference is optional and null-safe.
/// </summary>
public class HUDController : MonoBehaviour
{
    [Header("Top Center - Timer")]
    public TMP_Text timerText;
    public Color timerNormalColor = Color.cyan;
    public Color timerWarningColor = new Color(1f, 0.6f, 0f);
    public Color timerDangerColor = Color.red;
    [Tooltip("Timer warning threshold as fraction of max time (e.g. 0.3 = last 30%).")]
    [Range(0f, 1f)] public float warningThreshold = 0.3f;
    [Tooltip("Timer danger threshold as fraction of max time (e.g. 0.1 = last 10%).")]
    [Range(0f, 1f)] public float dangerThreshold = 0.1f;
    [Tooltip("Run length in seconds (read by GameStartFlow).")]
    public float maxGameTimeSeconds = 120f;

    [Header("Top Left - Thief Status")]
    public TMP_Text thiefStatusText;
    public AIThiefLaneDodge thiefReference;

    [Header("Top Right - Loot")]
    public TMP_Text lootText;

    [Header("Bottom Left - Health")]
    public Slider healthSlider;
    public TMP_Text healthText;
    [Tooltip("Claire's maximum health (read by GameStartFlow).")]
    public float maxHealth = 100f;
    [Tooltip("Label above the health bar. Auto-found inside the health bar prefab if left empty.")]
    public TMP_Text healthLabelText;
    public string healthLabel = "CLAIRE";

    [Header("Bottom Center - Alert Feed")]
    public TMP_Text alertText;
    [Tooltip("Seconds an alert stays on screen (standardised at 1.5s).")]
    public float alertDuration = 1.5f;

    [Header("Bottom Right - Controls Hint")]
    public TMP_Text controlsHintText;
    public string controlsHint = "A/D or ← →  move    SPACE  distract    ESC  pause";
    public float hideControlsAfterSeconds = 10f;

    private string _statusOverride;
    private float _alertHideAt;
    private float _hideControlsAt;
    private int _lastShownSeconds = -1;
    private int _lastLoot = -1;
    private float _lastHealth = -1f;
    private string _lastStatus;
    private float _lootPunchUntil;
    private Vector3 _lootBaseScale = Vector3.one;

    private const string LegacyControlsHint = "A/D or <- ->";

    private void Awake()
    {
        // MainScene still stores the old hint text; show the full current controls instead.
        if (string.IsNullOrEmpty(controlsHint) || controlsHint == LegacyControlsHint)
            controlsHint = "A/D move   SPACE distract   ESC pause";
        if (alertText != null) alertText.text = "";
        if (lootText != null) _lootBaseScale = lootText.transform.localScale;
        ResolveHealthLabel();
        RefreshAll(force: true);
    }

    private void Update()
    {
        RefreshAll(force: false);

        if (alertText != null && Time.unscaledTime >= _alertHideAt && !string.IsNullOrEmpty(alertText.text))
            alertText.text = "";

        if (controlsHintText != null && controlsHintText.gameObject.activeSelf && Time.unscaledTime >= _hideControlsAt
            && GameStartFlow.IsGameplayActive)
            controlsHintText.gameObject.SetActive(false);

        if (lootText != null)
        {
            float punch = Mathf.Clamp01((_lootPunchUntil - Time.unscaledTime) / 0.18f);
            lootText.transform.localScale = _lootBaseScale * (1f + 0.25f * punch);
        }
    }

    /// <summary>Called by GameStartFlow when a run starts.</summary>
    public void BeginRun()
    {
        _statusOverride = null;
        _hideControlsAt = Time.unscaledTime + Mathf.Max(0f, hideControlsAfterSeconds);
        if (controlsHintText != null)
        {
            controlsHintText.text = controlsHint;
            controlsHintText.gameObject.SetActive(true);
        }
        if (alertText != null) alertText.text = "";
        RefreshAll(force: true);
    }

    /// <summary>Shows a final status (e.g. "ESCAPED"). Kept for backwards compatibility.</summary>
    public void EndRun(string finalStatus = null)
    {
        _statusOverride = string.IsNullOrEmpty(finalStatus) ? "ENDED" : finalStatus;
        RefreshStatus(force: true);
    }

    /// <summary>Forwards to the authoritative health owner.</summary>
    public void SetHealth(float value)
    {
        var flow = GameStartFlow.Instance;
        if (flow != null) flow.SetClaireHealth(value);
    }

    /// <summary>Forwards to the authoritative health owner. Negative values count as damage.</summary>
    public void AddHealth(float delta)
    {
        var flow = GameStartFlow.Instance;
        if (flow == null) return;
        if (delta < 0f) flow.ApplyClaireDamage(-delta);
        else flow.SetClaireHealth(flow.ClaireHealth + delta);
    }

    public void ShowAlert(string message)
    {
        if (alertText == null) return;
        alertText.text = message;
        _alertHideAt = Time.unscaledTime + Mathf.Max(0.1f, alertDuration);
    }

    private void RefreshAll(bool force)
    {
        RefreshTimer(force);
        RefreshLoot(force);
        RefreshHealth(force);
        RefreshStatus(force);
    }

    private void RefreshTimer(bool force)
    {
        if (timerText == null) return;
        var flow = GameStartFlow.Instance;
        float max = flow != null ? flow.RunDuration : Mathf.Max(1f, maxGameTimeSeconds);
        float remaining = flow != null ? flow.TimeRemaining : max;

        int total = Mathf.CeilToInt(remaining);
        if (force || total != _lastShownSeconds)
        {
            _lastShownSeconds = total;
            timerText.text = $"{total / 60:00}:{total % 60:00}";
        }

        float fraction = remaining / max;
        timerText.color = fraction <= dangerThreshold ? timerDangerColor
            : fraction <= warningThreshold ? timerWarningColor
            : timerNormalColor;
    }

    private void RefreshLoot(bool force)
    {
        if (lootText == null) return;
        var flow = GameStartFlow.Instance;
        int loot = flow != null ? flow.LootValue : CollectiblePickup.RuntimeCollectedValue;
        if (!force && loot == _lastLoot) return;
        if (!force && loot > _lastLoot) _lootPunchUntil = Time.unscaledTime + 0.18f;
        _lastLoot = loot;
        lootText.text = loot.ToString();
    }

    private void RefreshHealth(bool force)
    {
        var flow = GameStartFlow.Instance;
        float max = flow != null ? flow.MaxHealth : Mathf.Max(1f, maxHealth);
        float current = flow != null ? flow.ClaireHealth : max;
        if (!force && Mathf.Approximately(current, _lastHealth)) return;
        _lastHealth = current;

        if (healthSlider != null)
        {
            healthSlider.minValue = 0f;
            healthSlider.maxValue = max;
            healthSlider.value = current;
        }
        if (healthText != null)
            healthText.text = $"{Mathf.CeilToInt(current)}/{Mathf.CeilToInt(max)}";
    }

    private void RefreshStatus(bool force)
    {
        if (thiefStatusText == null) return;
        string status = ComputeStatus();
        if (!force && status == _lastStatus) return;
        _lastStatus = status;
        thiefStatusText.text = status;
    }

    private string ComputeStatus()
    {
        var flow = GameStartFlow.Instance;
        if (flow == null)
            return _statusOverride ?? (thiefReference == null ? "NO THIEF" : thiefReference.IsCaught ? "CAUGHT" : "SAFE");

        switch (flow.State)
        {
            case GameStartFlow.RunState.MainMenu: return "READY";
            case GameStartFlow.RunState.Paused: return "PAUSED";
            case GameStartFlow.RunState.AJCaught: return "AJ CAUGHT!";
            case GameStartFlow.RunState.Results: return _statusOverride ?? GameStartFlow.StatusTextFor(flow.EndReason);
            default:
                if (thiefReference == null) return "NO THIEF";
                return thiefReference.IsCaught ? "AJ CAUGHT!" : "AJ SAFE";
        }
    }

    private void ResolveHealthLabel()
    {
        if (healthLabelText == null && healthSlider != null)
        {
            // The GUI-kit health bar prefab contains a "Character_Name_Text" label (was "Thief Handler").
            Transform t = healthSlider.transform;
            while (t != null && healthLabelText == null)
            {
                var label = FindChildRecursive(t, "Character_Name_Text");
                if (label != null) healthLabelText = label.GetComponentInChildren<TMP_Text>(true);
                t = t.parent;
                if (t != null && t.GetComponent<Canvas>() != null) break;
            }
        }
        if (healthLabelText != null && !string.IsNullOrEmpty(healthLabel))
            healthLabelText.text = healthLabel;
    }

    private static Transform FindChildRecursive(Transform root, string name)
    {
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var found = FindChildRecursive(root.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }
}
