using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
    public float maxGameTimeSeconds = 120f;

    [Header("Top Left - Thief Status")]
    public TMP_Text thiefStatusText;
    public AIThiefLaneDodge thiefReference;

    [Header("Top Right - Loot")]
    public TMP_Text lootText;

    [Header("Bottom Left - Health")]
    public Slider healthSlider;
    public TMP_Text healthText;
    public float maxHealth = 100f;

    [Header("Bottom Center - Alert Feed")]
    public TMP_Text alertText;
    public float alertDuration = 0.5f;

    [Header("Bottom Right - Controls Hint")]
    public TMP_Text controlsHintText;
    public string controlsHint = "A/D or <- ->";
    public float hideControlsAfterSeconds = 10f;

    private bool _running;
    private float _elapsedSeconds;
    private float _currentHealth;
    private float _alertHideAt;
    private float _hideControlsAt;

    private void Awake()
    {
        _currentHealth = Mathf.Max(1f, maxHealth);
        RefreshAll();
    }

    private void Update()
    {
        RefreshLoot();

        if (!_running)
            return;

        _elapsedSeconds += Time.unscaledDeltaTime;
        RefreshTimer();
        RefreshThiefStatus();

        if (alertText != null && Time.unscaledTime >= _alertHideAt && !string.IsNullOrEmpty(alertText.text))
            alertText.text = "";

        if (controlsHintText != null && Time.unscaledTime >= _hideControlsAt && controlsHintText.gameObject.activeSelf)
            controlsHintText.gameObject.SetActive(false);
    }

    public void BeginRun()
    {
        _running = true;
        _elapsedSeconds = 0f;
        _currentHealth = Mathf.Max(1f, maxHealth);
        CollectiblePickup.ResetRuntimeCollectedValue();
        _hideControlsAt = Time.unscaledTime + Mathf.Max(0f, hideControlsAfterSeconds);
        
        if (controlsHintText != null)
        {
            controlsHintText.text = controlsHint;
            controlsHintText.gameObject.SetActive(true);
        }

        RefreshAll();
    }

    public void EndRun(string finalStatus = null)
    {
        _running = false;
        if (thiefStatusText != null)
            thiefStatusText.text = string.IsNullOrEmpty(finalStatus) ? "ENDED" : finalStatus;
    }

    public void SetHealth(float value)
    {
        _currentHealth = Mathf.Clamp(value, 0f, Mathf.Max(1f, maxHealth));
        RefreshHealth();
    }

    public void AddHealth(float delta)
    {
        SetHealth(_currentHealth + delta);
    }

    public void ShowAlert(string message)
    {
        if (alertText != null)
        {
            alertText.text = message;
            _alertHideAt = Time.unscaledTime + Mathf.Max(0.1f, alertDuration);
        }
    }

    private void RefreshAll()
    {
        RefreshLoot();
        RefreshTimer();
        RefreshHealth();
        RefreshThiefStatus();
    }

    private void RefreshLoot()
    {
        if (lootText != null)
            lootText.text = CollectiblePickup.RuntimeCollectedValue.ToString();
    }

    private void RefreshTimer()
    {
        if (timerText == null)
            return;

        float remaining = Mathf.Max(0f, maxGameTimeSeconds - _elapsedSeconds);
        int total = Mathf.CeilToInt(remaining);
        int minutes = total / 60;
        int seconds = total % 60;
        timerText.text = $"{minutes:00}:{seconds:00}";

        float fraction = remaining / Mathf.Max(1f, maxGameTimeSeconds);
        if (fraction <= dangerThreshold)
            timerText.color = timerDangerColor;
        else if (fraction <= warningThreshold)
            timerText.color = timerWarningColor;
        else
            timerText.color = timerNormalColor;

        if (remaining <= 0f && _running)
        {
            _running = false;
            EndRun("TIME UP");
        }
    }

    private void RefreshThiefStatus()
    {
        if (thiefStatusText == null)
            return;

        if (thiefReference == null)
        {
            thiefStatusText.text = "NO THIEF";
            return;
        }

        if (thiefReference.IsCaught)
        {
            thiefStatusText.text = "CAUGHT";
            return;
        }

        thiefStatusText.text = "SAFE";
    }

    private void RefreshHealth()
    {
        if (healthSlider != null)
        {
            healthSlider.minValue = 0f;
            healthSlider.maxValue = Mathf.Max(1f, maxHealth);
            healthSlider.value = _currentHealth;
        }

        if (healthText != null)
            healthText.text = Mathf.CeilToInt(_currentHealth).ToString();
    }
}
