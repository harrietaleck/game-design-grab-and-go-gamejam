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
    private TextMeshProUGUI _resultsTitle;
    private TextMeshProUGUI _resultsReason;
    private TextMeshProUGUI _resultsStats;
    private TextMeshProUGUI _resultsExtra;
    private Button _retryButton;
    private CanvasGroup _resultsGroup;
    private float _resultsShownAt;

    private void Awake()
    {
        _flow = GetComponent<GameStartFlow>();
        Build();
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
        var pauseText = RuntimeUI.CreateText(pause.transform, "PausedText", "PAUSED\n<size=40%>ESC / P to resume</size>", 90f,
            TextAlignmentOptions.Center, RuntimeUI.Accent, FontStyles.Bold);
        RuntimeUI.Stretch(pauseText.rectTransform);
        _pausePanel = pause.gameObject;
        _pausePanel.SetActive(false);

        // ---- Results ----
        var dim = RuntimeUI.CreatePanel(root, "ResultsScreen (runtime)", new Color(0f, 0f, 0f, 0.55f));
        RuntimeUI.Stretch(dim.rectTransform);
        _resultsGroup = dim.gameObject.AddComponent<CanvasGroup>();
        _resultsPanel = dim.gameObject;

        var card = RuntimeUI.CreatePanel(dim.transform, "Card", RuntimeUI.PanelDark);
        RuntimeUI.Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(760f, 760f), Vector2.zero);

        _resultsTitle = RuntimeUI.CreateText(card.transform, "Title", "GETAWAY", 64f, TextAlignmentOptions.Center, RuntimeUI.Accent, FontStyles.Bold);
        RuntimeUI.Place(_resultsTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(720f, 90f), new Vector2(0f, -30f));

        _resultsReason = RuntimeUI.CreateText(card.transform, "Reason", "", 30f, TextAlignmentOptions.Center, Color.white);
        RuntimeUI.Place(_resultsReason.rectTransform, new Vector2(0.5f, 1f), new Vector2(700f, 80f), new Vector2(0f, -120f));

        _resultsStats = RuntimeUI.CreateText(card.transform, "Stats", "", 32f, TextAlignmentOptions.Left, Color.white);
        RuntimeUI.Place(_resultsStats.rectTransform, new Vector2(0.5f, 1f), new Vector2(560f, 330f), new Vector2(0f, -210f));
        _resultsStats.lineSpacing = 8f;

        _resultsExtra = RuntimeUI.CreateText(card.transform, "Extra", "", 26f, TextAlignmentOptions.Center, RuntimeUI.Gold);
        RuntimeUI.Place(_resultsExtra.rectTransform, new Vector2(0.5f, 0f), new Vector2(700f, 90f), new Vector2(0f, 135f));

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
            ShowResults();
    }

    private void ShowResults()
    {
        var f = _flow;
        bool won = f.WasSuccessful;
        _resultsTitle.text = won ? "GETAWAY SUCCESSFUL!" : "GETAWAY FAILED";
        _resultsTitle.color = won ? RuntimeUI.Accent : RuntimeUI.Danger;
        _resultsReason.text = ReasonText(f.EndReason);

        _resultsStats.text =
            $"Loot value\t<b>{f.LootValue}</b>\n" +
            $"Coins\t\t<b>{f.CoinCount}</b>\n" +
            $"Gold stacks\t<b>{f.GoldStackCount}</b>\n" +
            $"Time survived\t<b>{RuntimeUI.FormatTime(f.TimeSurvived)}</b>\n" +
            $"Claire's health\t<b>{Mathf.CeilToInt(f.ClaireHealth)}/{Mathf.CeilToInt(f.MaxHealth)}</b>\n" +
            $"Hits taken\t<b>{f.CollisionCount}</b>\n" +
            $"Rescues\t\t<b>{f.RescueSuccessCount}/{f.CaptureCount}</b>";

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
