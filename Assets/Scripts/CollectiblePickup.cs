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
    private bool _rewardApplied;
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

        var player = other.GetComponentInParent<PlayerLaneMovement>();
        if (player == null)
            return;

        CollectNow("OnTriggerEnter");
    }

    private void Update()
    {
        if (_collected || Time.time < _canCollectAt || !useProximityPickup)
            return;

        float sqrRadius = pickupRadius * pickupRadius;
        var players = FindObjectsByType<PlayerLaneMovement>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] == null)
                continue;

            float sqrDist = (players[i].transform.position - transform.position).sqrMagnitude;
            if (sqrDist <= sqrRadius)
            {
                CollectNow("ProximityCheck");
                return;
            }
        }
    }

    private void CollectNow(string reason)
    {
        if (_collected)
            return;

        _collected = true;
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        // Only reward on intentional collected despawn, not generic cleanup/despawn.
        if (!_collected || _rewardApplied)
            return;

        _rewardApplied = true;
        int awardValue = Mathf.Max(1, value);
        RuntimeCollectedValue += awardValue;
        
        if (pickupAudio != null)
            AudioSource.PlayClipAtPoint(pickupAudio, transform.position, pickupAudioVolume);
        
        // Trigger alert showing loot gained.
        var hud = FindFirstObjectByType<HUDController>();
        if (hud != null)
            hud.ShowAlert($"+{awardValue}");
        
        onCollected?.Invoke();
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
