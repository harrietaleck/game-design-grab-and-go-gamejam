using UnityEngine;

/// <summary>
/// Simple lane-based "AI" that dodges LaneObstacle objects by switching to a safe lane.
/// </summary>
public class AIThiefLaneDodge : MonoBehaviour
{
    [Header("Lane Setup")]
    public int laneCount = 4;
    public float laneDistance = 2.5f;
    public float laneCenterX = 0f;

    [Header("Movement")]
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

    private int _currentLane = 1;
    private float _nextDecisionTime;
    private bool _caught;
    private float _emergencyLaneBoostUntil;

    private void Start()
    {
        laneCount = LaneMath.ClampLaneCount(laneCount);
        _currentLane = GetNearestLaneIndex(transform.position.x);
    }

    private void Update()
    {
        if (_caught)
            return;

        if (moveForward)
            transform.position += new Vector3(0f, 0f, forwardSpeed * Time.deltaTime);

        // Urgent: obstacle very close ahead in current lane — dodge immediately (no cooldown, no random skip).
        if (IsLaneBlocked(_currentLane, urgentLookAheadZ))
            TryPickSafeLane(urgent: true);

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
        // If we randomly decide to "hesitate", skip this turn sometimes.
        if (Random.value < dodgeRandomness)
            return;

        int bestLane = _currentLane;

        bool currentLaneBlocked = IsLaneBlocked(_currentLane, lookAheadZ);
        if (!currentLaneBlocked)
            return;

        // Try to find the closest safe lane.
        int[] laneOrder = GetLaneOrderByDistance(_currentLane);
        for (int i = 0; i < laneOrder.Length; i++)
        {
            int laneIndex = laneOrder[i];
            if (!IsLaneBlocked(laneIndex, lookAheadZ))
            {
                bestLane = laneIndex;
                break;
            }
        }

        _currentLane = bestLane;
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
            _emergencyLaneBoostUntil = Time.time + 0.35f;
        return moved;
    }

    private bool TryPickSafeLane(bool urgent)
    {
        float range = urgent ? urgentLookAheadZ : lookAheadZ;
        int[] laneOrder = GetLaneOrderByDistance(_currentLane);
        for (int i = 0; i < laneOrder.Length; i++)
        {
            int laneIndex = laneOrder[i];
            if (!IsLaneBlocked(laneIndex, range))
            {
                if (laneIndex != _currentLane)
                {
                    _currentLane = laneIndex;
                    _nextDecisionTime = Time.time + decisionCooldown;
                }
                return true;
            }
        }
        return false;
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

    public void OnHitObstacle()
    {
        _caught = true;
        // Optionally you could trigger animation/state here.
    }
}

