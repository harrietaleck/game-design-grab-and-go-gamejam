using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Stage 3 rescue window. When AJ is caught (by an obstacle with no way out, or by maxed police pressure),
/// he stops in his lane. Claire keeps running towards him and must be in AJ's lane when she reaches him.
///   - Same lane on arrival  -> rescue succeeds, AJ sprints back to his lead and the run continues.
///   - Wrong lane on arrival -> rescue fails and the run ends (AJ captured).
/// The window is the time Claire needs to reach AJ (at least minRescueWindowSeconds).
/// Health reaching 0 or the timer running out during the window are handled by GameStartFlow.
/// </summary>
public class RescueController : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        GameStartFlow.RegisterBootstrap(flow =>
        {
            if (flow.GetComponent<RescueController>() == null)
                flow.gameObject.AddComponent<RescueController>();
        });
    }

    [Tooltip("Minimum seconds Claire gets to line up with AJ. If he is closer than this, he is placed further up the road.")]
    public float minRescueWindowSeconds = 3f;
    [Tooltip("Claire 'reaches' AJ when she is this close behind him (Z).")]
    public float reachDistance = 0.6f;
    [Tooltip("AJ's lead over Claire to restore after a rescue.")]
    public float recoveryGap = 10f;
    [Tooltip("Seconds AJ ignores obstacles right after a rescue.")]
    public float postRescueInvulnerability = 2.5f;

    public const string CauseExhausted = "exhausted"; // police pressure maxed out
    private const int RescueCoinCost = 10;
    private static readonly int HitState = Animator.StringToHash("Hit");
    private static readonly int TiredState = Animator.StringToHash("Tired");
    private bool _tiredRun;

    private GameStartFlow _flow;
    private AIThiefLaneDodge _aj;
    private PlayerLaneMovement _claire;
    private Animator _ajAnimator;
    private float _windowTotalDistance;
    private bool _active;

    private GameObject _banner;
    private TextMeshProUGUI _bannerText;
    private Image _bannerFill;

    private void Awake()
    {
        _flow = GetComponent<GameStartFlow>();
        BuildBanner();
    }

    private void Start()
    {
        _aj = FindFirstObjectByType<AIThiefLaneDodge>();
        _claire = FindFirstObjectByType<PlayerLaneMovement>();
        if (_aj != null) _ajAnimator = _aj.GetComponentInChildren<Animator>();
    }

    private void OnEnable() => GameEvents.OnEvent += OnGameEvent;
    private void OnDisable() => GameEvents.OnEvent -= OnGameEvent;

    private void OnGameEvent(GameEventData e)
    {
        if (e.type == GameEventType.AJCaught)
            BeginRescue(e.detail);
        else if (e.type == GameEventType.RunWon || e.type == GameEventType.RunFailed)
            EndWindowVisuals();
    }

    private void BeginRescue(string cause)
    {
        if (_aj == null || _claire == null || _flow == null || _flow.State != GameStartFlow.RunState.AJCaught)
            return;

        _active = true;
        _tiredRun = false;
        if (_ajAnimator != null)
        {
            _ajAnimator.speed = 1f;
            int reaction = cause == CauseExhausted ? TiredState : HitState;
            if (_ajAnimator.HasState(0, reaction)) _ajAnimator.CrossFadeInFixedTime(reaction, 0.1f);
            else _ajAnimator.speed = 0f; // the reaction clips are missing: hold the pose
        }

        // Snap AJ into the centre of his lane so the target is unambiguous.
        int ajLane = LaneMath.SharedNearestLane(_aj.transform.position.x);
        Vector3 ajPos = _aj.transform.position;
        ajPos.x = LaneMath.SharedLaneX(ajLane);

        // Guarantee a fair window.
        float minGap = Mathf.Max(reachDistance + 1f, _claire.forwardSpeed * minRescueWindowSeconds);
        float gap = ajPos.z - _claire.transform.position.z;
        if (gap < minGap)
            ajPos.z = _claire.transform.position.z + minGap;
        _aj.transform.position = ajPos;
        _windowTotalDistance = Mathf.Max(0.01f, ajPos.z - _claire.transform.position.z);

        ClearObstaclesInLane(ajLane, _claire.transform.position.z - 1f, ajPos.z + 3f);
        if (PoliceDirector.Instance != null)
            PoliceDirector.Instance.ClearLane(ajLane, _claire.transform.position.z - 1f, ajPos.z + 3f);

        if (_flow.hudController != null)
            _flow.hudController.ShowAlert(cause == CauseExhausted ? "AJ IS EXHAUSTED!" : cause == "police" ? "THE POLICE GRABBED AJ!" : "AJ IS HURT!");
        ProceduralSfx.Play(ProceduralSfx.Alarm, 0.7f);
        _banner.SetActive(true);
    }

    /// <summary>AJ visibly tires while police pressure is high, and recovers when it drops.</summary>
    private void UpdateTiredRun()
    {
        if (_aj == null || _ajAnimator == null || _flow == null || _aj.IsCaught || !_ajAnimator.HasState(0, TiredState)) return;
        if (_aj.AnimationBusy)
        {
            _tiredRun = false; // the jump, slide or skateboard has taken over the animator
            return;
        }
        var pressure = PolicePressure.Instance;
        bool want = _flow.State == GameStartFlow.RunState.Playing && pressure != null && pressure.IsHigh;
        if (want == _tiredRun) return;
        _tiredRun = want;
        if (want) _ajAnimator.CrossFadeInFixedTime(TiredState, 0.2f);
        else _aj.ResumeRunAnimation();
    }

    private void Update()
    {
        UpdateTiredRun();
        if (!_active || _flow == null)
            return;
        if (_flow.State == GameStartFlow.RunState.Paused)
            return;
        if (_flow.State != GameStartFlow.RunState.AJCaught)
        {
            EndWindowVisuals();
            return;
        }

        int ajLane = LaneMath.SharedNearestLane(_aj.transform.position.x);
        int claireLane = _claire.CurrentLane;
        float distance = _aj.transform.position.z - _claire.transform.position.z;
        float window01 = Mathf.Clamp01(distance / _windowTotalDistance);

        // Banner: direction + remaining window
        string dir = claireLane == ajLane ? "HOLD THIS LANE!" : claireLane < ajLane
            ? $"MOVE RIGHT  {new string('>', ajLane - claireLane)}"
            : $"{new string('<', claireLane - ajLane)}  MOVE LEFT";
        _bannerText.text = $"AJ IS CAUGHT!  GET IN HIS LANE\n<size=80%>{dir}</size>";
        _bannerText.color = claireLane == ajLane ? RuntimeUI.Accent : RuntimeUI.Gold;
        _bannerFill.fillAmount = window01;

        if (distance <= reachDistance)
        {
            bool sameLane = claireLane == ajLane &&
                            Mathf.Abs(_claire.transform.position.x - _aj.transform.position.x) <= LaneMath.SharedLaneDistance * 0.6f;
            if (sameLane) Succeed();
            else Fail();
        }
    }

    private void Succeed()
    {
        _active = false;
        _aj.ResumeRunAnimation();
        _aj.Release();
        _aj.BeginRecovery(_claire.transform, recoveryGap, postRescueInvulnerability);
        if (_flow.ExitAJCaught())
        {
            GameEvents.Raise(GameEventType.AJRescued);
            int coins = _flow.SpendCoins(RescueCoinCost);
            RescueSparkles.Create(_aj.transform, _claire.transform, postRescueInvulnerability);
            if (_flow.hudController != null) _flow.hudController.ShowAlert(coins > 0 ? $"AJ HEALED!  -{coins} COINS" : "AJ RESCUED!");
            ProceduralSfx.Play(ProceduralSfx.Rescue, 0.8f);
            if (GameplayHUDExtras.Instance != null) GameplayHUDExtras.Instance.Flash(new Color(0.2f, 1f, 0.5f, 0.25f));
        }
        _banner.SetActive(false);
    }

    private void Fail()
    {
        _active = false;
        _banner.SetActive(false);
        _flow.EndRun(RunEndReason.AJCaptured);
    }

    private void EndWindowVisuals()
    {
        _active = false;
        if (_banner != null) _banner.SetActive(false);
    }

    private static void ClearObstaclesInLane(int lane, float zMin, float zMax)
    {
        float laneX = LaneMath.SharedLaneX(lane);
        var obstacles = FindObjectsByType<LaneObstacle>(FindObjectsSortMode.None);
        for (int i = 0; i < obstacles.Length; i++)
        {
            var t = obstacles[i].transform;
            if (t.position.z < zMin || t.position.z > zMax) continue;
            if (Mathf.Abs(t.position.x - laneX) > LaneMath.SharedLaneDistance * 0.5f) continue;
            Destroy(obstacles[i].gameObject);
        }
    }

    private void BuildBanner()
    {
        Transform root = _flow != null && _flow.hudPanel != null ? _flow.hudPanel.transform : transform;
        var panel = RuntimeUI.CreateHudPanel(root, "RescueBanner (runtime)", new Color(1f, 0.72f, 0.72f));
        panel.raycastTarget = false;
        // On the right edge, under the loot counter: the radio box fills the top-left during a rescue.
        RuntimeUI.Place(panel.rectTransform, new Vector2(1f, 1f), new Vector2(780f, 160f), new Vector2(-16f, -205f));
        SidePopup.Attach(panel.rectTransform, fromRight: true);
        _bannerText = RuntimeUI.CreateText(panel.transform, "Text", "", 30f, TextAlignmentOptions.Center, RuntimeUI.Gold, FontStyles.Bold);
        RuntimeUI.Place(_bannerText.rectTransform, new Vector2(0.5f, 1f), new Vector2(730f, 104f), new Vector2(0f, -12f));
        _bannerText.fontSizeMax = _bannerText.fontSize;
        _bannerText.fontSizeMin = 10f;
        _bannerText.enableAutoSizing = true;
        _bannerFill = RuntimeUI.CreateBar(panel.transform, "Window", new Color(0f, 0f, 0f, 0.5f), RuntimeUI.Danger);
        RuntimeUI.Place((RectTransform)_bannerFill.transform.parent, new Vector2(0.5f, 0f), new Vector2(720f, 18f), new Vector2(0f, 8f));
        var theme = UITheme.Active;
        var red = theme != null ? theme.barRed.GetSprite() : null;
        if (red != null) { _bannerFill.sprite = red; _bannerFill.color = Color.white; }
        _banner = panel.gameObject;
        _banner.SetActive(false);
    }
}
