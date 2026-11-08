using UnityEngine;

/// <summary>A visible bullet: flies straight, hurts Claire only if she is still in its path, and sparks on impact.</summary>
public class PoliceBullet : MonoBehaviour
{
    private const float LifeSeconds = 2.5f;
    private const float HitRadiusX = 0.7f;

    private PoliceDirector _director;
    private Material _material;
    private Vector3 _velocity;
    private float _dieAt;

    public static void Spawn(PoliceDirector director, Material material, Vector3 origin, Vector3 direction, float speed)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = "Police Bullet";
        Destroy(go.GetComponent<Collider>());
        go.GetComponent<Renderer>().sharedMaterial = material;
        go.transform.SetPositionAndRotation(origin, Quaternion.FromToRotation(Vector3.up, direction));
        go.transform.localScale = new Vector3(0.13f, 0.45f, 0.13f);

        var trail = go.AddComponent<TrailRenderer>();
        trail.sharedMaterial = material;
        trail.time = 0.16f;
        trail.startWidth = 0.16f;
        trail.endWidth = 0f;

        var bullet = go.AddComponent<PoliceBullet>();
        bullet._director = director;
        bullet._material = material;
        bullet._velocity = direction.normalized * speed;
        bullet._dieAt = Time.time + LifeSeconds;
    }

    private void Update()
    {
        var flow = GameStartFlow.Instance;
        if (flow == null || !GameStartFlow.IsGameplayActive) return; // frozen while paused or after the run
        if (flow.State != GameStartFlow.RunState.Playing || _director == null || _director.Claire == null || Time.time >= _dieAt)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 from = transform.position;
        Vector3 to = from + _velocity * Time.deltaTime;
        transform.position = to;

        Vector3 claire = _director.Claire.transform.position;
        if (from.z - claire.z > 0f && to.z - claire.z <= 0.3f && Mathf.Abs(to.x - claire.x) < HitRadiusX)
        {
            ImpactSparks.Burst(new Vector3(to.x, claire.y + 1.2f, claire.z), _material, 10);
            _director.OnBulletHitClaire();
            Destroy(gameObject);
            return;
        }

        if (to.y <= claire.y)
        {
            ImpactSparks.Burst(to, _material, 6);
            Destroy(gameObject);
        }
    }
}

/// <summary>A short shower of glowing sparks.</summary>
public class ImpactSparks : MonoBehaviour
{
    private const float Life = 0.35f;
    private Transform[] _sparks;
    private Vector3[] _velocity;
    private float _born;

    public static void Burst(Vector3 position, Material material, int count)
    {
        var root = new GameObject("Impact Sparks").AddComponent<ImpactSparks>();
        root.transform.position = position;
        root._born = Time.time;
        root._sparks = new Transform[count];
        root._velocity = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            var spark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(spark.GetComponent<Collider>());
            spark.GetComponent<Renderer>().sharedMaterial = material;
            spark.transform.SetParent(root.transform, false);
            spark.transform.localScale = Vector3.one * 0.09f;
            root._sparks[i] = spark.transform;
            root._velocity[i] = Random.onUnitSphere * Random.Range(3f, 7f) + Vector3.up * 2f;
        }
    }

    private void Update()
    {
        float age = Time.time - _born;
        if (age >= Life)
        {
            Destroy(gameObject);
            return;
        }
        float scale = 0.09f * (1f - age / Life);
        for (int i = 0; i < _sparks.Length; i++)
        {
            _velocity[i] += Vector3.down * (18f * Time.deltaTime);
            _sparks[i].position += _velocity[i] * Time.deltaTime;
            _sparks[i].localScale = Vector3.one * scale;
        }
    }
}
