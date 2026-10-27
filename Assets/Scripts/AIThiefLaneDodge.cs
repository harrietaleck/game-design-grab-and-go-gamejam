using UnityEngine;

/// <summary>
/// Simple lane-based "AI" that dodges LaneObstacle objects by switching to a safe lane.
/// </summary>
public class AIThiefLaneDodge : MonoBehaviour
{
    [Header("Lane Setup")]
    [Tooltip("Use the project-wide lane settings in LaneMath (4 lanes, 3.0 apart, centre X 0). Turn off only to experiment with per-object values.")]
    public bool useSharedLaneSettings = true;

    public int laneCount = 4;
    public float laneDistance = LaneMath.SharedLaneDistance;
    public float laneCenterX = 0f;

    [Header("Movement")]
    [Tooltip("Make this character's Rigidbody kinematic at runtime (it is moved by script, not physics).")]
    public bool makeRigidbodyKinematic = true;

    [Tooltip("Lateral smoothing speed.")]
    public float laneMoveSpeed = 10f;

    [Tooltip("Move forward automatically along +Z.")]
    public bool moveForward = true;
    public float forwardSpeed = 6f;

    [Header("Dodge Decision")]
    [Tooltip("How far ahead (Z) the AI checks for obstacles (planning / early dodge).")]
    public float lookAheadZ = 28f;

    [Tooltip("Shorter range for urgent reactions. If the current lane is blocked in this zone, the AI dodges immediately (ignores cooldown + randomness).")]
    public float urgentLookAheadZ = 10f;

    [Tooltip("How wide (X) the AI checks per lane.")]
    public float laneCheckRadiusX = 0.9f;

    [Tooltip("How often the AI is allowed to change lanes (normal planning only; urgent zone bypasses this).")]
    public float decisionCooldown = 0.35f;

    [Tooltip("Small randomness makes dodging feel less perfect (normal planning only; urgent zone ignores this).")]
    [Range(0f, 1f)]
    public float dodgeRandomness = 0.15f;

    [Tooltip("Extra lateral lerp speed when dodging from urgent zone or after a bump.")]
    public float emergencyLaneMoveMultiplier = 2.5f;

    [Header("Smarter Dodging (Task 2.3)")]
    [Tooltip("After a planned (non-urgent) lane change, stay at least this long before planning another. Stops lane flip-flopping.")]
    public float minLaneHoldSeconds = 0.6f;
    [Tooltip("When crossing several lanes, the lanes in between must be clear for this distance ahead.")]
    public float crossingCheckZ = 6f;
    [Tooltip("Minimum seconds between AJ near-miss events (for pressure/dialogue).")]
    public float nearMissEventCooldown = 2.5f;
    public bool debugDecisions = false;

    [Header("Recovery After Rescue")]
    [Tooltip("Forward speed multiplier while AJ regains his lead after being rescued.")]
    public float recoverySpeedMultiplier = 1.6f;

    private int _currentLane = 1;
    private float _nextDecisionTime;
    private bool _caught;

    public bool IsCaught => _caught;

    /// <summary>Raised once each time AJ becomes caught. The string is the cause ("obstacle", "police").</summary>
    public event System.Action<AIThiefLaneDodge, string> Caught;
    /// <summary>Raised when AJ is released (rescued).</summary>
    public event System.Action<AIThiefLaneDodge> Released;

    public int CurrentLane => _currentLane;

    private float _emergencyLaneBoostUntil;
    private float _laneHoldUntil;
    private float _nextNearMissEventAt;
    private float _invulnerableUntil;
    private Transform _recoveryTarget;
    private float _recoveryGap;

    /// <summary>True briefly after a rescue: obstacles AJ touches are cleared instead of catching him.</summary>
    public bool IsInvulnerable => Time.time < _invulnerableUntil;
    public bool IsRecovering => _recoveryTarget != null;

