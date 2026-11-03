using UnityEngine;

/// <summary>
/// Puts a backpack from the "LowPoly Fantasy Bags" pack on Claire and AJ at runtime.
/// Each bag is fitted to a target height and parented to the character's upper-spine bone so it follows
/// the running animation. Visual only: colliders are disabled, nothing in gameplay changes.
/// </summary>
public class CharacterBackpacks : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        GameStartFlow.RegisterBootstrap(flow =>
        {
            if (flow.GetComponent<CharacterBackpacks>() == null)
                flow.gameObject.AddComponent<CharacterBackpacks>();
        });
    }

    public const string BackpackObjectName = "Backpack (runtime)";
    public string gearSetResource = "CharacterGearSet";

    private void Awake()
    {
        // Awake (not Start) so the bags exist before anything copies the characters.
        var set = Resources.Load<CharacterGearSet>(gearSetResource);
        if (set == null)
        {
            Debug.Log("[CharacterBackpacks] No CharacterGearSet in Resources; characters keep no bags.");
            return;
        }
        var claire = FindFirstObjectByType<PlayerLaneMovement>();
        var aj = FindFirstObjectByType<AIThiefLaneDodge>();
        if (claire != null) Attach(claire.transform, set.claire, set.boneNames);
        if (aj != null) Attach(aj.transform, set.aj, set.boneNames);
    }

    private static void Attach(Transform character, CharacterGearSet.Backpack gear, string[] boneNames)
    {
        if (gear == null || gear.prefab == null) return;
        foreach (Transform t in character.GetComponentsInChildren<Transform>(true))
            if (t.name == BackpackObjectName) return; // already wearing one

        Transform bone = null;
        foreach (string n in boneNames)
        {
            bone = FindBone(character, n);
            if (bone != null) break;
        }
        if (bone == null)
        {
            Debug.LogWarning($"[CharacterBackpacks] No spine bone found on {character.name}; bag not attached.");
            return;
        }

        var pack = Instantiate(gear.prefab);
        pack.name = BackpackObjectName;
        foreach (var c in pack.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        foreach (var r in pack.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

        // Face the same way as the character (or the opposite way if the model's straps point the other way).
        Vector3 forward = character.forward; forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
        forward.Normalize();
        // Keep the prefab's own base rotation (e.g. the -90 X fix for Blender exports), then turn it to face forward.
        pack.transform.rotation = Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(0f, gear.flip ? 180f : 0f, 0f)
                                  * gear.prefab.transform.rotation;

        // Fit to the target height.
        var b = Bounds(pack);
        if (b.size.y > 0.0001f) pack.transform.localScale *= gear.height / b.size.y;
        b = Bounds(pack);

        // Sit the bag's front against the back, centred on the spine bone.
        float halfDepth = Mathf.Abs(Vector3.Dot(b.extents, new Vector3(Mathf.Abs(forward.x), 0f, Mathf.Abs(forward.z))));
        Vector3 target = bone.position - forward * (gear.backOffset + halfDepth) + Vector3.up * gear.heightOffset;
        pack.transform.position += target - b.center;

        pack.transform.SetParent(bone, worldPositionStays: true);
    }

    private static Transform FindBone(Transform root, string boneName)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == boneName || t.name.EndsWith(":" + boneName)) return t;
        return null;
    }

    private static Bounds Bounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one * 0.3f);
        var b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }
}
