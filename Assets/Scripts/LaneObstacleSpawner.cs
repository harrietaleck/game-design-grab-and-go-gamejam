using System.Collections.Generic;
using UnityEngine;

public class LaneObstacleSpawner : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Object that advances forward in Z. Used for spawn/despawn positions.")]
    public Transform handlerTransform;

    [Header("Lane Setup")]
    [Tooltip("X distance between lanes (should match PlayerLaneMovement.laneDistance).")]
    public float laneDistance = 2.5f;

    [Tooltip("World X position of the middle lane.")]
    public float laneCenterX = 0f;

    [Tooltip("Number of lanes. Your project uses 3 by default.")]
    public int laneCount = 3;

    [Header("Spawning")]
    public float spawnIntervalSeconds = 1.2f;
    public float spawnAheadZ = 40f;
    public float despawnBehindZ = 20f;
    public float spawnZJitter = 8f;
    public int maxActiveObstacles = 40;

    [Header("Obstacle Creation")]
    [Tooltip("Optional obstacle prefab. If left null, spawner will create primitive cubes.")]
    public GameObject obstaclePrefab;

    [Tooltip("Primitive type used when obstaclePrefab is null.")]
    public PrimitiveType fallbackPrimitive = PrimitiveType.Cube;

    public Vector3 obstacleSize = new Vector3(1f, 1.5f, 1f);

    [Tooltip("Uniform scale for spawned obstacles (matches RoadSpawner default).")]
    [Range(0.1f, 2f)]
    public float obstacleScaleMultiplier = 0.75f;

    public float obstacleY = 0.0f;

    [Tooltip("If true, adds Rigidbody + sets collider as trigger for correct OnTriggerEnter.")]
    public bool ensureTriggerColliders = true;

    private readonly List<GameObject> _spawned = new();
    private float _nextSpawnTime;

    private void Start()
    {
        _nextSpawnTime = Time.time + 0.25f;
    }

    private void Update()
    {
        if (handlerTransform == null)
            return;

        // Despawn old obstacles.
        float despawnZ = handlerTransform.position.z - despawnBehindZ;
        for (int i = _spawned.Count - 1; i >= 0; i--)
        {
            if (_spawned[i] == null)
            {
                _spawned.RemoveAt(i);
                continue;
            }

            if (_spawned[i].transform.position.z < despawnZ)
            {
                Destroy(_spawned[i]);
                _spawned.RemoveAt(i);
            }
        }

        // Spawn new ones.
        if (Time.time < _nextSpawnTime)
            return;

        if (_spawned.Count >= maxActiveObstacles)
            return;

        SpawnOne();
        _nextSpawnTime = Time.time + spawnIntervalSeconds;
    }

    private void SpawnOne()
    {
        if (laneCount < 1)
            return;

        int laneIndex = Random.Range(0, laneCount);
        float x = laneCenterX + (laneIndex - (laneCount - 1) * 0.5f) * laneDistance;

        float z = handlerTransform.position.z + spawnAheadZ + Random.Range(-spawnZJitter, spawnZJitter);

        var pos = new Vector3(x, obstacleY, z);
        var rot = Quaternion.identity;

        GameObject go;
        if (obstaclePrefab != null)
        {
            go = Instantiate(obstaclePrefab, pos, rot);
            go.transform.localScale = Vector3.Scale(go.transform.localScale, Vector3.one * obstacleScaleMultiplier);
        }
        else
        {
            go = GameObject.CreatePrimitive(fallbackPrimitive);
            go.transform.position = pos;
            go.transform.rotation = rot;
            go.transform.localScale = Vector3.Scale(obstacleSize, Vector3.one * obstacleScaleMultiplier);

            if (go.TryGetComponent<Renderer>(out var r))
                r.material.color = new Color(0.9f, 0.25f, 0.2f, 1f);
        }

        EnsureObstacleSetup(go);

        _spawned.Add(go);
    }

    public static void EnsureObstacleSetup(GameObject go)
    {
        // Add behavior if missing.
        if (go.GetComponent<LaneObstacle>() == null)
            go.AddComponent<LaneObstacle>();

        if (!go.TryGetComponent<Collider>(out var col))
            col = go.AddComponent<BoxCollider>();

        if (col is BoxCollider box)
        {
            // Ensure the collider actually matches something reasonable if we used primitives.
            box.size = Vector3.one;
        }

        col.isTrigger = true;

        // Trigger events require a Rigidbody on at least one object.
        if (!go.TryGetComponent<Rigidbody>(out var rb))
            rb = go.AddComponent<Rigidbody>();

        rb.isKinematic = true;
        rb.useGravity = false;
    }
}

