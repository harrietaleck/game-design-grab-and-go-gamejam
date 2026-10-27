using UnityEngine;

/// <summary>
/// Which Cartoon City (ithappy) prefabs dress the street. Lives at Assets/Resources/CityDressingSet.asset
/// and is generated automatically by the editor (Grab &amp; Go > Rebuild City Dressing Set). Edit freely in the Inspector.
/// </summary>
[CreateAssetMenu(menuName = "Grab & Go/City Dressing Set", fileName = "CityDressingSet")]
public class CityDressingSet : ScriptableObject
{
    [Header("Buildings (street front and skyline)")]
    public GameObject[] buildings;

    [Header("Sidewalk")]
    public GameObject[] sidewalkTiles;

    [Header("Street furniture")]
    public GameObject[] streetLights;
    public GameObject[] trees;
    public GameObject[] bushes;
    public GameObject[] trashCans;
    public GameObject[] busStops;
    public GameObject[] billboards;
    public GameObject[] trafficLights;
    public GameObject[] fountains;

    [Header("Atmosphere")]
    public Material skybox;
    public bool useFog = true;
    public Color fogColor = new Color(0.663f, 0.871f, 0.898f, 1f); // same as the pack's demo scene
    public float fogDensity = 0.006f;
}
