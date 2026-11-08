using UnityEngine;

/// <summary>The goal: a big van loaded with bags. AJ dives in first; the run is won when Claire reaches it.</summary>
public class GetawayVan : MonoBehaviour
{
    private const float VanScale = 1.7f;

    public float FinishZ { get; private set; }

    private PlayerLaneMovement _claire;
    private AIThiefLaneDodge _aj;
    private GameStartFlow _flow;
    private bool _ajAboard;

    public static GetawayVan Create(float z, PlayerLaneMovement claire, PoliceSet set)
    {
        var spawner = FindFirstObjectByType<RoadSpawner>();
        GameObject prefab = null;
        if (spawner != null && spawner.obstaclePrefabs != null)
            foreach (var p in spawner.obstaclePrefabs)
                if (p != null && p.name == "Van") prefab = p;

        var root = new GameObject("Getaway Van").AddComponent<GetawayVan>();
        root.transform.position = new Vector3(0f, spawner != null ? spawner.obstacleY : 0f, z);
        root._claire = claire;
        root._aj = FindFirstObjectByType<AIThiefLaneDodge>();
        root._flow = GameStartFlow.Instance;

        float rearZ = z - 3f;
        if (prefab != null)
        {
            var van = Instantiate(prefab, root.transform);
            van.transform.localPosition = Vector3.zero;
            van.transform.localScale *= VanScale;
            foreach (var c in van.GetComponentsInChildren<Collider>(true)) Destroy(c);
            var bounds = Bounds(van);
            van.transform.position += Vector3.up * (root.transform.position.y - bounds.min.y);
            rearZ = bounds.min.z;
            root.AddBags(bounds);
        }

        root.FinishZ = rearZ - 2.5f;
        root.AddBeacon();
        return root;
    }

    private void AddBags(Bounds vanBounds)
    {
        var shop = ShopSet.Active;
        if (shop == null || shop.bags == null || shop.bags.Length == 0) return;

        // A pile on the road behind the open van, and more strapped to the roof.
        for (int i = 0; i < 7; i++)
        {
            bool roof = i >= 4;
            float x = roof ? (i - 5) * 0.8f : (i - 1.5f) * 1.1f;
            float y = roof ? vanBounds.max.y : transform.position.y;
            float z = roof ? vanBounds.center.z + (i - 5) * 0.7f : vanBounds.min.z - 0.5f - (i % 2) * 0.5f;

            var bag = Instantiate(shop.bags[i % shop.bags.Length], transform);
            foreach (var c in bag.GetComponentsInChildren<Collider>(true)) Destroy(c);
            var b = Bounds(bag);
            if (b.size.y > 0.001f) bag.transform.localScale *= 0.85f / b.size.y;
            b = Bounds(bag);
            bag.transform.position += new Vector3(x, y, z) - new Vector3(b.center.x, b.min.y, b.center.z);
        }
    }

    private void AddBeacon()
    {
        var fx = Resources.Load<Material>("FxParticle");
        if (fx == null) return;
        var beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        beam.name = "Van Beacon";
        Destroy(beam.GetComponent<Collider>());
        var material = new Material(fx);
        material.SetColor("_BaseColor", new Color(0.3f, 1f, 0.45f, 0.28f));
        beam.GetComponent<Renderer>().sharedMaterial = material;
        beam.transform.SetParent(transform, false);
        beam.transform.localPosition = Vector3.up * 30f;
        beam.transform.localScale = new Vector3(3f, 30f, 3f);
    }

    private static Bounds Bounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.one);
        var b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);
        return b;
    }

    private void Update()
    {
        if (_flow == null || _flow.State != GameStartFlow.RunState.Playing || _claire == null) return;

        if (!_ajAboard && _aj != null && !_aj.IsCaught && _aj.transform.position.z >= FinishZ)
        {
            _ajAboard = true;
            _aj.BoardVan();
            if (_flow.hudController != null) _flow.hudController.ShowAlert("AJ'S IN! GET TO THE VAN!");
        }

        if (_claire.transform.position.z >= FinishZ)
        {
            ProceduralSfx.Play(ProceduralSfx.Rescue, 0.9f);
            _flow.EndRun(RunEndReason.ReachedVan);
        }
    }
}

