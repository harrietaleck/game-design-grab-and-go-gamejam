using UnityEngine;

public class PlayerLaneMovement : MonoBehaviour
{
    [Header("Lane Settings")]
    [Tooltip("Use the project-wide lane settings in LaneMath (4 lanes, 3.0 apart, centre X 0). Turn off only to experiment with per-object values.")]
    public bool useSharedLaneSettings = true;

    [Tooltip("Number of playable lanes.")]
    public int laneCount = 4;

    [Tooltip("Distance between lane centers.")]
    public float laneDistance = LaneMath.SharedLaneDistance;

    [Tooltip("World X center of the road lanes.")]
    public float laneCenterX = 0f;

    [Tooltip("Lane index to start in. Leave negative to use nearest lane to current X.")]
    public int startLane = -1;

    [Tooltip("Lateral smoothing speed.")]
    public float moveSpeed = 10f;

    [Header("Forward Movement")]
    [Tooltip("Make this character's Rigidbody kinematic at runtime (it is moved by script, not physics).")]
    public bool makeRigidbodyKinematic = true;

    [Tooltip("When enabled, the handler moves forward automatically along world +Z.")]
    public bool moveForward = true;

    [Tooltip("Forward speed in units/second (world +Z).")]
    public float forwardSpeed = 6f;

    [Header("Jump (W / Up) and Dodge (S / Down)")]
    public float jumpHeight = 1.25f;
    public float doubleJumpHeight = 1.5f;
    public float jumpGravity = 26f;
    [Tooltip("Seconds the dodge slide lasts. Bullets and grabs miss during it.")]
    public float dodgeSeconds = 0.5f;
    public float dodgeCooldown = 1.1f;
    public float dodgeLeanAngle = -62f;

    /// <summary>Multiplied onto forward speed (the skateboard sets it).</summary>
    public float SpeedMultiplier { get; set; } = 1f;
    /// <summary>Temporary slowdown from hazards such as tear gas; separate from the skateboard's multiplier.</summary>
    public float HazardSpeedMultiplier { get; set; } = 1f;
    /// <summary>Extra height while standing on a skateboard.</summary>
    public float RideHeight { get; set; }

    public float HeightAboveGround => _jumpHeightNow;
    public bool IsDodging => Time.time < _dodgeUntil;
    /// <summary>True when bullets and grabs miss: mid-jump or mid-slide.</summary>
    public bool IsEvading => IsDodging || _jumpHeightNow > 0.35f
                             || (GameStartFlow.Instance != null && GameStartFlow.Instance.IsClaireInvulnerable);

    private bool _blinkedOff;

    /// <summary>Claire flickers while protected after a hit.</summary>
    private void UpdateHitBlink()
    {
        var flow = GameStartFlow.Instance;
        bool off = flow != null && flow.IsClaireInvulnerable && Mathf.Repeat(Time.time * 11f, 1f) < 0.5f;
        if (off == _blinkedOff) return;
        _blinkedOff = off;
        foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = !off;
    }

    private float _groundY;
    private float _jumpHeightNow;
    private float _jumpVelocity;
    private int _jumpsUsed;
    private float _dodgeUntil = float.MinValue;
    private float _dodgeReadyAt;
    private float _lean;
    private Animator _animator;
    private bool _hasActionClips;
    private bool _actionPlaying;
    private bool _dodgeActive;

    private static readonly int JumpState = Animator.StringToHash("Jump");
    private static readonly int DoubleJumpState = Animator.StringToHash("DoubleJump");
    private static readonly int SlideState = Animator.StringToHash("Slide");
    private static readonly int DuckState = Animator.StringToHash("Duck");
    private static readonly int RunState = Animator.StringToHash("Running");
    private static readonly int SkateState = Animator.StringToHash("Skate");

    private int currentLane;

    /// <summary>The lane Claire is in or moving to.</summary>
    public int CurrentLane => currentLane;
    private Vector3 targetPosition;

    void Awake()
    {
        // Characters are moved by script, so a dynamic (gravity) Rigidbody fights the transform and can shove
        // Claire and AJ apart. Kinematic keeps trigger events with obstacles and loot working.
        if (makeRigidbodyKinematic && TryGetComponent<Rigidbody>(out var body))
        {
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.None;
        }
        if (useSharedLaneSettings)
        {
            laneCount = LaneMath.SharedLaneCount;
            laneDistance = LaneMath.SharedLaneDistance;
            laneCenterX = LaneMath.SharedLaneCenterX;
        }
        if (ShopStore.Owns(ShopStore.Sprint)) forwardSpeed *= ShopStore.SprintMultiplier;
        _animator = GetComponentInChildren<Animator>();
        _hasActionClips = _animator != null && _animator.HasState(0, SlideState);
    }

    private float AirTime(float height) => 2f * Mathf.Sqrt(2f * height / jumpGravity);

    /// <summary>Plays a one-shot move, timed to last 'seconds'. Riding keeps the skate pose for jumps.</summary>
    private void PlayAction(int state, string clipName, float seconds)
    {
        if (!_hasActionClips) return;
        float length = seconds;
        foreach (var clip in _animator.runtimeAnimatorController.animationClips)
            if (clip.name == clipName) length = clip.length;
        _animator.CrossFadeInFixedTime(state, 0.06f);
        _animator.speed = length / Mathf.Max(0.05f, seconds);
        _actionPlaying = true;
    }

