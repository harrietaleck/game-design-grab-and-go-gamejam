using UnityEngine;


public class RoadSpawner : MonoBehaviour
{
    [Header("Road Tile Setup")]
    [Tooltip("Single road segment prefab. Used when roadTilePrefabs is empty.")]
    public GameObject roadTilePrefab;

    [Tooltip("If this array has any elements, each new tile randomly picks one prefab (weighted options: duplicate the same prefab in the list). Leave empty to only use roadTilePrefab.")]
    public GameObject[] roadTilePrefabs;

    public int initialTiles = 5;

    [Tooltip("Forward spacing between tile starts. Use the same value for all road tile variants, or gaps/overlaps will look wrong.")]
    public float tileLength = 25f;

    [Header("Endless Spawning (keep around player)")]
    [Tooltip("If set, the spawner will continuously create/destroy tiles based on this Transform's Z position.")]
    public Transform handlerTransform;

    [Tooltip("Spawn tiles so that the next tile start is always before (playerZ + spawnDistanceAhead).")]
    public float spawnDistanceAhead = 120f;

    [Tooltip("Destroy tiles when their Z is behind (playerZ - despawnDistanceBehind).")]
    public float despawnDistanceBehind = 50f;

    [Tooltip("Safety cap for how many tiles can exist at once.")]
    public int maxTiles = 20;

    [Header("Obstacle Spawning (per tile)")]
    [Tooltip("If set, obstacles are spawned at the same time as each road tile.")]
    public bool spawnObstaclesWithTiles = true;

    [Tooltip("Optional obstacle prefab. If null, the spawner creates red cube obstacles.")]
    public GameObject obstaclePrefab;

    [Header("Obstacle Row Count (never 3)")]
    [Tooltip("If true, spawns 1 obstacle on a row then 2 obstacles on the next row, alternating. If false, picks randomly between 1 vs 2 per row.")]
    public bool oneThenTwoAlternating = false;

    [Tooltip("Only used when oneThenTwoAlternating is false. Chance [0..1] that a row spawns 2 obstacles (instead of 1). Lower = calmer street.")]
    [Range(0f, 1f)]
    public float twoObstacleRowChance = 0.35f;

    [Tooltip("Chance [0..1] that this tile row has no obstacles at all (breathing room).")]
    [Range(0f, 1f)]
    public float emptyRowChance = 0.22f;

    [Header("Obstacle Prefabs (multiple)")]
    [Tooltip("If set (size > 0), the spawner will randomly pick one prefab from this list for each obstacle instance.")]
    public GameObject[] obstaclePrefabs;

    [Tooltip("X distance between lanes.")]
    public float laneDistance = 2.5f;

    [Tooltip("World X position of the middle lane.")]
    public float laneCenterX = 0f;

    [Tooltip("Number of lanes.")]
    public int laneCount = 4;

    [Tooltip("Legacy setting (no longer used). Obstacle count is controlled by oneThenTwoAlternating / twoObstacleRowChance.")]
    [Range(0f, 1f)]
    public float obstacleChancePerLane = 0.35f;

    [Tooltip("Y position of the obstacle.")]
    public float obstacleY = 0f;

    [Tooltip("Obstacle Z within the tile as a fraction of tileLength (0 = tile start, 1 = tile end).")]
    [Range(0f, 1f)]
    public float obstacleZWithinTile = 0.6f;

    [Tooltip("Base size for primitive cube obstacles (also scaled by obstacleScaleMultiplier).")]
    public Vector3 obstacleSize = new Vector3(1f, 1.5f, 1f);

    [Tooltip("Uniform scale applied to all obstacles (prefabs and cubes). 0.75 = 75% size.")]
    [Range(0.1f, 2f)]
    public float obstacleScaleMultiplier = 0.75f;


    private float nextSpawnZ = 0f;
    private readonly System.Collections.Generic.List<GameObject> _spawnedTiles = new();
    private int _nextRowObstacleCount = 1;

    void Start()
    {
        // If no handler is set, just behave like the original prototype.
        laneCount = LaneMath.ClampLaneCount(laneCount);
        _nextRowObstacleCount = oneThenTwoAlternating ? 1 : 1;
        for (int i = 0; i < initialTiles; i++)
            SpawnTile();
    }

