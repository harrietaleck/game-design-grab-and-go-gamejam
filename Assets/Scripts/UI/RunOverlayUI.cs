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
    private GameObject _pauseMenu;
    private GameObject _pauseSettings;
    private Button _resumeButton;
    private GameObject _resultsPanel;
    private GameObject _resultsCard;
    private GameObject _resultsScoreboard;
    private FancyText _resultsTitle;
    private TextMeshProUGUI _resultsReason;
    private TextMeshProUGUI[] _statValues;
    private TextMeshProUGUI _resultsExtra;
    private string _resultsComment;
    private Button _retryButton;
    private Button _nextButton;
    private CanvasGroup _resultsGroup;
    private float _resultsShownAt;

    private void Awake()
    {
        _flow = GetComponent<GameStartFlow>();
        gameObject.AddComponent<GamepadMenuFocus>();
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
        {
            _resultsGroup.alpha = Mathf.Clamp01((Time.unscaledTime - _resultsShownAt) / 0.35f);
            // A player mashing A to jump must not press Retry the instant the run ends.
            _resultsGroup.interactable = Time.unscaledTime - _resultsShownAt > 0.8f;
            if (_resultsGroup.interactable && _resultsCard.activeSelf && _nextButton.gameObject.activeSelf && Input.GetKeyDown(KeyCode.N))
                OnNextDifficulty();
        }
    }

    private void Build()
    {
        Transform root = transform; // GamePlayCanvas

        // ---- Pause ----
        var pause = RuntimeUI.CreatePanel(root, "PauseOverlay (runtime)", new Color(0f, 0f, 0f, 0.6f));
        RuntimeUI.Stretch(pause.rectTransform);
        _pauseMenu = RuntimeUI.CreateRect(pause.transform, "PauseMenu").gameObject;
        RuntimeUI.Stretch((RectTransform)_pauseMenu.transform);
        FancyText.Create(_pauseMenu.transform, "PausedText", "PAUSED", 110f, UITheme.Fancy.Neon,
            new Vector2(0.5f, 1f), new Vector2(1200f, 190f), new Vector2(0f, -110f));

        var buttons = new (string label, UnityEngine.Events.UnityAction action)[]
        {
            ("RESUME", OnResume), ("RETRY", OnRetry), ("SETTINGS", OpenPauseSettings), ("HOME", OnMenu), ("QUIT", GameNavigation.Quit)
        };
        for (int i = 0; i < buttons.Length; i++)
        {
            var b = RuntimeUI.CreateButton(_pauseMenu.transform, buttons[i].label + " Button", buttons[i].label, buttons[i].action);
            RuntimeUI.Place((RectTransform)b.transform, new Vector2(0.5f, 1f), new Vector2(520f, 88f), new Vector2(0f, -340f - i * 104f));
            if (i == 0) _resumeButton = b;
        }

        _pauseSettings = SharedScreens.BuildSettings(pause.transform, ClosePauseSettings);
        _pauseSettings.SetActive(false);
        _flow.PauseBackHandler = () =>
        {
            if (!_pauseSettings.activeSelf) return false;
            ClosePauseSettings();
            return true;
        };

        _pausePanel = pause.gameObject;
        _pausePanel.SetActive(false);

        // ---- Results ----
        var dim = RuntimeUI.CreatePanel(root, "ResultsScreen (runtime)", new Color(0f, 0f, 0f, 0.55f));
        RuntimeUI.Stretch(dim.rectTransform);
        _resultsGroup = dim.gameObject.AddComponent<CanvasGroup>();
        _resultsPanel = dim.gameObject;

        // A near-opaque backing: names floating over the 3D scene must not show through the numbers.
        var card = RuntimeUI.CreateWindow(dim.transform, "Card", null, 0.94f);
        RuntimeUI.Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(1240f, 940f), Vector2.zero);

        _resultsTitle = FancyText.Create(card.transform, "Title", "GETAWAY", 64f, UITheme.Fancy.Gold,
            new Vector2(0.5f, 1f), new Vector2(1120f, 100f), new Vector2(0f, -40f));

        _resultsReason = RuntimeUI.CreateText(card.transform, "Reason", "", 28f, TextAlignmentOptions.Center, Color.white);
        RuntimeUI.Place(_resultsReason.rectTransform, new Vector2(0.5f, 1f), new Vector2(1120f, 96f), new Vector2(0f, -142f));
        _resultsReason.fontSizeMax = _resultsReason.fontSize;
        _resultsReason.fontSizeMin = 9f;
        _resultsReason.enableAutoSizing = true;

        _statValues = new TextMeshProUGUI[StatLabels.Length];
        for (int i = 0; i < StatLabels.Length; i++)
            _statValues[i] = BuildStatRow(card.transform, i);

        _resultsExtra = RuntimeUI.CreateText(card.transform, "Extra", "", 26f, TextAlignmentOptions.Center, RuntimeUI.Gold);
        RuntimeUI.Place(_resultsExtra.rectTransform, new Vector2(0.5f, 1f), new Vector2(1120f, 110f), new Vector2(0f, -558f));
        _resultsExtra.fontSizeMax = _resultsExtra.fontSize;
        _resultsExtra.fontSizeMin = 9f;
        _resultsExtra.enableAutoSizing = true;

        var grey = new Color(0.85f, 0.85f, 0.9f);
        _nextButton = RuntimeUI.CreateButton(card.transform, "NextDifficultyButton", "NEXT", OnNextDifficulty);
        RuntimeUI.Place((RectTransform)_nextButton.transform, new Vector2(0.5f, 0f), new Vector2(550f, 80f), new Vector2(-285f, 130f));
        _retryButton = RuntimeUI.CreateButton(card.transform, "RetryButton", "RETRY  (R)", OnRetry);
        RuntimeUI.Place((RectTransform)_retryButton.transform, new Vector2(0.5f, 0f), new Vector2(550f, 80f), new Vector2(285f, 130f));
        var homeButton = RuntimeUI.CreateButton(card.transform, "HomeButton", "HOME  (ESC)", OnMenu, grey);
        RuntimeUI.Place((RectTransform)homeButton.transform, new Vector2(0.5f, 0f), new Vector2(365f, 80f), new Vector2(-385f, 36f));
        var scoresButton = RuntimeUI.CreateButton(card.transform, "ScoreboardButton", "SCOREBOARD", OpenResultsScoreboard, grey);
        RuntimeUI.Place((RectTransform)scoresButton.transform, new Vector2(0.5f, 0f), new Vector2(365f, 80f), new Vector2(0f, 36f));
        var quitButton = RuntimeUI.CreateButton(card.transform, "QuitButton", "QUIT", GameNavigation.Quit, grey);
        RuntimeUI.Place((RectTransform)quitButton.transform, new Vector2(0.5f, 0f), new Vector2(365f, 80f), new Vector2(385f, 36f));

        _resultsCard = card.gameObject;
        _resultsScoreboard = SharedScreens.BuildScoreboard(dim.transform, CloseResultsScoreboard, allowClear: false);
        _resultsScoreboard.SetActive(false);

        _resultsPanel.SetActive(false);
    }

    private static readonly string[] StatLabels =
    {
        "Loot value", "Coins", "Gold / jewelry", "Loot lost", "Rating",
        "Roadblocks", "Time survived", "Claire's health", "Hits taken", "Rescues"
    };
    private const int RowsPerColumn = 5;

    /// <summary>One row of the two-column table; label and value have their own boxes so they can never collide.</summary>
    private static TextMeshProUGUI BuildStatRow(Transform card, int index)
    {
        int row = index % RowsPerColumn;
        float x = index < RowsPerColumn ? -285f : 285f;
        var back = RuntimeUI.CreatePanel(card, "Stat " + StatLabels[index], new Color(1f, 1f, 1f, row % 2 == 0 ? 0.07f : 0f));
        back.raycastTarget = false;
        RuntimeUI.Place(back.rectTransform, new Vector2(0.5f, 1f), new Vector2(550f, 60f), new Vector2(x, -246f - row * 60f));

        var label = RuntimeUI.CreateText(back.transform, "Label", StatLabels[index], 28f, TextAlignmentOptions.Left, Color.white);
        RuntimeUI.Place(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(330f, 54f), new Vector2(16f, 0f));
        RuntimeUI.FitOnOneLine(label);
        var value = RuntimeUI.CreateText(back.transform, "Value", "", 28f, TextAlignmentOptions.Right, Color.white, FontStyles.Bold);
        RuntimeUI.Place(value.rectTransform, new Vector2(1f, 0.5f), new Vector2(190f, 54f), new Vector2(-16f, 0f));
        RuntimeUI.FitOnOneLine(value);
        return value;
    }

    private void OnStateChanged(GameStartFlow.RunState oldState, GameStartFlow.RunState newState)
    {
        bool paused = newState == GameStartFlow.RunState.Paused;
        _pausePanel.SetActive(paused);
        _pausePanel.transform.SetAsLastSibling();
        if (paused)
        {
            _pauseSettings.SetActive(false);
            _pauseMenu.SetActive(true);
            _resumeButton.Select();
        }

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
        RefreshReason();

        // Same order as StatLabels.
        int roadblocks = RoadblockDirector.Instance != null ? RoadblockDirector.Instance.RoadblocksPassed : 0;
        string[] values =
        {
            f.LootValue.ToString(), f.CoinCount.ToString(), $"{f.GoldStackCount} / {f.JewelCount}", f.LootLost.ToString(),
            $"{ProgressionStore.Stars(f)}/3 STARS",
            $"{roadblocks}/{RoadblockDirector.TotalRoadblocks}", RuntimeUI.FormatTime(f.TimeSurvived),
            $"{Mathf.CeilToInt(f.ClaireHealth)}/{Mathf.CeilToInt(f.MaxHealth)}", f.CollisionCount.ToString(),
            $"{f.RescueSuccessCount}/{f.CaptureCount}"
        };
        for (int i = 0; i < values.Length; i++) _statValues[i].text = values[i];

        // A win offers the next difficulty, except on Hard, where there is none.
        bool hasNext = won && GameSettings.Difficulty != GameSettings.DifficultyLevel.Hard;
        _nextButton.gameObject.SetActive(hasNext);
        if (hasNext)
            _nextButton.GetComponentInChildren<TMP_Text>().text = $"NEXT: {NextDifficulty().ToString().ToUpperInvariant()}  (N)";
        RuntimeUI.Place((RectTransform)_retryButton.transform, new Vector2(0.5f, 0f), new Vector2(550f, 80f), new Vector2(hasNext ? 285f : 0f, 130f));

        string extra = null;
        try { extra = ExtraResultsLines?.Invoke(f); } catch (System.Exception e) { Debug.LogException(e); }
        _resultsExtra.text = extra ?? "";

        _resultsShownAt = Time.unscaledTime;
        _resultsGroup.alpha = 0f;
        _resultsPanel.SetActive(true);
        _resultsPanel.transform.SetAsLastSibling();
        if (_retryButton != null) _retryButton.Select();
    }

    /// <summary>AJ's last word goes inside the results card, so the radio box never sits on top of it.</summary>
    public void SetResultsComment(string speaker, string line)
    {
        _resultsComment = string.IsNullOrEmpty(line) ? null : $"{speaker}: \"{line}\"";
        RefreshReason();
    }

    private void RefreshReason()
    {
        if (_flow == null) return;
        string reason = ReasonText(_flow.EndReason);
        _resultsReason.text = _resultsComment == null ? reason : $"{reason}\n<size=80%><i>{_resultsComment}</i></size>";
    }

    private static GameSettings.DifficultyLevel NextDifficulty() =>
        GameSettings.Difficulty == GameSettings.DifficultyLevel.Easy ? GameSettings.DifficultyLevel.Normal : GameSettings.DifficultyLevel.Hard;

    private void OnNextDifficulty()
    {
        GameSettings.Difficulty = NextDifficulty();
        GameSettings.Save();
        OnRetry();
    }

    public static string ReasonText(RunEndReason reason)
    {
        switch (reason)
        {
            case RunEndReason.ReachedVan: return "Five roadblocks down. Claire and AJ made it to the van.";
            case RunEndReason.TimeUpEscaped: return "Claire kept AJ free until the clock ran out. Clean getaway.";
            case RunEndReason.ClaireOutOfHealth: return "Claire took too many hits and couldn't keep up.";
            case RunEndReason.AJCaptured: return "Claire missed AJ during the rescue. He was caught.";
            case RunEndReason.TimeUpWhileCaught: return "Time ran out while AJ was still caught.";
            case RunEndReason.Abandoned: return "Run abandoned.";
            default: return "";
        }
    }

    private void OnResume() { if (_flow != null) _flow.SetPaused(false); }

    private void OpenPauseSettings()
    {
        _pauseMenu.SetActive(false);
        _pauseSettings.SetActive(true);
    }

    private void ClosePauseSettings()
    {
        GameSettings.Save();
        _pauseSettings.SetActive(false);
        _pauseMenu.SetActive(true);
        _resumeButton.Select();
    }

    private void OpenResultsScoreboard()
    {
        _resultsCard.SetActive(false);
        _resultsScoreboard.SetActive(true);
    }

    private void CloseResultsScoreboard()
    {
        _resultsScoreboard.SetActive(false);
        _resultsCard.SetActive(true);
        _retryButton.Select();
    }

    private void OnRetry() { if (_flow != null) _flow.Retry(); }
    private void OnMenu() { if (_flow != null) _flow.ReturnToMenu(); }
}
