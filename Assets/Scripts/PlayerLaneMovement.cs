using UnityEngine;

public class PlayerLaneMovement : MonoBehaviour
{
    [Header("Lane Settings")]
    [Tooltip("Number of playable lanes.")]
    public int laneCount = 4;

    [Tooltip("Distance between lane centers.")]
    public float laneDistance = 2.5f;

    [Tooltip("World X center of the road lanes.")]
    public float laneCenterX = 0f;

    [Tooltip("Lane index to start in. Leave negative to use nearest lane to current X.")]
    public int startLane = -1;

    [Tooltip("Lateral smoothing speed.")]
    public float moveSpeed = 10f;

    [Header("Forward Movement")]
    [Tooltip("When enabled, the handler moves forward automatically along world +Z.")]
    public bool moveForward = true;

    [Tooltip("Forward speed in units/second (world +Z).")]
    public float forwardSpeed = 6f;

    private int currentLane;
    private Vector3 targetPosition;

    void Start()
    {
        int safeLaneCount = LaneMath.ClampLaneCount(laneCount);
        currentLane = startLane >= 0
            ? Mathf.Clamp(startLane, 0, safeLaneCount - 1)
            : LaneMath.GetNearestLaneIndex(transform.position.x, safeLaneCount, laneDistance, laneCenterX);

        targetPosition = transform.position;
        float startX = LaneMath.GetLaneX(currentLane, safeLaneCount, laneDistance, laneCenterX);
        transform.position = new Vector3(startX, transform.position.y, transform.position.z);
    }

    void Update()
    {
        HandleInput();
        
        // Auto-move forward so the runner/prototype advances without player input.
        if (moveForward)
        {
            transform.position += new Vector3(0f, 0f, forwardSpeed * Time.deltaTime);
        }

        MovePlayer();
    }

    void HandleInput()
    {
        // Move Left
        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
        {
            if (currentLane > 0)
                currentLane--;
        }

        // Move Right
        if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
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