    private void EndAction()
    {
        if (!_actionPlaying) return;
        _actionPlaying = false;
        _animator.speed = 1f;
        _animator.CrossFadeInFixedTime(RideHeight > 0f ? SkateState : RunState, 0.12f);
    }

    void Start()
    {
        int safeLaneCount = LaneMath.ClampLaneCount(laneCount);
        currentLane = startLane >= 0
            ? Mathf.Clamp(startLane, 0, safeLaneCount - 1)
            : LaneMath.GetNearestLaneIndex(transform.position.x, safeLaneCount, laneDistance, laneCenterX);

        targetPosition = transform.position;
        _groundY = transform.position.y;
        float startX = LaneMath.GetLaneX(currentLane, safeLaneCount, laneDistance, laneCenterX);
        transform.position = new Vector3(startX, transform.position.y, transform.position.z);
    }

    void Update()
    {
        UpdateHitBlink();
        // Only advance during an active run (Playing or AJ rescue window).
        if (GameStartFlow.Instance != null && !GameStartFlow.IsGameplayActive)
            return;

        HandleInput();
        
        // Auto-move forward so the runner/prototype advances without player input.
        if (moveForward)
        {
            transform.position += new Vector3(0f, 0f, forwardSpeed * SpeedMultiplier * HazardSpeedMultiplier * Time.deltaTime);
        }

        MovePlayer();
        UpdateJumpAndDodge();
    }

    private void TryJump()
    {
        if (IsDodging) return;
        if (_jumpsUsed == 0)
        {
            _jumpsUsed = 1;
            _jumpVelocity = Mathf.Sqrt(2f * jumpGravity * jumpHeight);
            ProceduralSfx.Play(ProceduralSfx.Jump, 0.5f);
            if (RideHeight <= 0f) PlayAction(JumpState, "Player Jump", AirTime(jumpHeight));
        }
        else if (_jumpsUsed == 1 && ShopStore.Owns(ShopStore.DoubleJump))
        {
            _jumpsUsed = 2;
            _jumpVelocity = Mathf.Sqrt(2f * jumpGravity * doubleJumpHeight);
            ProceduralSfx.Play(ProceduralSfx.Jump, 0.6f);
            if (RideHeight <= 0f) PlayAction(DoubleJumpState, "Player Double Jump", AirTime(doubleJumpHeight));
        }
    }

    private void TryDodge()
    {
        if (_jumpsUsed > 0 || Time.time < _dodgeReadyAt) return;
        _dodgeUntil = Time.time + dodgeSeconds;
        _dodgeReadyAt = Time.time + dodgeCooldown;
        _dodgeActive = true;
        ProceduralSfx.Play(ProceduralSfx.Dodge, 0.6f);
        if (RideHeight > 0f) PlayAction(DuckState, "Player Duck", dodgeSeconds);
        else PlayAction(SlideState, "Player Slide", dodgeSeconds);
    }

    private void UpdateJumpAndDodge()
    {
        if (_jumpsUsed > 0)
        {
            _jumpVelocity -= jumpGravity * Time.deltaTime;
            _jumpHeightNow += _jumpVelocity * Time.deltaTime;
            if (_jumpHeightNow <= 0f)
            {
                _jumpHeightNow = 0f;
                _jumpVelocity = 0f;
                _jumpsUsed = 0;
                EndAction();
            }
        }

        if (_dodgeActive && !IsDodging)
        {
            _dodgeActive = false;
            EndAction();
        }

        Vector3 p = transform.position;
        p.y = _groundY + _jumpHeightNow + RideHeight;
        transform.position = p;

        // If the animation clips are ever missing, the moves still read: slower legs in the air, a lean back to slide.
        if (_animator != null && !_hasActionClips) _animator.speed = _jumpsUsed > 0 ? 0.35f : 1f;
        _lean = Mathf.MoveTowards(_lean, IsDodging && !_hasActionClips ? dodgeLeanAngle : 0f, 420f * Time.deltaTime);
        transform.rotation = Quaternion.Euler(_lean, 0f, 0f);
    }

    void HandleInput()
    {
        if (GameInput.Jump) TryJump();
        if (GameInput.Dodge) TryDodge();

        // Move Left
        if (GameInput.MoveLeft)
        {
            if (currentLane > 0)
                currentLane--;
        }

        // Move Right
        if (GameInput.MoveRight)
        {
            if (currentLane < LaneMath.ClampLaneCount(laneCount) - 1)
                currentLane++;
        }
    }

    void MovePlayer()
    {
        int safeLaneCount = LaneMath.ClampLaneCount(laneCount);
        currentLane = Mathf.Clamp(currentLane, 0, safeLaneCount - 1);
        float targetX = LaneMath.GetLaneX(currentLane, safeLaneCount, laneDistance, laneCenterX);

        targetPosition = new Vector3(targetX, transform.position.y, transform.position.z);

        // Smooth movement
        transform.position = Vector3.Lerp(transform.position, targetPosition, moveSpeed * Time.deltaTime);
    }
}