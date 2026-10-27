using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Turns the endless road into a city street using the Cartoon City (ithappy) prefabs already in the project:
/// - hides the squashed placeholder buildings baked into each RoadTile (the road itself is untouched),
/// - streams properly scaled buildings along both sides plus a taller skyline row behind them,
/// - dresses every tile with sidewalks, street lights, palms/bushes, bins, bus stops, billboards and traffic lights,
/// - applies the pack's skybox and haze.
/// Pure decoration: colliders on decor are disabled, so lanes, obstacles, loot and AJ's AI are unaffected.
/// </summary>
public class CityDresser : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        GameStartFlow.RegisterBootstrap(flow =>
        {
            var spawner = FindFirstObjectByType<RoadSpawner>();
            if (spawner != null && spawner.GetComponent<CityDresser>() == null)
                spawner.gameObject.AddComponent<CityDresser>();
        });
    }

    [Header("General")]
    public bool dressCity = true;
    [Tooltip("Hide the stretched placeholder buildings inside each RoadTile prefab.")]
    public bool hideTileBuildings = true;
    public string dressingSetResource = "CityDressingSet";

    [Header("Street layout")]
    [Tooltip("Used if the road width cannot be measured from the road mesh.")]
    public float fallbackRoadHalfWidth = 8f;
    public float sidewalkWidth = 4.5f;
    [Tooltip("Extra space between the sidewalk and building fronts.")]
    public float buildingSetback = 0.6f;

    [Header("Buildings")]
    public Vector2 frontageRange = new Vector2(9f, 16f);
    public float maxBuildingHeight = 38f;
    public float maxBuildingDepth = 16f;
    public Vector2 gapRange = new Vector2(0.6f, 2.5f);
    [Range(0f, 1f)] public float plazaChance = 0.12f;
    public float plazaLength = 9f;
    public bool skylineRow = true;
    public float skylineExtraOffset = 22f;

    [Header("Streaming")]
    public float spawnAhead = 170f;
    public float despawnBehind = 45f;

    private CityDressingSet _set;
    private RoadSpawner _spawner;
    private Transform _handler;
    private Transform _root;
    private float _roadHalfWidth = -1f;
    private float _groundY;
    private int _tileIndex;

    private class Stream
    {
        public float side;      // -1 left, +1 right
        public bool skyline;
        public float cursorZ;
        public readonly List<(GameObject go, float maxZ)> items = new();
    }
    private readonly List<Stream> _streams = new();
    private bool _streamsReady;

    private void Awake()
    {
        _spawner = GetComponent<RoadSpawner>();
        _set = Resources.Load<CityDressingSet>(dressingSetResource);
        if (!dressCity || _set == null)
        {
            if (_set == null) Debug.Log("[CityDresser] No CityDressingSet in Resources; keeping the original street.");
            enabled = false;
            return;
        }
        if (_spawner != null) _spawner.TileSpawned += OnTileSpawned;
        _root = new GameObject("City (runtime)").transform;
    }

    private void OnDestroy()
    {
        if (_spawner != null) _spawner.TileSpawned -= OnTileSpawned;
    }

    private void Start()
    {
        _handler = _spawner != null && _spawner.handlerTransform != null
            ? _spawner.handlerTransform
            : FindFirstObjectByType<PlayerLaneMovement>()?.transform;

        ApplyAtmosphere();

        // The RoadTile placed directly in MainScene also has placeholder buildings.
        if (hideTileBuildings)
        {
            foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (t.parent == null && t.name == "RoadTile") HideTileBuildings(t.gameObject);
        }
        EnsureStreams();
    }

    private void Update()
    {
        if (!_streamsReady || _handler == null) return;
        float z = _handler.position.z;
        foreach (var s in _streams)
        {
            int guard = 0;
            while (s.cursorZ < z + spawnAhead && guard++ < 8)
                SpawnBuilding(s);
            for (int i = s.items.Count - 1; i >= 0; i--)
            {
                if (s.items[i].go == null) { s.items.RemoveAt(i); continue; }
                if (s.items[i].maxZ < z - despawnBehind)
                {
                    Destroy(s.items[i].go);
                    s.items.RemoveAt(i);
                }
            }
        }
    }

    // ---------------- Tiles ----------------

    private void OnTileSpawned(GameObject tile, float startZ, float length)
    {
        if (!enabled || tile == null) return;
        if (_roadHalfWidth < 0f) MeasureRoad(tile);
        if (hideTileBuildings) HideTileBuildings(tile);
        DressTile(tile.transform, startZ, length, _tileIndex++);
        EnsureStreams();
    }

    private static void HideTileBuildings(GameObject tile)
    {
        foreach (Transform child in tile.transform)
            if (child.name.Contains("Building")) child.gameObject.SetActive(false);
    }

    private void MeasureRoad(GameObject tile)
    {
        _roadHalfWidth = fallbackRoadHalfWidth;
        _groundY = 0f;
        foreach (var r in tile.GetComponentsInChildren<Renderer>(true))
        {
            if (!r.name.ToLowerInvariant().Contains("road")) continue;
            var b = r.bounds;
            float half = Mathf.Max(Mathf.Abs(b.min.x), Mathf.Abs(b.max.x));
            if (half > 6.2f && half < 14f) _roadHalfWidth = half;
            if (b.max.y > -1.5f && b.max.y < 1.5f) _groundY = b.max.y;
            break;
        }
        // Never closer than the playable lanes plus a margin.
        float lanesHalf = LaneMath.SharedLaneCount * LaneMath.SharedLaneDistance * 0.5f;
        _roadHalfWidth = Mathf.Max(_roadHalfWidth, lanesHalf + 1.5f);
    }

    private void DressTile(Transform tile, float z0, float len, int index)
    {
        float edge = _roadHalfWidth;
        for (int sideIndex = 0; sideIndex < 2; sideIndex++)
        {
            float side = sideIndex == 0 ? -1f : 1f;
            float faceRoadYaw = side < 0 ? 90f : -90f;

            // Sidewalk slab filling edge .. edge + sidewalkWidth
            var walk = SpawnFitted(Pick(_set.sidewalkTiles), tile, new Vector3(side * (edge + sidewalkWidth * 0.5f), 0f, z0 + len * 0.5f),
                                   new Vector2(sidewalkWidth, len + 0.05f));

            // Street light, alternating sides
            if ((index + sideIndex) % 2 == 0)
                SpawnProp(Pick(_set.streetLights), tile, new Vector3(side * (edge + 0.7f), _groundY, z0 + len * 0.5f), faceRoadYaw);

            // Greenery
            float r = Random.value;
            if (r < 0.55f)
                SpawnProp(Pick(_set.trees), tile, new Vector3(side * (edge + sidewalkWidth * 0.65f), _groundY, z0 + Random.Range(1.5f, len - 1.5f)), Random.Range(0f, 360f));
            else if (r < 0.9f)
                SpawnProp(Pick(_set.bushes), tile, new Vector3(side * (edge + sidewalkWidth * 0.75f), _groundY, z0 + Random.Range(1.5f, len - 1.5f)), Random.Range(0f, 360f));

            // Bins
            if (Random.value < 0.3f)
                SpawnProp(Pick(_set.trashCans), tile, new Vector3(side * (edge + 1.3f), _groundY, z0 + Random.Range(1f, len - 1f)), faceRoadYaw);
        }

        // Occasional landmarks (one side at a time so they never crowd each other)
        float landmarkSide = index % 2 == 0 ? -1f : 1f;
        float landmarkYaw = landmarkSide < 0 ? 90f : -90f;
        if (index % 7 == 3)
            SpawnProp(Pick(_set.busStops), tile, new Vector3(landmarkSide * (edge + sidewalkWidth * 0.45f), _groundY, z0 + len * 0.5f), landmarkYaw);
        else if (index % 5 == 1)
            SpawnProp(Pick(_set.billboards), tile, new Vector3(landmarkSide * (edge + sidewalkWidth * 0.8f), _groundY, z0 + len * 0.5f), landmarkYaw);
        if (index % 12 == 6)
        {
            SpawnProp(Pick(_set.trafficLights), tile, new Vector3(-(edge + 0.5f), _groundY, z0 + 1f), 90f);
            SpawnProp(Pick(_set.trafficLights), tile, new Vector3(edge + 0.5f, _groundY, z0 + 1f), -90f);
        }
    }

    // ---------------- Buildings ----------------

    private void EnsureStreams()
    {
        if (_streamsReady || _roadHalfWidth < 0f || _handler == null || _set.buildings == null || _set.buildings.Length == 0) return;
        float startZ = _handler.position.z - despawnBehind;
        _streams.Add(new Stream { side = -1f, cursorZ = startZ });
        _streams.Add(new Stream { side = 1f, cursorZ = startZ + 4f });
        if (skylineRow)
        {
            _streams.Add(new Stream { side = -1f, skyline = true, cursorZ = startZ - 10f });
            _streams.Add(new Stream { side = 1f, skyline = true, cursorZ = startZ - 3f });
        }
        _streamsReady = true;
    }

    private void SpawnBuilding(Stream s)
    {
        float frontX = _roadHalfWidth + sidewalkWidth + buildingSetback + (s.skyline ? skylineExtraOffset : 0f);

        if (!s.skyline && Random.value < plazaChance)
        {
            // Small plaza: open space with a fountain or greenery.
            var plazaRoot = new GameObject("Plaza").transform;
            plazaRoot.SetParent(_root, false);
            float cz = s.cursorZ + plazaLength * 0.5f;
            var feature = Random.value < 0.5f ? Pick(_set.fountains) : Pick(_set.trees);
            SpawnProp(feature, plazaRoot, new Vector3(s.side * (frontX + 4f), _groundY, cz), Random.Range(0f, 360f));
            SpawnProp(Pick(_set.bushes), plazaRoot, new Vector3(s.side * (frontX + 1f), _groundY, cz - 3f), 0f);
            SpawnProp(Pick(_set.bushes), plazaRoot, new Vector3(s.side * (frontX + 1f), _groundY, cz + 3f), 0f);
            s.items.Add((plazaRoot.gameObject, s.cursorZ + plazaLength));
            s.cursorZ += plazaLength;
            return;
        }

        var prefab = Pick(_set.buildings);
        if (prefab == null) { s.cursorZ += frontageRange.x; return; }

        var go = Instantiate(prefab, _root);
        go.name = prefab.name;
        DisableColliders(go);

        // Long side faces the street; right side turned around to face the road too.
        go.transform.rotation = Quaternion.identity;
        var b = ComputeBounds(go);
        float yaw = b.size.x > b.size.z ? 90f : 0f;
        if (s.side > 0f) yaw += 180f;
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        b = ComputeBounds(go);

        float heightLimit = s.skyline ? maxBuildingHeight * 1.8f : maxBuildingHeight;
        float frontage = Random.Range(frontageRange.x, frontageRange.y) * (s.skyline ? 1.5f : 1f);
        float scale = frontage / Mathf.Max(0.01f, b.size.z);
        scale = Mathf.Min(scale, heightLimit / Mathf.Max(0.01f, b.size.y), (s.skyline ? maxBuildingDepth * 2f : maxBuildingDepth) / Mathf.Max(0.01f, b.size.x));
        scale = Mathf.Clamp(scale, 0.02f, 50f);
        go.transform.localScale = prefab.transform.localScale * scale;
        b = ComputeBounds(go);

        // Front face at frontX, base on the ground, starting at the cursor.
        float nearX = s.side < 0f ? b.max.x : b.min.x;
        float targetNearX = s.side * frontX;
        go.transform.position += new Vector3(targetNearX - nearX, _groundY - b.min.y, s.cursorZ - b.min.z);

        float maxZ = s.cursorZ + b.size.z;
        s.items.Add((go, maxZ));
        s.cursorZ = maxZ + Random.Range(gapRange.x, gapRange.y);
    }

    // ---------------- Helpers ----------------

    private GameObject SpawnProp(GameObject prefab, Transform parent, Vector3 worldPos, float yaw)
    {
        if (prefab == null) return null;
        var go = Instantiate(prefab, worldPos, Quaternion.Euler(0f, yaw, 0f) * prefab.transform.rotation, parent);
        DisableColliders(go);
        // Sit on the ground.
        var b = ComputeBounds(go);
        go.transform.position += new Vector3(0f, _groundY - b.min.y, 0f);
        return go;
    }

    /// <summary>Instantiates a flat prefab and stretches it to exactly cover size (x by z), top just above the road.</summary>
    private GameObject SpawnFitted(GameObject prefab, Transform parent, Vector3 center, Vector2 size)
    {
        if (prefab == null) return null;
        var go = Instantiate(prefab, center, prefab.transform.rotation, parent);
        DisableColliders(go);
        var b = ComputeBounds(go);
        if (b.size.x > 0.001f && b.size.z > 0.001f)
        {
            var s = go.transform.localScale;
            go.transform.localScale = new Vector3(s.x * size.x / b.size.x, s.y, s.z * size.y / b.size.z);
            b = ComputeBounds(go);
        }
        go.transform.position += new Vector3(center.x - b.center.x, (_groundY + 0.15f) - b.max.y, center.z - b.center.z); // slight curb
        return go;
    }

    private static GameObject Pick(GameObject[] list)
    {
        if (list == null || list.Length == 0) return null;
        return list[Random.Range(0, list.Length)];
    }

    private static void DisableColliders(GameObject go)
    {
        foreach (var c in go.GetComponentsInChildren<Collider>(true)) c.enabled = false;
    }

    private static Bounds ComputeBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.one);
        var b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        return b;
    }

    private void ApplyAtmosphere()
    {
        if (_set.skybox != null)
        {
            RenderSettings.skybox = _set.skybox;
            DynamicGI.UpdateEnvironment();
        }
        if (_set.useFog)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = _set.fogColor;
            RenderSettings.fogDensity = _set.fogDensity;
        }
    }
}
