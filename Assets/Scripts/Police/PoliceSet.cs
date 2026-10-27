using UnityEngine;

/// <summary>
/// Optional: real police character prefabs. Create via Assets > Create > Grab &amp; Go > Police Set, save it as
/// Assets/Resources/PoliceSet.asset and add prefabs (each with an Animator). If none is provided, the
/// PoliceDirector builds stand-in officers from AJ's and Claire's rigs (navy uniform tint + cap).
/// </summary>
[CreateAssetMenu(menuName = "Grab & Go/Police Set", fileName = "PoliceSet")]
public class PoliceSet : ScriptableObject
{
    public GameObject[] officerPrefabs;
    [Tooltip("Tint stand-in officers only (ignored for real police prefabs).")]
    public bool tintPrefabs = false;
}