    private void Awake()
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
    }

    private void Start()
    {
        laneCount = LaneMath.ClampLaneCount(laneCount);
        _currentLane = GetNearestLaneIndex(transform.position.x);
    }

    private void Update()
    {
        if (_caught)
            return;
        if (GameStartFlow.Instance != null && !GameStartFlow.IsGameplayActive)
            return;

        if (moveForward)
            transform.position += new Vector3(0f, 0f, CurrentForwardSpeed() * Time.deltaTime);

        // Urgent: obstacle very close ahead in current lane - dodge immediately (no cooldown, no random skip).
        if (IsLaneBlocked(_currentLane, urgentLookAheadZ))
        {
            if (TryPickSafeLane(urgent: true))
                RaiseNearMiss("urgent dodge");
        }

        // Normal planning: decide occasionally to avoid lane jitter.
        if (Time.time >= _nextDecisionTime)
        {
            _nextDecisionTime = Time.time + Mathf.Max(0.01f, decisionCooldown);
            DecideAndSetLane();
        }

        MoveToLaneX();
    }

    private void DecideAndSetLane()
    {
        if (Time.time < _laneHoldUntil)
            return;

        // If we randomly decide to "hesitate", skip this turn sometimes.
        if (Random.value < dodgeRandomness)
            return;

        if (!IsLaneBlocked(_currentLane, lookAheadZ))
            return;

        int best = PickBestLane(lookAheadZ);
        if (best >= 0 && best != _currentLane)
        {
            if (debugDecisions) Debug.Log($"[AJ] Planned dodge {_currentLane} -> {best}");
            _currentLane = best;
            _laneHoldUntil = Time.time + minLaneHoldSeconds;
        }
    }

    /// <summary>
    /// Nearest clear lane that can also be reached safely (lanes crossed on the way are clear).
    /// Ties between equally near lanes go to the one with more open road ahead. Returns -1 if none.
    /// </summary>
    private int PickBestLane(float range)
    {
        int[] order = GetLaneOrderByDistance(_currentLane);
        int best = -1;
        int bestDistance = int.MaxValue;
        float bestClearance = -1f;
        for (int i = 0; i < order.Length; i++)
        {
            int lane = order[i];
            int distance = Mathf.Abs(lane - _currentLane);
            if (best >= 0 && distance > bestDistance)
                break; // order is sorted by distance; nothing nearer remains
            if (IsLaneBlocked(lane, range) || !IsCrossingClear(_currentLane, lane))
                continue;
            float clearance = ClearanceAhead(lane, lookAheadZ * 1.5f);
            if (best < 0 || clearance > bestClearance)
            {
                best = lane;
                bestDistance = distance;
                bestClearance = clearance;
            }
        }
        return best;
    }

    private bool IsCrossingClear(int fromLane, int toLane)
    {
        int step = toLane > fromLane ? 1 : -1;
        for (int lane = fromLane + step; lane != toLane; lane += step)
        {
            if (IsLaneBlocked(lane, crossingCheckZ))
                return false;
        }
        return true;
    }

    /// <summary>Distance to the nearest obstacle ahead in a lane (or maxRange if none).</summary>
    private float ClearanceAhead(int laneIndex, float maxRange)
    {
        float x = GetLaneX(laneIndex);
        var center = new Vector3(x, transform.position.y, transform.position.z + maxRange * 0.5f);
        var hits = Physics.OverlapBox(center, new Vector3(laneCheckRadiusX, 1.5f, maxRange * 0.5f), Quaternion.identity, ~0, QueryTriggerInteraction.Collide);
        float nearest = maxRange;
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i] == null || hits[i].GetComponentInParent<LaneObstacle>() == null) continue;
            float dz = hits[i].bounds.min.z - transform.position.z;
            if (dz >= 0f && dz < nearest) nearest = dz;
        }
        return nearest;
    }

    private void RaiseNearMiss(string detail)
    {
        if (Time.time < _nextNearMissEventAt) return;
        _nextNearMissEventAt = Time.time + nearMissEventCooldown;
        GameEvents.Raise(GameEventType.AJNearMiss, 0, detail);
    }

    private float CurrentForwardSpeed()
    {
        if (_recoveryTarget == null)
            return forwardSpeed;
        float gap = transform.position.z - _recoveryTarget.position.z;
        if (gap >= _recoveryGap)
        {
            _recoveryTarget = null; // lead restored
            return forwardSpeed;
        }
        return forwardSpeed * Mathf.Max(1f, recoverySpeedMultiplier);
    }

    /// <summary>After a rescue: run faster until AJ is 'gap' units ahead of 'claire', and ignore obstacles for a moment.</summary>
    public void BeginRecovery(Transform claire, float gap, float invulnerableSeconds)
    {
        _recoveryTarget = claire;
        _recoveryGap = Mathf.Max(0f, gap);
        _invulnerableUntil = Time.time + Mathf.Max(0f, invulnerableSeconds);
    }

    /// <summary>
    /// Called when the AI touches an obstacle trigger. Tries to jump to a safe lane; returns true if one was found.
    /// </summary>
    public bool TryEmergencyDodgeFromContact()
    {
        if (_caught)
            return false;

        bool moved = TryPickSafeLane(urgent: true);
        if (moved)
        {
            _emergencyLaneBoostUntil = Time.time + 0.35f;
            RaiseNearMiss("emergency dodge");
        }
        return moved;
    }

    private bool TryPickSafeLane(bool urgent)
    {
        float range = urgent ? urgentLookAheadZ : lookAheadZ;
        if (!IsLaneBlocked(_currentLane, range))
            return true;

        int best = PickBestLane(range);
        if (best < 0)
            return false;
        if (best != _currentLane)
        {
            if (debugDecisions) Debug.Log($"[AJ] {(urgent ? "Urgent" : "Planned")} dodge {_currentLane} -> {best}");
            _currentLane = best;
            _nextDecisionTime = Time.time + decisionCooldown;
            _laneHoldUntil = Time.time + minLaneHoldSeconds;
        }
        return true;
    }

    private void MoveToLaneX()
    {
        float targetX = GetLaneX(_currentLane);
        var targetPosition = new Vector3(targetX, transform.position.y, transform.position.z);
        float speed = laneMoveSpeed;
        if (Time.time < _emergencyLaneBoostUntil)
            speed *= emergencyLaneMoveMultiplier;
        transform.position = Vector3.Lerp(transform.position, targetPosition, speed * Time.deltaTime);
    }

    private bool IsLaneBlocked(int laneIndex, float forwardRangeZ)
    {
        if (forwardRangeZ <= 0f)
            return false;

        float x = GetLaneX(laneIndex);
        float zCenter = transform.position.z + forwardRangeZ * 0.5f;

        // Box covers the AI's upcoming path in that lane.
        var halfExtents = new Vector3(laneCheckRadiusX, 1.5f, forwardRangeZ * 0.5f);
        var center = new Vector3(x, transform.position.y, zCenter);

        var hits = Physics.OverlapBox(
            center,
            halfExtents,
            Quaternion.identity,
            ~0,
            QueryTriggerInteraction.Collide);
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i] == null)
                continue;

            // Collider might be on a child; search parents so AI can still "see" the LaneObstacle.
            if (hits[i].GetComponentInParent<LaneObstacle>() != null)
                return true;
        }

        return false;
    }

    private int[] GetLaneOrderByDistance(int fromLane)
    {
        // For 3 lanes, this gives an intuitive priority:
        // current lane, then the nearest side lane, then the other side lane.
        laneCount = LaneMath.ClampLaneCount(laneCount);
        int[] order = new int[laneCount];
        for (int i = 0; i < laneCount; i++)
            order[i] = i;

        // Simple insertion sort by absolute lane index distance.
        for (int i = 1; i < order.Length; i++)
        {
            int key = order[i];
            int j = i - 1;
            while (j >= 0 && Mathf.Abs(order[j] - fromLane) > Mathf.Abs(key - fromLane))
            {
                order[j + 1] = order[j];
                j--;
            }
            order[j + 1] = key;
        }

        return order;
    }

    private float GetLaneX(int laneIndex)
    {
        return LaneMath.GetLaneX(laneIndex, laneCount, laneDistance, laneCenterX);
    }

    private int GetNearestLaneIndex(float x)
    {
        return LaneMath.GetNearestLaneIndex(x, laneCount, laneDistance, laneCenterX);
    }

    /// <summary>Kept for compatibility (called by LaneObstacle when no escape lane exists).</summary>
    public void OnHitObstacle()
    {
        SetCaught("obstacle");
    }

    /// <summary>
    /// Stops AJ's movement (and, via ThiefCollectibleDropper, his loot drops) and informs the run controller once.
    /// </summary>
    public void SetCaught(string cause)
    {
        if (_caught)
            return;
        _caught = true;
        _recoveryTarget = null;
        Caught?.Invoke(this, cause);

        var flow = GameStartFlow.Instance;
        if (flow != null && flow.EnterAJCaught())
            GameEvents.Raise(GameEventType.AJCaught, 0, cause);
    }

    /// <summary>Frees AJ (used by the rescue system). Snaps him back to the nearest lane.</summary>
    public void Release()
    {
        if (!_caught)
            return;
        _caught = false;
        _currentLane = GetNearestLaneIndex(transform.position.x);
        _nextDecisionTime = Time.time + decisionCooldown;
        Released?.Invoke(this);
    }
}
