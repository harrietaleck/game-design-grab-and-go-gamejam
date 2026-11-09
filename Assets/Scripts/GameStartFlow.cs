using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// The single authoritative run controller (Task 1.4).
/// Owns: run state, countdown, Claire's health, loot totals and the one-time run ending.
/// The HUD and every other system only read from / report to this component.
/// (Kept the original class name, fields and Inspector references so MainScene keeps working.)
/// </summary>
public class GameStartFlow : MonoBehaviour
{
    public enum RunState
    {
        MainMenu,
        Playing,
        Paused,
        AJCaught,   // rescue window: gameplay continues, AJ is held
        Results     // terminal: world frozen, results screen shown
    }

    [Header("UI")]
    public GameObject menuPanel;
    public GameObject hudPanel;
    public Button playButton;
    public HUDController hudController;

    [Header("Gameplay Components")]
    [Tooltip("Optional scripts/components to enable only after Play is clicked.")]
    public Behaviour[] enableOnStart;

    [Tooltip("Pause time before Play is clicked.")]
    public bool pauseTimeUntilStart = true;

    [Header("Run Rules")]
    [Tooltip("Run length in seconds. If a HUDController is assigned, its maxGameTimeSeconds is used instead (kept for backwards compatibility).")]
    public float runDurationSeconds = 120f;
    [Tooltip("Claire's starting/maximum health. If a HUDController is assigned, its maxHealth is used instead.")]
    public float claireMaxHealth = 100f;
    [Tooltip("Health at or below this fraction raises a single low-health warning per run.")]
    [Range(0f, 1f)] public float lowHealthFraction = 0.3f;

    [Header("Input")]
    public KeyCode[] startKeys = { KeyCode.Space, KeyCode.Return, KeyCode.KeypadEnter };
    public KeyCode[] pauseKeys = { KeyCode.Escape, KeyCode.P };
    public KeyCode retryKey = KeyCode.R;

    // ---------- Static access ----------
    public static GameStartFlow Instance { get; private set; }
    public static int CurrentRunId { get; private set; }

    /// <summary>True when Claire, AJ, spawners and pickups should advance (Playing or rescue window).</summary>
    public static bool IsGameplayActive =>
        Instance != null && (Instance.State == RunState.Playing || Instance.State == RunState.AJCaught);

    public const string FrontEndSceneName = "FrontEnd";
    public const string GameSceneName = "MainScene";

    // Retry and the front end load the scene and skip its built-in menu.
    private static bool s_autoStartOnLoad;

    public static void RequestAutoStart() => s_autoStartOnLoad = true;

    /// <summary>Lets an open sub-screen (e.g. settings on the pause menu) consume the pause key. Returns true if it did.</summary>
    public Func<bool> PauseBackHandler;

    // ---------- Events ----------
    public event Action<RunState, RunState> StateChanged;       // (old, new)
    public event Action<RunEndReason> RunEnded;
    public event Action<float, float> HealthChanged;             // (current, max)
    public event Action LootChanged;

    // ---------- Authoritative run values ----------
    public RunState State { get; private set; } = RunState.MainMenu;
    public RunEndReason EndReason { get; private set; } = RunEndReason.None;
    public float TimeRemaining { get; private set; }
    public float RunDuration => Mathf.Max(1f, hudController != null ? hudController.maxGameTimeSeconds : runDurationSeconds);
    public float TimeSurvived { get; private set; }
    public float RunProgress01 => Mathf.Clamp01(TimeSurvived / RunDuration);
    public float ClaireHealth { get; private set; }
    public float MaxHealth => Mathf.Max(1f, hudController != null ? hudController.maxHealth : claireMaxHealth);
    public int LootValue { get; private set; }
    public int CoinCount { get; private set; }
    public int GoldStackCount { get; private set; }
    public int JewelCount { get; private set; }
    /// <summary>Obstacles Claire cleared by jumping or sliding this run.</summary>
    public int NearMissCount { get; set; }

    private const float HitProtectionSeconds = 1f;
    private float _protectedUntil;

    /// <summary>True briefly after a hit, so one mistake can't chain into several.</summary>
    public bool IsClaireInvulnerable => IsGameplayActive && Time.time < _protectedUntil;
    public void GrantHitProtection() => _protectedUntil = Time.time + HitProtectionSeconds;
    /// <summary>Pickups worth at least this much count as jewelry.</summary>
    public const int JewelValue = 10;
    /// <summary>Total loot value lost to crashes this run.</summary>
    public int LootLost { get; private set; }
    public int CollisionCount { get; private set; }
    public int RescueSuccessCount { get; set; }
    public int CaptureCount { get; set; }
    public int DistractionsUsed { get; set; }
    public int RunStartFrame { get; private set; } = -1;
    public bool WasSuccessful => EndReason == RunEndReason.TimeUpEscaped || EndReason == RunEndReason.ReachedVan;

