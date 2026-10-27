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
    public float minRescueWindowSeconds = 1.8f;
    [Tooltip("Claire 'reaches' AJ when she is this close behind him (Z).")]
    public float reachDistance = 0.6f;
    [Tooltip("AJ's lead over Claire to restore after a rescue.")]
    public float recoveryGap = 10f;
    [Tooltip("Seconds AJ ignores obstacles right after a rescue.")]
    public float postRescueInvulnerability = 2.5f;

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
        if (_ajAnimator != null) _ajAnimator.speed = 0f;

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

        if (_flow.hudController != null)
            _flow.hudController.ShowAlert(cause == "police" ? "THE POLICE GRABBED AJ!" : "AJ IS CAUGHT!");
        ProceduralSfx.Play(ProceduralSfx.Alarm, 0.7f);
        _banner.SetActive(true);
    }

    private void Update()
    {
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
            ? $"MOVE RIGHT  {new string('▶', ajLane - claireLane)}"
            : $"{new string('◀', claireLane - ajLane)}  MOVE LEFT";
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
        if (_ajAnimator != null) _ajAnimator.speed = 1f;
        _aj.Release();
        _aj.BeginRecovery(_claire.transform, recoveryGap, postRescueInvulnerability);
        if (_flow.ExitAJCaught())
        {
            GameEvents.Raise(GameEventType.AJRescued);
            if (_flow.hudController != null) _flow.hudController.ShowAlert("AJ RESCUED!");
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
        var panel = RuntimeUI.CreatePanel(root, "RescueBanner (runtime)", new Color(0.35f, 0f, 0f, 0.75f));
        panel.raycastTarget = false;
        RuntimeUI.Place(panel.rectTransform, new Vector2(0.5f, 1f), new Vector2(760f, 130f), new Vector2(0f, -190f));
        _bannerText = RuntimeUI.CreateText(panel.transform, "Text", "", 40f, TextAlignmentOptions.Center, RuntimeUI.Gold, FontStyles.Bold);
        RuntimeUI.Place(_bannerText.rectTransform, new Vector2(0.5f, 1f), new Vector2(740f, 100f), new Vector2(0f, -4f));
        _bannerFill = RuntimeUI.CreateBar(panel.transform, "Window", new Color(0f, 0f, 0f, 0.5f), RuntimeUI.Danger);
        RuntimeUI.Place((RectTransform)_bannerFill.transform.parent, new Vector2(0.5f, 0f), new Vector2(720f, 18f), new Vector2(0f, 8f));
        _banner = panel.gameObject;
        _banner.SetActive(false);
    }
}
