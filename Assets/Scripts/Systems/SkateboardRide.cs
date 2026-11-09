using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Skateboard power-up. A board appears on the road from time to time; when Claire runs over it she and AJ both
/// ride for a few seconds as a speed boost, and the board takes the hit from one crash. Uses the board equipped in the shop.
/// </summary>
public class SkateboardRide : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        GameStartFlow.RegisterBootstrap(flow =>
        {
            if (flow.GetComponent<SkateboardRide>() == null)
                flow.gameObject.AddComponent<SkateboardRide>();
        });
    }

    public static SkateboardRide Instance { get; private set; }
    private static readonly int SkateState = Animator.StringToHash("Skate");
    private static readonly int RunState = Animator.StringToHash("Running");

    public float firstDelay = 14f;
    public Vector2 spawnInterval = new Vector2(20f, 32f);
    public float spawnAhead = 42f;
    public float rideSeconds = 9f;
    public float longRideSeconds = 14f;
    public float speedMultiplier = 1.7f;
    [Tooltip("Degrees the camera widens during the boost, for a sense of speed.")]
    public float boostFieldOfView = 10f;
    public float boardLength = 1.15f;
    public float rideHeight = 0.16f;
    [Tooltip("Turn of the board around Y, so its nose points along the road.")]
    public float boardYaw = 0f;

    public bool IsRiding { get; private set; }
    public float RideRemaining01 => IsRiding ? Mathf.Clamp01((_rideEndsAt - Time.time) / _rideLength) : 0f;

    private GameStartFlow _flow;
    private PlayerLaneMovement _claire;
    private AIThiefLaneDodge _aj;
    private GameObject _boardPrefab;
    private Transform _pickup;
    private GameObject _claireBoard, _ajBoard;
    private float _nextSpawnAt = float.MaxValue;
    private float _rideEndsAt, _rideLength = 1f;

    private GameObject _hud;
    private Image _hudFill;
    private Camera _camera;
    private float _baseFieldOfView;
    private Material _trailMaterial;

    private void Awake()
    {
        Instance = this;
        _flow = GetComponent<GameStartFlow>();
        BuildHud();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (_trailMaterial != null) Destroy(_trailMaterial);
    }
    private void OnEnable() => GameEvents.OnEvent += OnGameEvent;
    private void OnDisable() => GameEvents.OnEvent -= OnGameEvent;

    private void Start()
    {
        _claire = FindFirstObjectByType<PlayerLaneMovement>();
        _aj = FindFirstObjectByType<AIThiefLaneDodge>();
        var set = ShopSet.Active;
        if (set != null) _boardPrefab = set.EquippedBoardPrefab;
        _camera = Camera.main;
        if (_camera != null) _baseFieldOfView = _camera.fieldOfView;
        var fx = Resources.Load<Material>("FxParticle");
        if (fx != null)
        {
            _trailMaterial = new Material(fx);
            _trailMaterial.SetColor("_BaseColor", new Color(0.2f, 0.9f, 1f, 0.8f));
        }
        // Covers a run that started before this component's Start.
        if (_flow != null && _flow.State == GameStartFlow.RunState.Playing) _nextSpawnAt = Time.time + firstDelay;
    }

    private void OnGameEvent(GameEventData e)
    {
        if (e.type == GameEventType.RunStarted) _nextSpawnAt = Time.time + firstDelay;
        else if (e.type == GameEventType.AJCaught) EndRide();
    }

    private void Update()
    {
        if (_claire == null || _boardPrefab == null || !GameStartFlow.IsGameplayActive) return;

        if (_camera != null)
            _camera.fieldOfView = Mathf.MoveTowards(_camera.fieldOfView, _baseFieldOfView + (IsRiding ? boostFieldOfView : 0f), 30f * Time.deltaTime);

        if (IsRiding)
        {
            _hudFill.fillAmount = RideRemaining01;
            if (Time.time >= _rideEndsAt) EndRide();
        }

        if (_pickup != null) UpdatePickup();
        else if (!IsRiding && _flow.State == GameStartFlow.RunState.Playing && Time.time >= _nextSpawnAt) SpawnPickup();
    }

    private void UpdatePickup()
    {
        _pickup.Rotate(Vector3.up, 160f * Time.deltaTime, Space.World);
        Vector3 c = _claire.transform.position;
        Vector3 p = _pickup.position;
        if (Mathf.Abs(p.z - c.z) < 1.1f && Mathf.Abs(p.x - c.x) < 1.3f)
        {
            Destroy(_pickup.gameObject);
            _pickup = null;
            StartRide();
        }
        else if (p.z < c.z - 6f)
        {
            Destroy(_pickup.gameObject);
            _pickup = null;
            ScheduleNext();
        }
    }

    private void ScheduleNext() => _nextSpawnAt = Time.time + Random.Range(spawnInterval.x, spawnInterval.y);

    private void SpawnPickup()
    {
        float z = _claire.transform.position.z + spawnAhead;
        int lane = PickClearLane(z);
        if (lane < 0)
        {
            _nextSpawnAt = Time.time + 2f;
            return;
        }

        var go = CreateBoard();
        go.name = "Skateboard Pickup";
        go.transform.SetPositionAndRotation(new Vector3(LaneMath.SharedLaneX(lane), _claire.transform.position.y + 0.7f, z), Quaternion.Euler(0f, boardYaw, 0f));
        go.transform.localScale *= 1.25f;
        _pickup = go.transform;
    }

    public static int PickClearLane(float z)
    {
        var blocked = new bool[LaneMath.SharedLaneCount];
        foreach (var o in FindObjectsByType<LaneObstacle>(FindObjectsSortMode.None))
            if (Mathf.Abs(o.transform.position.z - z) < 7f)
                blocked[LaneMath.SharedNearestLane(o.transform.position.x)] = true;

        int start = Random.Range(0, blocked.Length);
        for (int i = 0; i < blocked.Length; i++)
        {
            int lane = (start + i) % blocked.Length;
            if (!blocked[lane]) return lane;
        }
        return -1;
    }

    private void StartRide()
    {
        IsRiding = true;
        _rideLength = ShopStore.Owns(ShopStore.LongRide) ? longRideSeconds : rideSeconds;
        _rideEndsAt = Time.time + _rideLength;

        _claireBoard = Mount(_claire.transform);
        _claire.RideHeight = rideHeight;
        _claire.SpeedMultiplier = speedMultiplier;
        if (_aj != null && !_aj.IsCaught)
        {
            _ajBoard = Mount(_aj.transform);
            _aj.RideHeight = rideHeight;
            _aj.SpeedMultiplier = speedMultiplier;
        }

        _hud.SetActive(true);
        _hudFill.fillAmount = 1f;
        ProceduralSfx.Play(ProceduralSfx.Rescue, 0.7f);
        if (_flow.hudController != null) _flow.hudController.ShowAlert("SPEED BOOST!");
    }

    private GameObject Mount(Transform rider)
    {
        var board = CreateBoard();
        board.name = "Skateboard";
        board.transform.SetParent(rider, false);
        board.transform.localPosition = new Vector3(0f, -rideHeight + 0.03f, 0f);
        board.transform.localRotation = Quaternion.Euler(0f, boardYaw, 0f);
        Play(rider, SkateState);
        if (_trailMaterial != null)
        {
            foreach (float side in new[] { -0.18f, 0.18f })
            {
                var streak = new GameObject("Speed Streak").AddComponent<TrailRenderer>();
                streak.transform.SetParent(board.transform, false);
                streak.transform.position = rider.position + new Vector3(side, 0.05f - rideHeight, 0f);
                streak.sharedMaterial = _trailMaterial;
                streak.time = 0.3f;
                streak.startWidth = 0.14f;
                streak.endWidth = 0f;
            }
        }
        return board;
    }

    private GameObject CreateBoard()
    {
        var go = Instantiate(_boardPrefab);
        foreach (var c in go.GetComponentsInChildren<Collider>(true)) Destroy(c);

        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            float largest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            if (largest > 0.0001f) go.transform.localScale *= boardLength / largest;
        }
        return go;
    }

    private static void Play(Transform rider, int state)
    {
        var animator = rider.GetComponentInChildren<Animator>();
        if (animator != null && animator.HasState(0, state)) animator.CrossFadeInFixedTime(state, 0.15f);
    }

    /// <summary>A crash while riding smashes the board instead of hurting Claire. Returns true if it did.</summary>
    public bool TryAbsorbCrash()
    {
        if (!IsRiding) return false;
        _flow.GrantHitProtection();
        var boardRenderer = _claireBoard != null ? _claireBoard.GetComponentInChildren<Renderer>() : null;
        if (boardRenderer != null) ImpactSparks.Burst(_claire.transform.position + Vector3.up * 0.4f, boardRenderer.sharedMaterial, 14);
        ProceduralSfx.Play(ProceduralSfx.BulletHit, 0.9f);
        if (_flow.hudController != null) _flow.hudController.ShowAlert("BOARD SMASHED!");
        EndRide();
        return true;
    }

    private void EndRide()
    {
        if (!IsRiding) return;
        IsRiding = false;
        if (_claireBoard != null) Destroy(_claireBoard);
        if (_ajBoard != null) Destroy(_ajBoard);
        _claire.RideHeight = 0f;
        _claire.SpeedMultiplier = 1f;
        Play(_claire.transform, RunState);
        if (_aj != null)
        {
            _aj.RideHeight = 0f;
            _aj.SpeedMultiplier = 1f;
            Play(_aj.transform, RunState);
        }
        _hud.SetActive(false);
        ScheduleNext();
    }

    private void BuildHud()
    {
        Transform root = _flow != null && _flow.hudPanel != null ? _flow.hudPanel.transform : transform;
        var panel = RuntimeUI.CreateHudPanel(root, "SkateTimer (runtime)");
        RuntimeUI.Place(panel.rectTransform, new Vector2(0f, 0.5f), new Vector2(460f, 100f), new Vector2(16f, -160f));
        SidePopup.Attach(panel.rectTransform, fromRight: false);
        var text = RuntimeUI.CreateText(panel.transform, "Text", "SPEED BOOST", 24f, TextAlignmentOptions.Center, RuntimeUI.Accent, FontStyles.Bold);
        RuntimeUI.Place(text.rectTransform, new Vector2(0.5f, 1f), new Vector2(420f, 40f), new Vector2(0f, -12f));
        _hudFill = RuntimeUI.CreateBar(panel.transform, "Bar", new Color(0f, 0f, 0f, 0.55f), RuntimeUI.Accent);
        RuntimeUI.Place((RectTransform)_hudFill.transform.parent, new Vector2(0.5f, 0f), new Vector2(400f, 30f), new Vector2(0f, 16f));
        _hud = panel.gameObject;
        _hud.SetActive(false);
    }
}