    /// <summary>Hook for the police-pressure system (Stage 2). Returns 0 when it is not present.</summary>
    public Func<float> PolicePressureProvider;
    public float PolicePressureValue => PolicePressureProvider != null ? PolicePressureProvider() : 0f;

    private bool _started;
    private bool _ended;
    private bool _lowHealthWarned;
    private RunState _stateBeforePause;
    private float _recentLootWindowStart;
    private int _recentLootCount;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[GameStartFlow] A second run controller was found and disabled. Only one may exist.");
            enabled = false;
            return;
        }
        Instance = this;

        if (playButton != null)
            playButton.onClick.AddListener(StartGame);

        if (menuPanel != null)
            menuPanel.SetActive(true);
        if (hudPanel != null)
            hudPanel.SetActive(false);
        ReplaceOldMenuArt();

        TimeRemaining = RunDuration;
        ClaireHealth = MaxHealth;
        CollectiblePickup.ResetRuntimeCollectedValue();

        SetGameplayEnabled(false);
        if (pauseTimeUntilStart)
            Time.timeScale = 0f;

        if (hudController != null)
            hudController.EndRun("READY");

        RunSubsystemBootstrap();
    }

    private void Start()
    {
        if (s_autoStartOnLoad)
        {
            s_autoStartOnLoad = false;
            StartGame();
        }
        else if (Application.CanStreamedLevelBeLoaded(FrontEndSceneName))
        {
            // Pressing Play in this scene goes through the real menus.
            SceneManager.LoadScene(FrontEndSceneName);
        }
    }

    /// <summary>MainScene's old menu only shows for an instant now, on the way to the front end: show the title art there.</summary>
    private void ReplaceOldMenuArt()
    {
        if (menuPanel == null || !Application.CanStreamedLevelBeLoaded(FrontEndSceneName)) return;
        var art = Resources.Load<Sprite>("SplashArt");
        var image = menuPanel.GetComponent<Image>();
        if (art == null || image == null) return;
        image.sprite = art;
        foreach (Transform child in menuPanel.transform) child.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (playButton != null)
            playButton.onClick.RemoveListener(StartGame);
        if (Instance == this)
            Instance = null;
    }

    /// <summary>Extension point: later systems attach themselves here, so MainScene needs no manual edits.</summary>
    private void RunSubsystemBootstrap()
    {
        foreach (var hook in s_bootstrapHooks)
        {
            try { hook?.Invoke(this); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }

    private static readonly System.Collections.Generic.List<Action<GameStartFlow>> s_bootstrapHooks = new();
    /// <summary>Systems register a callback (from a [RuntimeInitializeOnLoadMethod]) that runs in GameStartFlow.Awake.</summary>
    public static void RegisterBootstrap(Action<GameStartFlow> hook)
    {
        if (hook != null && !s_bootstrapHooks.Contains(hook))
            s_bootstrapHooks.Add(hook);
    }

    private void Update()
    {
        HandleInput();

        if (State == RunState.Playing || State == RunState.AJCaught)
        {
            // Scaled time: the countdown stops while paused.
            TimeSurvived += Time.deltaTime;
            TimeRemaining = Mathf.Max(0f, TimeRemaining - Time.deltaTime);
            // The clock only ends the run as a fallback, if the van mission could not be set up.
            bool mission = RoadblockDirector.Instance != null && RoadblockDirector.Instance.MissionActive;
            if (TimeRemaining <= 0f && !mission)
            {
                GameEvents.Raise(GameEventType.TimeUp);
                EndRun(State == RunState.AJCaught ? RunEndReason.TimeUpWhileCaught : RunEndReason.TimeUpEscaped);
            }
        }
    }

    private void HandleInput()
    {
        switch (State)
        {
            case RunState.MainMenu:
                if (AnyKeyDown(startKeys))
                    StartGame();
                break;
            case RunState.Playing:
            case RunState.AJCaught:
                if (AnyKeyDown(pauseKeys) || GameInput.PausePad)
                    SetPaused(true);
                break;
            case RunState.Paused:
                if ((AnyKeyDown(pauseKeys) || GameInput.PausePad || GameInput.BackPad) && (PauseBackHandler == null || !PauseBackHandler()))
                    SetPaused(false);
                break;
            case RunState.Results:
                if (Input.GetKeyDown(retryKey) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                    Retry();
                else if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.M))
                    ReturnToMenu();
                break;
        }
    }

    private static bool AnyKeyDown(KeyCode[] keys)
    {
        if (keys == null) return false;
        for (int i = 0; i < keys.Length; i++)
            if (Input.GetKeyDown(keys[i])) return true;
        return false;
    }

    // ---------- Run lifecycle ----------

    public void StartGame()
    {
        if (_started || State != RunState.MainMenu)
            return;
        _started = true;
        _ended = false;
        CurrentRunId++;
        RunStartFrame = Time.frameCount;

        TimeRemaining = RunDuration;
        TimeSurvived = 0f;
        NearMissCount = 0;
        _protectedUntil = 0f;
        ClaireHealth = MaxHealth;
        LootValue = CoinCount = GoldStackCount = JewelCount = CollisionCount = LootLost = 0;
        RescueSuccessCount = CaptureCount = DistractionsUsed = 0;
        _lowHealthWarned = false;
        CollectiblePickup.ResetRuntimeCollectedValue();

        Time.timeScale = 1f;
        SetGameplayEnabled(true);

        if (menuPanel != null)
            menuPanel.SetActive(false);
        if (hudPanel != null)
            hudPanel.SetActive(true);

        SetState(RunState.Playing);

        if (hudController != null)
            hudController.BeginRun();

        HealthChanged?.Invoke(ClaireHealth, MaxHealth);
        LootChanged?.Invoke();
        GameEvents.Raise(GameEventType.RunStarted);
    }

    public void SetPaused(bool paused)
    {
        if (paused && (State == RunState.Playing || State == RunState.AJCaught))
        {
            _stateBeforePause = State;
            Time.timeScale = 0f;
            SetState(RunState.Paused);
        }
        else if (!paused && State == RunState.Paused)
        {
            Time.timeScale = 1f;
            SetState(_stateBeforePause);
        }
    }

    /// <summary>Called by the rescue system when AJ is captured during an active run.</summary>
    public bool EnterAJCaught()
    {
        if (State != RunState.Playing)
            return false;
        CaptureCount++;
        SetState(RunState.AJCaught);
        return true;
    }

    /// <summary>Called by the rescue system when Claire frees AJ.</summary>
    public bool ExitAJCaught()
    {
        if (State != RunState.AJCaught)
            return false;
        RescueSuccessCount++;
        SetState(RunState.Playing);
        return true;
    }

    /// <summary>Ends the run exactly once. Every ending path (time, health, failed rescue) goes through here.</summary>
    public void EndRun(RunEndReason reason)
    {
        if (_ended || !_started)
            return;
        _ended = true;
        EndReason = reason;

        SetGameplayEnabled(false);
        Time.timeScale = 0f;   // freeze the world; results UI uses unscaled time
        SetState(RunState.Results);

        if (hudController != null)
            hudController.EndRun(StatusTextFor(reason));

        GameEvents.Raise(WasSuccessful ? GameEventType.RunWon : GameEventType.RunFailed, 0, reason.ToString());
        RunEnded?.Invoke(reason);
    }

    public static string StatusTextFor(RunEndReason reason)
    {
        switch (reason)
        {
            case RunEndReason.TimeUpEscaped:
            case RunEndReason.ReachedVan: return "ESCAPED";
            case RunEndReason.ClaireOutOfHealth: return "CLAIRE DOWN";
            case RunEndReason.AJCaptured: return "BUSTED";
            case RunEndReason.CaughtAtRoadblock: return "CAUGHT";
            case RunEndReason.TimeUpWhileCaught: return "TIME UP";
            default: return "ENDED";
        }
    }

    public void Retry()
    {
        s_autoStartOnLoad = true;
        ReloadScene();
    }

    public void ReturnToMenu()
    {
        if (State != RunState.Results && State != RunState.MainMenu && _started && !_ended)
        {
            // Quitting mid-run still records a single, explicit ending.
            EndRun(RunEndReason.Abandoned);
        }
        s_autoStartOnLoad = false;
        if (Application.CanStreamedLevelBeLoaded(FrontEndSceneName))
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(FrontEndSceneName);
            return;
        }
        ReloadScene();
    }

    private static void ReloadScene()
    {
        Time.timeScale = 1f;
        var scene = SceneManager.GetActiveScene();
        if (scene.buildIndex >= 0)
        {
            SceneManager.LoadScene(scene.buildIndex);
            return;
        }
#if UNITY_EDITOR
        // Scene not in Build Settings yet: still allow restart inside the editor.
        UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(scene.path, new LoadSceneParameters(LoadSceneMode.Single));
#else
        SceneManager.LoadScene(scene.name);
#endif
    }

    private void SetState(RunState next)
    {
        if (State == next) return;
        var old = State;
        State = next;
        StateChanged?.Invoke(old, next);
    }

    // ---------- Health (single source of truth) ----------

    /// <summary>ClaireCollision detail for gunfire, which hurts Claire without raising police pressure.</summary>
    public const string ShotDamageDetail = "shot";

    public void ApplyClaireDamage(float amount, string detail = null)
    {
        if (!IsGameplayActive || amount <= 0f || IsClaireInvulnerable)
            return;
        GrantHitProtection();
        CollisionCount++;
        SetClaireHealth(ClaireHealth - amount);
        // A fatal hit has already ended the run; RunFailed must be the last event of a run.
        if (!_ended)
            GameEvents.Raise(GameEventType.ClaireCollision, Mathf.RoundToInt(amount), detail);
    }

    public void SetClaireHealth(float value)
    {
        if (_ended) return;
        ClaireHealth = Mathf.Clamp(value, 0f, MaxHealth);
        HealthChanged?.Invoke(ClaireHealth, MaxHealth);

        if (!_lowHealthWarned && ClaireHealth > 0f && ClaireHealth <= MaxHealth * lowHealthFraction)
        {
            _lowHealthWarned = true;
            GameEvents.Raise(GameEventType.ClaireLowHealth);
        }

        if (ClaireHealth <= 0f && IsGameplayActive)
            EndRun(RunEndReason.ClaireOutOfHealth);
    }

    // ---------- Loot ----------

    /// <summary>True when a pickup may be collected and scored right now.</summary>
    public static bool CanCollectLoot => IsGameplayActive;

    /// <summary>Registers one collected item. Returns false (and scores nothing) if the run is not active.</summary>
    public bool RegisterLoot(int value)
    {
        if (!CanCollectLoot)
            return false;
        int v = Mathf.Max(1, value);
        LootValue += v;
        if (v >= JewelValue) JewelCount++;
        else if (v >= 5) GoldStackCount++;
        else CoinCount++;
        LootChanged?.Invoke();

        GameEvents.Raise(v >= 5 ? GameEventType.GoldStackCollected : GameEventType.CoinCollected, v);

        // Streak detection for dialogue ("MULTIPLE_LOOT_COLLECTED").
        if (Time.time - _recentLootWindowStart > 3f)
        {
            _recentLootWindowStart = Time.time;
            _recentLootCount = 0;
        }
        _recentLootCount++;
        if (_recentLootCount == 5)
            GameEvents.Raise(GameEventType.MultipleLootCollected, _recentLootCount);
        return true;
    }

    /// <summary>
    /// Removes a fraction of the current loot value (rounded down, so a single coin is kept) and returns how much
    /// was lost. Scores and stats are owned here; visuals and dialogue react to GameEventType.LootSpilled.
    /// </summary>
    /// <summary>Spends up to this many picked-up coins (on healing AJ). Returns how many were actually spent.</summary>
    public int SpendCoins(int amount)
    {
        int spent = Mathf.Clamp(amount, 0, Mathf.Min(CoinCount, LootValue));
        if (spent <= 0) return 0;
        CoinCount -= spent;
        LootValue -= spent;
        CollectiblePickup.SetRuntimeCollectedValue(LootValue);
        LootChanged?.Invoke();
        return spent;
    }

    public int SpillLoot(float fraction)
    {
        if (!IsGameplayActive || LootValue <= 0) return 0;
        int lost = Mathf.FloorToInt(LootValue * Mathf.Clamp01(fraction));
        if (lost <= 0) return 0;
        LootValue -= lost;
        LootLost += lost;
        CollectiblePickup.SetRuntimeCollectedValue(LootValue);
        LootChanged?.Invoke();
        GameEvents.Raise(GameEventType.LootSpilled, lost);
        return lost;
    }

    private void SetGameplayEnabled(bool enabled)
    {
        if (enableOnStart == null)
            return;
        for (int i = 0; i < enableOnStart.Length; i++)
        {
            if (enableOnStart[i] != null)
                enableOnStart[i].enabled = enabled;
        }
    }
}
