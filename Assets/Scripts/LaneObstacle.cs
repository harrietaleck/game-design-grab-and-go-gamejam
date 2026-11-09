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

    [Tooltip("Spike strips and barrier arms: AJ's AI does not steer around these, he jumps or slides instead.")]
    public bool passableByRunners = false;
    [Tooltip("Claire clears this obstacle by sliding.")]
    public bool clearedByDodge = false;
    [Tooltip("Roadblocks: reaching this ends the run. Hit protection and the skateboard do not help.")]
    public bool catchesClaire = false;

    [Tooltip("If above 0, Claire clears this obstacle when at least this high. 0 = work it out from the object's name.")]
    public float clearHeight = 0f;

    /// <summary>Bins can be jumped with one jump; cars need Double Jump Shoes; vans and roadblocks cannot be jumped.</summary>
    private float ClearHeight
    {
        get
        {
            if (clearHeight > 0f) return clearHeight;
            if (name.Contains("Trash")) return 0.5f;
            return name.Contains("Van") && !JumpBoost.Active ? 99f : 1.6f;
        }
    }

    private const float BoostClearHeight = 0.25f;

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
            if (aiMovement == null || passableByRunners)
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
            // A boosted jump only has to have left the ground: it is about to be high enough to clear traffic.
            bool boostClears = JumpBoost.Active && clearHeight <= 0f && handlerMovement.HeightAboveGround >= BoostClearHeight;
            if (boostClears || handlerMovement.HeightAboveGround >= ClearHeight || (clearedByDodge && handlerMovement.IsDodging))
            {
                if (GameStartFlow.Instance != null) GameStartFlow.Instance.NearMissCount++;
                return; // jumped or slid it
            }

            if (catchesClaire && GameStartFlow.Instance != null)
            {
                ProceduralSfx.Play(ProceduralSfx.Alarm, 0.7f);
                GameStartFlow.Instance.EndRun(RunEndReason.CaughtAtRoadblock);
                return;
            }

            if (GameStartFlow.Instance != null && GameStartFlow.Instance.IsClaireInvulnerable)
                return; // still protected from the last hit

            if (SkateboardRide.Instance != null && SkateboardRide.Instance.TryAbsorbCrash())
            {
                onHit?.Invoke();
                if (destroyOnHit)
                    Destroy(gameObject);
                return;
            }

            if (disableHandlerMovementOnHit)
                handlerMovement.enabled = false;

            // Lose part of the loot first (so a fatal crash still costs loot), then apply health damage.
            // Both go through the run controller, the single source of truth. The obstacle is destroyed below,
            // so one obstacle can only do this once.
            var flow = GameStartFlow.Instance;
            int lost = flow != null ? flow.SpillLoot(ShopStore.Owns(ShopStore.BigBag) ? 0.15f : 0.25f) : 0;
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

