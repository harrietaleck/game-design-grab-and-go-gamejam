using UnityEngine;
using UnityEngine.Events;

public class LaneObstacle : MonoBehaviour
{
    [Header("Hit Behavior")]
    [Tooltip("If the handler has PlayerLaneMovement, disable it on hit.")]
    public bool disableHandlerMovementOnHit = true;

    [Tooltip("Destroy the obstacle when it is hit.")]
    public bool destroyOnHit = true;

    [Tooltip("If the AI thief bumps the obstacle, try an emergency lane change first. Only mark AI as caught if no safe lane exists.")]
    public bool allowAIEmergencyDodgeOnContact = true;

    [Tooltip("Invoked when the obstacle is hit by the handler.")]
    public UnityEvent onHit;

    private void OnTriggerEnter(Collider other)
    {
        // Treat the handler as "the player" by checking for PlayerLaneMovement.
        // This avoids relying on tags that may not be set yet.
        var handlerMovement = other.GetComponentInParent<PlayerLaneMovement>();
        if (handlerMovement == null)
        {
            // Also support AI dodging runner collisions.
            var aiMovement = other.GetComponentInParent<AIThiefLaneDodge>();
            if (aiMovement == null)
                return;

            if (allowAIEmergencyDodgeOnContact && aiMovement.TryEmergencyDodgeFromContact())
            {
                // AI found a safe lane — still clear the obstacle if you want one-touch feedback.
                onHit?.Invoke();
                if (destroyOnHit)
                    Destroy(gameObject);
                return;
            }

            aiMovement.OnHitObstacle();
        }
        else
        {
            if (disableHandlerMovementOnHit)
                handlerMovement.enabled = false;
        }

        onHit?.Invoke();

        if (destroyOnHit)
            Destroy(gameObject);
    }
}

