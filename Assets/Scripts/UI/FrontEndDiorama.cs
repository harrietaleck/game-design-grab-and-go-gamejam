using UnityEngine;

/// <summary>
/// Live backdrop for the front end: Claire and AJ sprinting down the real city street with a patrol car,
/// an officer and the helicopter on their heels. The rig moves forward and the road and city stream around it.
/// </summary>
public class FrontEndDiorama : MonoBehaviour
{
    public float speed = 6f;
    public Vector3 policeCarOffset = new Vector3(-1.5f, 0f, -11f);
    public Vector3 officerOffset = new Vector3(2.2f, 0.31f, -5.5f);
    public Vector3 helicopterOffset = new Vector3(3f, 9f, -24f);

    private Transform _helicopter;
    private Vector3 _helicopterBase;
    private Material _lightMaterial;
    private AudioSource _helicopterAudio;

    private void Start()
    {
        var set = Resources.Load<PoliceSet>("PoliceSet");
        if (set == null) return;

        if (set.policeCar != null) AddPoliceCar(set.policeCar);

        if (set.officerPrefabs != null && set.officerPrefabs.Length > 0 && set.officerPrefabs[0] != null)
        {
            var officer = Instantiate(set.officerPrefabs[0], transform);
            officer.transform.SetLocalPositionAndRotation(officerOffset, Quaternion.identity);
            var animator = officer.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animator.applyRootMotion = false;
                if (animator.runtimeAnimatorController == null) animator.runtimeAnimatorController = set.officerRunController;
            }
        }

        if (set.helicopter != null)
        {
            var heli = Instantiate(set.helicopter, transform);
            foreach (var c in heli.GetComponentsInChildren<Collider>(true)) Destroy(c);
            heli.transform.SetLocalPositionAndRotation(helicopterOffset, Quaternion.Euler(8f, 0f, 0f));
            _helicopter = heli.transform;
            _helicopterBase = helicopterOffset;
            _helicopterAudio = ProceduralSfx.AttachHelicopterSound(heli);
        }
    }

    private void AddPoliceCar(GameObject prefab)
    {
        var car = Instantiate(prefab, transform);
        car.transform.SetLocalPositionAndRotation(policeCarOffset, Quaternion.identity);
        foreach (var c in car.GetComponentsInChildren<Collider>(true)) Destroy(c);
        foreach (var a in car.GetComponentsInChildren<Animator>(true)) Destroy(a);

        var renderers = car.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;
        Bounds b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);
        car.transform.position += Vector3.up * (transform.position.y - b.min.y);

        _lightMaterial = new Material(renderers[0].sharedMaterial) { name = "Backdrop Police Light" };
        _lightMaterial.SetTexture("_BaseMap", null);
        foreach (float side in new[] { -0.4f, 0.4f })
        {
            var light = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(light.GetComponent<Collider>());
            light.GetComponent<Renderer>().sharedMaterial = _lightMaterial;
            light.transform.SetParent(car.transform, true);
            light.transform.position = new Vector3(b.center.x + side, transform.position.y + b.size.y + 0.1f, b.center.z);
            light.transform.localScale = Vector3.one * 0.32f;
            light.AddComponent<PoliceLightBlink>();
        }
    }

    private void Update()
    {
        transform.position += Vector3.forward * (speed * Time.deltaTime);
        if (_helicopterAudio != null) _helicopterAudio.volume = 0.18f * GameSettings.SfxVolume;
        if (_helicopter != null)
            _helicopter.localPosition = _helicopterBase + new Vector3(Mathf.Sin(Time.time * 0.5f) * 3f, Mathf.Sin(Time.time) * 0.5f, 0f);
    }

    private void OnDestroy()
    {
        if (_lightMaterial != null) Destroy(_lightMaterial);
    }
}