/// <summary>Makes AJ jump a spike strip or slide under a barrier arm as he reaches it.</summary>
public class RunnerHazard : MonoBehaviour
{
    public bool slide;
    private bool _done;

    private void Update()
    {
        var police = PoliceDirector.Instance;
        if (_done || police == null || police.AJ == null || !GameStartFlow.IsGameplayActive) return;
        Vector3 aj = police.AJ.transform.position;
        float ahead = transform.position.z - aj.z;
        if (ahead < 0f)
        {
            _done = true;
            return;
        }
        if (ahead < 2.4f && Mathf.Abs(transform.position.x - aj.x) < LaneMath.SharedLaneDistance * 0.5f)
        {
            _done = true;
            police.AJ.PassHazard(slide);
        }
    }
}

/// <summary>A first-aid kit: restores some of Claire's health.</summary>
public class HealthPickup : MonoBehaviour
{
    private const float HealAmount = 20f;

    public static void Create(Vector3 position, Transform parent, Material white, Material red)
    {
        var root = new GameObject("First Aid Kit");
        root.transform.SetParent(parent, false);
        root.transform.position = position;
        Part(root.transform, white, Vector3.zero, new Vector3(0.75f, 0.75f, 0.3f));
        Part(root.transform, red, Vector3.back * 0.17f, new Vector3(0.5f, 0.15f, 0.03f));
        Part(root.transform, red, Vector3.back * 0.17f, new Vector3(0.15f, 0.5f, 0.03f));
        Part(root.transform, red, Vector3.forward * 0.17f, new Vector3(0.5f, 0.15f, 0.03f));
        Part(root.transform, red, Vector3.forward * 0.17f, new Vector3(0.15f, 0.5f, 0.03f));
        root.AddComponent<HealthPickup>();
    }

    private static void Part(Transform parent, Material material, Vector3 position, Vector3 size)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(go.GetComponent<Collider>());
        go.GetComponent<Renderer>().sharedMaterial = material;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = size;
    }

    private void Update()
    {
        transform.Rotate(Vector3.up, 120f * Time.deltaTime, Space.World);
        var flow = GameStartFlow.Instance;
        var police = PoliceDirector.Instance;
        if (flow == null || police == null || police.Claire == null || !GameStartFlow.IsGameplayActive) return;

        Vector3 c = police.Claire.transform.position;
        if (Mathf.Abs(transform.position.z - c.z) > 1.1f || Mathf.Abs(transform.position.x - c.x) > 1.4f) return;
        flow.SetClaireHealth(flow.ClaireHealth + HealAmount);
        ProceduralSfx.Play(ProceduralSfx.Rescue, 0.6f);
        if (flow.hudController != null) flow.hudController.ShowAlert($"+{HealAmount:0} HEALTH");
        Destroy(gameObject);
    }
}

/// <summary>Red and blue light washing over a roadblock.</summary>
public class RoadblockStrobe : MonoBehaviour
{
    private Light _light;

    private void Start()
    {
        _light = new GameObject("Strobe").AddComponent<Light>();
        _light.transform.SetParent(transform, false);
        _light.transform.localPosition = Vector3.up * 3f;
        _light.type = LightType.Point;
        _light.range = 24f;
        _light.intensity = 9f;
        _light.shadows = LightShadows.None;
    }

    private void Update()
    {
        bool red = !GameSettings.ScreenFlash || Mathf.Repeat(Time.time * 3.5f, 1f) < 0.5f;
        _light.color = red ? new Color(1f, 0.12f, 0.1f) : new Color(0.15f, 0.35f, 1f);
    }
}
