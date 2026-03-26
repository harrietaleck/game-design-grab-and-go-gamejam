using System;
using UnityEngine;

[RequireComponent(typeof(AIThiefLaneDodge))]
public class ThiefCollectibleDropper : MonoBehaviour
{
    [Serializable]
    public class DropEntry
    {
        public GameObject prefab;
        [Min(1)] public int weight = 1;
        [Min(1)] public int fallbackValue = 1;
        [Tooltip("If enabled, this drop spins infinitely.")]
        public bool rotateOnSpawn = false;
        [Tooltip("Spin speed used when rotateOnSpawn is enabled.")]
        public float spinSpeed = 240f;
        public AudioClip pickupAudio;
        [Range(0f, 1f)] public float pickupAudioVolume = 1f;
    }

    [Header("Drops")]
    public DropEntry[] dropTable;
    [Range(0f, 1f)] public float dropChancePerInterval = 0.7f;
    public float minDropInterval = 0.8f;
    public float maxDropInterval = 1.8f;

    [Header("Placement")]
    public float dropYOffset = 0.25f;
    public float backOffsetMin = 0.3f;
    public float backOffsetMax = 1.2f;
    public float lateralJitter = 0.2f;
    [Tooltip("Optional explicit player reference. If empty, first PlayerLaneMovement is used.")]
    public Transform playerTransform;
    [Tooltip("If enabled, enforce a minimum spawn Z ahead of player.")]
    public bool enforceMinAheadOfPlayer = false;
    [Tooltip("Used only when enforceMinAheadOfPlayer is true.")]
    public float minSpawnAheadOfPlayerZ = 2.5f;
    [Tooltip("Try to parent drop under detected road tile root.")]
    public bool parentToTileRoot = true;

    [Header("Pickup Feel")]
    [Tooltip("Enable proximity pickup in addition to trigger collision.")]
    public bool useProximityPickup = true;
    [Tooltip("Radius used for proximity pickup.")]
    public float pickupRadius = 0.7f;
    [Tooltip("Delay before a fresh drop can be picked up.")]
    public float pickupDelaySeconds = 0.25f;

    private float _nextDropAt;
    private Collider[] _thiefColliders;

    private void Awake()
    {
        _thiefColliders = GetComponentsInChildren<Collider>(includeInactive: false);
        if (playerTransform == null)
        {
            var player = FindFirstObjectByType<PlayerLaneMovement>();
            if (player != null)
                playerTransform = player.transform;
        }
        ScheduleNext();
    }

    private void Update()
    {
        if (Time.time < _nextDropAt)
            return;

        ScheduleNext();
        if (UnityEngine.Random.value > dropChancePerInterval)
            return;

        DropEntry entry = PickEntry();
        if (entry == null || entry.prefab == null)
            return;

        SpawnDrop(entry);
    }

    private void SpawnDrop(DropEntry entry)
    {
        float x = transform.position.x + UnityEngine.Random.Range(-lateralJitter, lateralJitter);
        float z = transform.position.z - UnityEngine.Random.Range(backOffsetMin, backOffsetMax);
        if (enforceMinAheadOfPlayer && playerTransform != null)
            z = Mathf.Max(z, playerTransform.position.z + minSpawnAheadOfPlayerZ);
        var pos = new Vector3(x, transform.position.y + dropYOffset, z);

        Transform tileParent = parentToTileRoot ? FindTileParent(pos) : null;
        Quaternion spawnRotation = entry.prefab.transform.rotation;
        GameObject go = tileParent != null
            ? Instantiate(entry.prefab, pos, spawnRotation, tileParent)
            : Instantiate(entry.prefab, pos, spawnRotation);

        CollectiblePickup.EnsureSetup(go, entry.fallbackValue);
        if (entry.rotateOnSpawn)
        {
            var spinner = go.GetComponent<CollectibleSpin>();
            if (spinner == null)
                spinner = go.AddComponent<CollectibleSpin>();
            spinner.axis = Vector3.forward;
            spinner.speedDegreesPerSecond = Mathf.Abs(entry.spinSpeed);
        }
        var pickup = go.GetComponent<CollectiblePickup>();
        if (pickup != null)
        {
            pickup.value = Mathf.Max(1, entry.fallbackValue);
            pickup.useProximityPickup = useProximityPickup;
            pickup.pickupRadius = Mathf.Max(0.05f, pickupRadius);
            pickup.pickupDelaySeconds = Mathf.Max(0f, pickupDelaySeconds);
            pickup.pickupAudio = entry.pickupAudio;
            pickup.pickupAudioVolume = entry.pickupAudioVolume;
        }
        IgnoreThiefCollision(go);
    }

    private Transform FindTileParent(Vector3 spawnPos)
    {
        Vector3 origin = spawnPos + Vector3.up * 8f;
        if (Physics.Raycast(origin, Vector3.down, out var hit, 25f, ~0, QueryTriggerInteraction.Ignore))
        {
            // Parent only if we clearly hit a road tile hierarchy.
            Transform root = hit.collider.transform.root;
            if (root != null && root.name.Contains("RoadTile", StringComparison.OrdinalIgnoreCase))
                return root;
        }
        return null;
    }

    private void IgnoreThiefCollision(GameObject drop)
    {
        if (drop == null || _thiefColliders == null)
            return;

        var dropColliders = drop.GetComponentsInChildren<Collider>(includeInactive: false);
        for (int i = 0; i < _thiefColliders.Length; i++)
        {
            if (_thiefColliders[i] == null)
                continue;
            for (int j = 0; j < dropColliders.Length; j++)
            {
                if (dropColliders[j] == null)
                    continue;
                Physics.IgnoreCollision(_thiefColliders[i], dropColliders[j], true);
            }
        }
    }

    private void ScheduleNext()
    {
        float min = Mathf.Max(0.05f, minDropInterval);
        float max = Mathf.Max(min, maxDropInterval);
        _nextDropAt = Time.time + UnityEngine.Random.Range(min, max);
    }

    private DropEntry PickEntry()
    {
        if (dropTable == null || dropTable.Length == 0)
            return null;

        int total = 0;
        for (int i = 0; i < dropTable.Length; i++)
        {
            if (dropTable[i] != null && dropTable[i].prefab != null)
                total += Mathf.Max(1, dropTable[i].weight);
        }
        if (total <= 0)
            return null;

        int roll = UnityEngine.Random.Range(0, total);
        int running = 0;
        for (int i = 0; i < dropTable.Length; i++)
        {
            var e = dropTable[i];
            if (e == null || e.prefab == null)
                continue;
            running += Mathf.Max(1, e.weight);
            if (roll < running)
                return e;
        }
        return null;
    }
}
