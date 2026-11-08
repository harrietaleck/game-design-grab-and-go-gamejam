using UnityEngine;

/// <summary>
/// Real police assets, loaded from Assets/Resources/PoliceSet.asset (built by Grab &amp; Go > Rebuild Police Set).
/// If no officer prefabs are provided, the PoliceDirector builds stand-in officers from AJ's and Claire's rigs
/// (navy uniform tint + cap). Roadblocks, the helicopter and the K9 are skipped when their prefabs are missing.
/// </summary>
[CreateAssetMenu(menuName = "Grab & Go/Police Set", fileName = "PoliceSet")]
public class PoliceSet : ScriptableObject
{
    public GameObject[] officerPrefabs;
    [Tooltip("Tint stand-in officers only (ignored for real police prefabs).")]
    public bool tintPrefabs = false;
    [Tooltip("Humanoid controller (Run, Idle, Kneel, Fire states) applied to officer prefabs that have no controller of their own.")]
    public RuntimeAnimatorController officerRunController;
    [Tooltip("Handgun placed in each officer's right hand.")]
    public GameObject handgun;
    [Tooltip("Patrol car parked across the lanes in roadblocks.")]
    public GameObject policeCar;
    [Tooltip("Helicopter that joins the chase when police pressure is high.")]
    public GameObject helicopter;
    [Tooltip("K9 unit that hunts Claire.")]
    public GameObject policeDog;
    [Tooltip("Controller (Run, Idle states) for the K9.")]
    public RuntimeAnimatorController dogController;
    [HideInInspector] public int generatorVersion;
}
