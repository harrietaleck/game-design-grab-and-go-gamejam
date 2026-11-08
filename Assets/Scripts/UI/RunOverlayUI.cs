using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pause overlay and results screen, built at runtime on the gameplay canvas.
/// Reads everything from GameStartFlow; Retry/Menu buttons call GameStartFlow.
/// </summary>
public class RunOverlayUI : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        GameStartFlow.RegisterBootstrap(flow =>
        {
            if (flow.GetComponent<RunOverlayUI>() == null)
                flow.gameObject.AddComponent<RunOverlayUI>();
        });
    }

    /// <summary>Optional extra lines for the results screen (e.g. personal bests). Set by other systems.</summary>
    public static System.Func<GameStartFlow, string> ExtraResultsLines;

    private GameStartFlow _flow;
    private GameObject _pausePanel;
    private GameObject _resultsPanel;
    private FancyText _resultsTitle;
    private TextMeshProUGUI _resultsReason;
    private TextMeshProUGUI _resultsStats;
    private TextMeshProUGUI _resultsExtra;
    private Button _retryButton;
    private CanvasGroup _resultsGroup;
    private float _resultsShownAt;

    private void Awake()
    {
        _flow = GetComponent<GameStartFlow>();
        MakeCanvasResponsive();
        Build();
    }

    /// <summary>
    /// MainScene's canvas used Constant Pixel Size, so the HUD was tiny at 1080p and larger.
    /// Scale with screen size using the canvas's own 800x600 reference keeps the original layout
    /// (designed at roughly that size) and makes it readable at every Windows resolution.
    /// </summary>
    private void MakeCanvasResponsive()
    {
        var scaler = GetComponent<UnityEngine.UI.CanvasScaler>();
        if (scaler == null) return;
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(800f, 600f);
        scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
    }

    private void OnEnable()
    {
        if (_flow != null) _flow.StateChanged += OnStateChanged;
    }

    private void OnDisable()
    {
        if (_flow != null) _flow.StateChanged -= OnStateChanged;
    }

    private void Update()
    {
        if (_resultsGroup != null && _resultsPanel.activeSelf)
            _resultsGroup.alpha = Mathf.Clamp01((Time.unscaledTime - _resultsShownAt) / 0.35f);
    }

    private void Build()
    {
        Transform root = transform; // GamePlayCanvas

        // ---- Pause ----
        var pause = RuntimeUI.CreatePanel(root, "PauseOverlay (runtime)", new Color(0f, 0f, 0f, 0.6f));
        RuntimeUI.Stretch(pause.rectTransform);
        FancyText.Create(pause.transform, "PausedText", "PAUSED", 120f, UITheme.Fancy.Neon,
            new Vector2(0.5f, 0.5f), new Vector2(1200f, 220f), new Vector2(0f, 60f));
        var pauseHint = RuntimeUI.CreateText(pause.transform, "PausedHint", "ESC / P to resume", 36f,
            TextAlignmentOptions.Center, RuntimeUI.Accent, FontStyles.Bold);
        RuntimeUI.Place(pauseHint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(1000f, 80f), new Vector2(0f, -90f));
        _pausePanel = pause.gameObject;
        _pausePanel.SetActive(false);

        // ---- Results ----
        var dim = RuntimeUI.CreatePanel(root, "ResultsScreen (runtime)", new Color(0f, 0f, 0f, 0.55f));
        RuntimeUI.Stretch(dim.rectTransform);
        _resultsGroup = dim.gameObject.AddComponent<CanvasGroup>();
        _resultsPanel = dim.gameObject;

        var card = RuntimeUI.CreateWindow(dim.transform, "Card", null, 0.7f);
        RuntimeUI.Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(1000f, 1080f), Vector2.zero);

        _resultsTitle = FancyText.Create(card.transform, "Title", "GETAWAY", 64f, UITheme.Fancy.Gold,
            new Vector2(0.5f, 1f), new Vector2(960f, 100f), new Vector2(0f, -45f));

        _resultsReason = RuntimeUI.CreateText(card.transform, "Reason", "", 28f, TextAlignmentOptions.Center, Color.white);
        RuntimeUI.Place(_resultsReason.rectTransform, new Vector2(0.5f, 1f), new Vector2(940f, 100f), new Vector2(0f, -130f));

        _resultsStats = RuntimeUI.CreateText(card.transform, "Stats", "", 30f, TextAlignmentOptions.TopLeft, Color.white);
        RuntimeUI.Place(_resultsStats.rectTransform, new Vector2(0.5f, 1f), new Vector2(600f, 520f), new Vector2(0f, -250f));

        _resultsExtra = RuntimeUI.CreateText(card.transform, "Extra", "", 26f, TextAlignmentOptions.Center, RuntimeUI.Gold);
        RuntimeUI.Place(_resultsExtra.rectTransform, new Vector2(0.5f, 0f), new Vector2(940f, 120f), new Vector2(0f, 140f));

        _retryButton = RuntimeUI.CreateButton(card.transform, "RetryButton", "RETRY  (R)", OnRetry);
        RuntimeUI.Place((RectTransform)_retryButton.transform, new Vector2(0.5f, 0f), new Vector2(300f, 70f), new Vector2(-165f, 40f));
        var menuButton = RuntimeUI.CreateButton(card.transform, "MenuButton", "MENU  (ESC)", OnMenu, new Color(0.85f, 0.85f, 0.9f));
        RuntimeUI.Place((RectTransform)menuButton.transform, new Vector2(0.5f, 0f), new Vector2(300f, 70f), new Vector2(165f, 40f));

        _resultsPanel.SetActive(false);
    }

    private void OnStateChanged(GameStartFlow.RunState oldState, GameStartFlow.RunState newState)
    {
        _pausePanel.SetActive(newState == GameStartFlow.RunState.Paused);
        _pausePanel.transform.SetAsLastSibling();

        if (newState == GameStartFlow.RunState.Results)
            StartCoroutine(ShowResultsNextFrame());
    }

    // Wait one frame so every RunEnded listener (e.g. personal bests) has finished first.
    private System.Collections.IEnumerator ShowResultsNextFrame()
    {
        yield return null;
        ShowResults();
    }

    private void ShowResults()
    {
        var f = _flow;
        bool won = f.WasSuccessful;
        _resultsTitle.SetStyle(won ? UITheme.Fancy.Gold : UITheme.Fancy.Purple);
        _resultsTitle.SetText(won ? "GETAWAY SUCCESSFUL!" : "GETAWAY FAILED");
        _resultsReason.text = ReasonText(f.EndReason);

        const string col = "<pos=60%>"; // a fixed value column; tab stops vary with label width
        _resultsStats.text =
            $"Loot value{col}<b>{f.LootValue}</b>\n" +
            $"Coins{col}<b>{f.CoinCount}</b>\n" +
            $"Gold stacks{col}<b>{f.GoldStackCount}</b>\n" +
            $"Loot lost{col}<b>{f.LootLost}</b>\n" +
            $"Time survived{col}<b>{RuntimeUI.FormatTime(f.TimeSurvived)}</b>\n" +
            $"Claire's health{col}<b>{Mathf.CeilToInt(f.ClaireHealth)}/{Mathf.CeilToInt(f.MaxHealth)}</b>\n" +
            $"Hits taken{col}<b>{f.CollisionCount}</b>\n" +
            $"Rescues{col}<b>{f.RescueSuccessCount}/{f.CaptureCount}</b>";

        string extra = null;
        try { extra = ExtraResultsLines?.Invoke(f); } catch (System.Exception e) { Debug.LogException(e); }
        _resultsExtra.text = extra ?? "";

        _resultsShownAt = Time.unscaledTime;
        _resultsGroup.alpha = 0f;
        _resultsPanel.SetActive(true);
        _resultsPanel.transform.SetAsLastSibling();
        if (_retryButton != null) _retryButton.Select();
    }

    public static string ReasonText(RunEndReason reason)
    {
        switch (reason)
        {
            case RunEndReason.TimeUpEscaped: return "Claire kept AJ free until the clock ran out. Clean getaway.";
            case RunEndReason.ClaireOutOfHealth: return "Claire took too many hits and couldn't keep up.";
            case RunEndReason.AJCaptured: return "Claire missed AJ during the rescue. He was caught.";
            case RunEndReason.TimeUpWhileCaught: return "Time ran out while AJ was still caught.";
            case RunEndReason.Abandoned: return "Run abandoned.";
            default: return "";
        }
    }

    private void OnRetry() { if (_flow != null) _flow.Retry(); }
    private void OnMenu() { if (_flow != null) _flow.ReturnToMenu(); }
}
