using UnityEngine;
using UnityEngine.Events;

public class LaneObstacle : MonoBehaviour
{
    [Header("Hit Behavior")]
    [Tooltip("If the handler has PlayerLaneMovement, disable it on hit.")]
    public bool disableHandlerMovementOnHit = false;

    [Tooltip("Destroy the obstacle when it is hit.")]
    public bool destroyOnHit = true;

    [Tooltip("If the AI thief bumps the obstacle, try an emergency lane change first. Only mark AI as caught if no safe lane exists.")]
    public bool allowAIEmergencyDodgeOnContact = true;

    [Tooltip("Health damage dealt when player hits this obstacle.")]
    public float healthDamage = 10f;

    [Tooltip("Fraction of Claire's loot value lost (and spilled from her bag) when she hits this obstacle.")]
    [Range(0f, 1f)] public float lootLossFraction = 0.5f;

    [Tooltip("Invoked when the obstacle is hit by the handler.")]
    public UnityEvent onHit;

    private void OnTriggerEnter(Collider other)
    {
        if (GameStartFlow.Instance != null && !GameStartFlow.IsGameplayActive)
            return;

        // Treat the handler as "the player" by checking for PlayerLaneMovement.
        // This avoids relying on tags that may not be set yet.
        var handlerMovement = other.GetComponentInParent<PlayerLaneMovement>();
        if (handlerMovement == null)
        {
            // Also support AI dodging runner collisions.
            var aiMovement = other.GetComponentInParent<AIThiefLaneDodge>();
            if (aiMovement == null)
                return;

            // Just rescued: AJ barges through instead of being caught again immediately.
            if (aiMovement.IsInvulnerable)
            {
                onHit?.Invoke();
                if (destroyOnHit)
                    Destroy(gameObject);
                return;
            }

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
            
            // Lose part of the loot first (so a fatal crash still costs loot), then apply health damage.
            // Both go through the run controller, the single source of truth. The obstacle is destroyed below,
            // so one obstacle can only do this once.
            var flow = GameStartFlow.Instance;
            int lost = flow != null ? flow.SpillLoot(lootLossFraction) : 0;
            if (flow != null)
                flow.ApplyClaireDamage(healthDamage);
            var hud = FindFirstObjectByType<HUDController>();
            if (hud != null)
            {
                if (flow == null) hud.AddHealth(-healthDamage);
                hud.ShowAlert(lost > 0 ? $"CRASH!  -{lost} LOOT" : "WATCH OUT!");
            }
        }

        onHit?.Invoke();

        if (destroyOnHit)
            Destroy(gameObject);
    }
}

