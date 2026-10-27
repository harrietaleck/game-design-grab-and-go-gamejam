using UnityEngine;
using UnityEngine.Events;

public class CollectiblePickup : MonoBehaviour
{
    [Min(1)]
    public int value = 1;

    public UnityEvent onCollected;

    public static int RuntimeCollectedValue { get; private set; }

    public static void ResetRuntimeCollectedValue()
    {
        RuntimeCollectedValue = 0;
    }

    /// <summary>Raised once per collected item (after scoring).</summary>
    public static event System.Action<CollectiblePickup> Collected;

    [Tooltip("Delay collection shortly after spawn so pickups don't vanish instantly on overlap.")]
    public float pickupDelaySeconds = 0.4f;
    [Tooltip("Enable fallback radius pickup (in addition to trigger collision).")]
    public bool useProximityPickup = false;
    [Tooltip("Fallback pickup radius around this collectible for PlayerLaneMovement.")]
    public float pickupRadius = 0.35f;
    [Tooltip("Optional one-shot SFX played when collected.")]
    public AudioClip pickupAudio;
    [Range(0f, 1f)] public float pickupAudioVolume = 1f;

    private bool _collected;

    // Cached player (was a FindObjectsByType call per item per frame).
    private static PlayerLaneMovement s_player;
    private float _canCollectAt;

    private void Awake()
    {
        _canCollectAt = Time.time + Mathf.Max(0f, pickupDelaySeconds);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_collected)
            return;

        if (Time.time < _canCollectAt)
            return;
        if (GameStartFlow.Instance != null && !GameStartFlow.CanCollectLoot)
            return;

        var player = other.GetComponentInParent<PlayerLaneMovement>();
        if (player == null)
            return;

        CollectNow("OnTriggerEnter");
    }

    private void Update()
    {
        if (_collected || Time.time < _canCollectAt || !useProximityPickup)
            return;
        if (GameStartFlow.Instance != null && !GameStartFlow.CanCollectLoot)
            return;

        if (s_player == null)
            s_player = FindFirstObjectByType<PlayerLaneMovement>();
        if (s_player == null)
            return;

        float sqrRadius = pickupRadius * pickupRadius;
        if ((s_player.transform.position - transform.position).sqrMagnitude <= sqrRadius)
            CollectNow("ProximityCheck");
    }

    private void CollectNow(string reason)
    {
        if (_collected)
            return;

        int awardValue = Mathf.Max(1, value);
        var flow = GameStartFlow.Instance;
        if (flow != null && !flow.RegisterLoot(awardValue))
            return; // run not active: leave the item where it is, score nothing

        _collected = true;
        RuntimeCollectedValue = flow != null ? flow.LootValue : RuntimeCollectedValue + awardValue;

        if (pickupAudio != null)
            AudioSource.PlayClipAtPoint(pickupAudio, transform.position, pickupAudioVolume);

        var hud = FindFirstObjectByType<HUDController>();
        if (hud != null)
            hud.ShowAlert(awardValue >= 5 ? $"+{awardValue} GOLD!" : $"+{awardValue}");

        LootPopup.Spawn(transform.position, awardValue);

        onCollected?.Invoke();
        Collected?.Invoke(this);
        Destroy(gameObject);
    }

    public static void EnsureSetup(GameObject go, int fallbackValue)
    {
        if (go == null)
            return;

        var pickup = go.GetComponent<CollectiblePickup>();
        if (pickup == null)
            pickup = go.AddComponent<CollectiblePickup>();

        if (pickup.value <= 0)
            pickup.value = Mathf.Max(1, fallbackValue);

        // CRITICAL: Disable ALL existing colliders (they might be huge or misconfigured)
        var existingColliders = go.GetComponentsInChildren<Collider>(includeInactive: false);
        foreach (var col in existingColliders)
            col.enabled = false;

        // Add a fresh, properly sized trigger collider on the root
        if (!go.TryGetComponent<BoxCollider>(out var boxCol))
            boxCol = go.AddComponent<BoxCollider>();
        
        boxCol.isTrigger = true;
        boxCol.center = Vector3.zero;
        boxCol.size = new Vector3(0.015f, 0.015f, 0.015f); // Very tight trigger - must actually touch to collect

        if (!go.TryGetComponent<Rigidbody>(out var rb))
            rb = go.AddComponent<Rigidbody>();

        rb.isKinematic = true;
        rb.useGravity = false;

        // Force a short grace window for newly spawned collectibles.
        pickup.pickupDelaySeconds = Mathf.Max(0f, pickup.pickupDelaySeconds);
    }
}
