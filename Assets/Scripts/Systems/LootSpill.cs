using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Visual coin spill when Claire loses loot in a crash (GameEventType.LootSpilled).
/// Uses the game's own coin / gold-stack prefabs from AJ's drop table, bursts them out of Claire's backpack
/// on scripted arcs (no physics), lets them bounce on the road behind her and fade out.
/// Purely visual: the score was already reduced by GameStartFlow.SpillLoot, and spilled coins can't be collected.
/// </summary>
public class LootSpill : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        GameStartFlow.RegisterBootstrap(flow =>
        {
            if (flow.GetComponent<LootSpill>() == null)
                flow.gameObject.AddComponent<LootSpill>();
        });
    }

    [Tooltip("Most coins shown for one spill (one per loot point, gold stacks for big losses).")]
    public int maxCoins = 14;
    public float burstUpSpeed = 5.5f;
    public float burstSideSpeed = 3f;
    [Tooltip("Coins keep this share of Claire's forward speed (so they stay on screen and slide back past her).")]
    public Vector2 forwardMomentum = new Vector2(0.55f, 0.95f);
    public float gravity = 14f;
    public float lifetime = 2f;
    [Tooltip("Spilled coins are drawn a bit larger than pickups so the burst reads at gameplay distance.")]
    public float coinScale = 1.4f;
    [Range(0f, 1f)] public float spillVolume = 0.9f;

    private class Coin
    {
        public Transform t;
        public Vector3 velocity;
        public Vector3 spin;
        public float born;
        public Vector3 baseScale;
        public bool landed;
    }

    private readonly List<Coin> _coins = new();
    private Transform _claire;
    private float _claireSpeed = 6f;
    private Transform _bag;
    private ThiefCollectibleDropper _dropper;
    private float _groundY;

    private void Start()
    {
        var claire = FindFirstObjectByType<PlayerLaneMovement>();
        if (claire != null)
        {
            _claire = claire.transform;
            _groundY = _claire.position.y;
            _claireSpeed = claire.forwardSpeed;
        }
        _dropper = FindFirstObjectByType<ThiefCollectibleDropper>();
    }

    private void OnEnable() => GameEvents.OnEvent += OnGameEvent;
    private void OnDisable() => GameEvents.OnEvent -= OnGameEvent;

    private void OnGameEvent(GameEventData e)
    {
        if (e.type == GameEventType.LootSpilled && e.value > 0)
            Spill(e.value);
    }

    private void Spill(int lostValue)
    {
        if (_claire == null) return;
        if (_bag == null)
        {
            foreach (var t in _claire.GetComponentsInChildren<Transform>(true))
                if (t.name == CharacterBackpacks.BackpackObjectName) { _bag = t; break; }
        }
        Vector3 origin = _bag != null ? _bag.position : _claire.position + Vector3.up * 1.2f;

        GameObject coinPrefab = DropPrefab(small: true);
        GameObject stackPrefab = DropPrefab(small: false);
        if (coinPrefab == null && stackPrefab == null) return;

        // Show roughly one coin per loot point; use gold stacks (worth 5) for larger losses.
        int stacks = stackPrefab != null ? Mathf.Min(lostValue / 5, maxCoins / 3) : 0;
        int coins = Mathf.Clamp(lostValue - stacks * 5, 0, maxCoins - stacks);
        if (coinPrefab == null) { stacks += coins; coins = 0; }

        for (int i = 0; i < stacks; i++) Launch(stackPrefab, origin);
        for (int i = 0; i < coins; i++) Launch(coinPrefab, origin);

        var clip = SpillClip();
        if (clip != null) AudioSource.PlayClipAtPoint(clip, origin, spillVolume);
    }

    private void Launch(GameObject prefab, Vector3 origin)
    {
        if (prefab == null) return;
        var go = Instantiate(prefab, origin + Random.insideUnitSphere * 0.15f, Random.rotation);
        go.name = "Spilled Coin";
        // Visual only: no pickups, physics or scripts from the prefab.
        foreach (var c in go.GetComponentsInChildren<Collider>(true)) Destroy(c);
        foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true)) Destroy(rb);
        foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true)) if (mb != null) Destroy(mb);

        // Out of the bag: up and sideways, keeping most of Claire's forward momentum, then sliding back past her.
        var v = new Vector3(Random.Range(-burstSideSpeed, burstSideSpeed),
                            Random.Range(burstUpSpeed * 0.6f, burstUpSpeed),
                            _claireSpeed * Random.Range(forwardMomentum.x, forwardMomentum.y));
        go.transform.localScale *= coinScale;
        _coins.Add(new Coin
        {
            t = go.transform,
            velocity = v,
            spin = Random.onUnitSphere * Random.Range(360f, 900f),
            born = Time.time,
            baseScale = go.transform.localScale
        });
    }

    private void Update()
    {
        // Frozen while paused / after the run (scaled time), cleaned up with the scene on restart.
        float dt = Time.deltaTime;
        for (int i = _coins.Count - 1; i >= 0; i--)
        {
            var c = _coins[i];
            if (c.t == null) { _coins.RemoveAt(i); continue; }
            float age = Time.time - c.born;
            if (age >= lifetime)
            {
                Destroy(c.t.gameObject);
                _coins.RemoveAt(i);
                continue;
            }

            c.velocity.y -= gravity * dt;
            Vector3 p = c.t.position + c.velocity * dt;
            if (p.y <= _groundY + 0.05f && c.velocity.y < 0f)
            {
                // Bounce and skid on the road.
                p.y = _groundY + 0.05f;
                c.velocity.y = -c.velocity.y * 0.35f;
                c.velocity.x *= 0.6f;
                c.velocity.z *= 0.45f; // road friction: coins stop and Claire runs past them
                c.spin *= 0.5f;
                c.landed = true;
            }
            c.t.position = p;
            c.t.Rotate(c.spin * dt, Space.World);

            // Shrink away over the last third of its life.
            float fade = Mathf.Clamp01((lifetime - age) / (lifetime * 0.33f));
            c.t.localScale = c.baseScale * fade;
        }
    }

    private GameObject DropPrefab(bool small)
    {
        if (_dropper == null || _dropper.dropTable == null) return null;
        GameObject best = null;
        int bestValue = small ? int.MaxValue : int.MinValue;
        foreach (var e in _dropper.dropTable)
        {
            if (e == null || e.prefab == null) continue;
            if (small ? e.fallbackValue < bestValue : e.fallbackValue > bestValue)
            {
                bestValue = e.fallbackValue;
                best = e.prefab;
            }
        }
        return best;
    }

    private AudioClip SpillClip()
    {
        if (_dropper == null || _dropper.dropTable == null) return null;
        AudioClip clip = null;
        int bestValue = int.MinValue;
        foreach (var e in _dropper.dropTable)
            if (e != null && e.pickupAudio != null && e.fallbackValue > bestValue) { bestValue = e.fallbackValue; clip = e.pickupAudio; }
        return clip; // the clinking-coins sound
    }
}
