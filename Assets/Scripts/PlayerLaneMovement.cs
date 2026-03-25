using UnityEngine;

public class PlayerLaneMovement : MonoBehaviour
{
    [Header("Lane Settings")]
    public float laneDistance = 2.5f;   // Distance between lanes
    public float moveSpeed = 10f;       // Smooth movement speed

    [Header("Forward Movement")]
    [Tooltip("When enabled, the handler moves forward automatically along world +Z.")]
    public bool moveForward = true;

    [Tooltip("Forward speed in units/second (world +Z).")]
    public float forwardSpeed = 6f;

    private int currentLane = 1;        // 0 = left, 1 = middle, 2 = right
    private Vector3 targetPosition;

    void Start()
    {
        targetPosition = transform.position;
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
            if (currentLane < 2)
                currentLane++;
        }
    }

    void MovePlayer()
    {
        // Calculate target X position based on lane
        float targetX = (currentLane - 1) * laneDistance;

        targetPosition = new Vector3(targetX, transform.position.y, transform.position.z);

        // Smooth movement
        transform.position = Vector3.Lerp(transform.position, targetPosition, moveSpeed * Time.deltaTime);
    }
}