    private void Update()
    {
        if (handlerTransform == null)
            return;

        float playerZ = handlerTransform.position.z;

        // Spawn new tiles ahead.
        while (nextSpawnZ < playerZ + spawnDistanceAhead && _spawnedTiles.Count < maxTiles)
            SpawnTile();

        // Despawn tiles behind.
        while (_spawnedTiles.Count > 0 && _spawnedTiles[0] != null)
        {
            float tileZ = _spawnedTiles[0].transform.position.z;
            if (tileZ >= playerZ - despawnDistanceBehind)
                break;

            Destroy(_spawnedTiles[0]);
            _spawnedTiles.RemoveAt(0);
        }
    }

    void SpawnTile()
    {
        float tileStartZ = nextSpawnZ;
        GameObject prefab = GetRoadTilePrefab();
        if (prefab == null)
        {
            Debug.LogWarning("RoadSpawner: No road tile prefab assigned. Assign roadTilePrefab or add entries to roadTilePrefabs.");
            nextSpawnZ += tileLength;
            return;
        }

        var tileRoot = Instantiate(prefab, new Vector3(0, 0, tileStartZ), Quaternion.identity);

        // Keep a reference so we can despawn old tiles (and their child obstacles).
        _spawnedTiles.Add(tileRoot);

        if (spawnObstaclesWithTiles)
            SpawnObstaclesForTile(tileStartZ, tileRoot.transform);

        nextSpawnZ += tileLength;
    }

    private GameObject GetRoadTilePrefab()
    {
        if (roadTilePrefabs != null && roadTilePrefabs.Length > 0)
        {
            int i = Random.Range(0, roadTilePrefabs.Length);
            if (roadTilePrefabs[i] != null)
                return roadTilePrefabs[i];
            // Fallback if slot is null: try any non-null entry.
            for (int k = 0; k < roadTilePrefabs.Length; k++)
            {
                if (roadTilePrefabs[k] != null)
                    return roadTilePrefabs[k];
            }
        }

        return roadTilePrefab;
    }

    private void SpawnObstaclesForTile(float tileStartZ, Transform tileParent)
    {
        if (laneCount < 1)
            return;

        if (Random.value < emptyRowChance)
            return;

        float obstacleZ = tileStartZ + tileLength * obstacleZWithinTile;

        // Decide row obstacle count (1 or 2 only).
        int obstacleCount = 1;
        if (oneThenTwoAlternating)
        {
            obstacleCount = Mathf.Clamp(_nextRowObstacleCount, 1, 2);
            _nextRowObstacleCount = obstacleCount == 1 ? 2 : 1;
        }
        else
        {
            obstacleCount = Random.value < twoObstacleRowChance ? 2 : 1;
        }

        // Pick distinct random lanes.
        int[] laneIndices = new int[laneCount];
        for (int i = 0; i < laneCount; i++)
            laneIndices[i] = i;
        ShuffleInPlace(laneIndices);

        int lanesToUse = Mathf.Clamp(obstacleCount, 1, laneCount);
        for (int i = 0; i < lanesToUse; i++)
        {
            int laneIndex = laneIndices[i];

            float x = LaneMath.GetLaneX(laneIndex, laneCount, laneDistance, laneCenterX);
            var pos = new Vector3(x, obstacleY, obstacleZ);
            CreateAndSetupObstacle(pos, tileParent);
        }
    }

    private static void ShuffleInPlace(int[] array)
    {
        // Fisher-Yates shuffle.
        for (int i = array.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (array[i], array[j]) = (array[j], array[i]);
        }
    }

    private void CreateAndSetupObstacle(Vector3 pos, Transform parent)
    {
        GameObject go;

        GameObject prefabToUse = null;
        if (obstaclePrefabs != null && obstaclePrefabs.Length > 0)
            prefabToUse = obstaclePrefabs[Random.Range(0, obstaclePrefabs.Length)];
        else
            prefabToUse = obstaclePrefab;

        if (prefabToUse != null)
        {
            // Parent under the tile so destroying the tile also destroys its obstacles.
            go = Instantiate(prefabToUse, pos, Quaternion.identity, parent);
            go.transform.localScale = Vector3.Scale(go.transform.localScale, Vector3.one * obstacleScaleMultiplier);
        }
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.position = pos;
            go.transform.SetParent(parent, worldPositionStays: true);
            go.transform.localScale = Vector3.Scale(obstacleSize, Vector3.one * obstacleScaleMultiplier);

            if (go.TryGetComponent<Renderer>(out var r))
                r.material.color = new Color(0.9f, 0.25f, 0.2f, 1f);
        }

        LaneObstacleSpawner.EnsureObstacleSetup(go);
    }

}
