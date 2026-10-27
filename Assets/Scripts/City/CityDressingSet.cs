using UnityEngine;

/// <summary>
/// Which Cartoon City (ithappy) prefabs dress the street. Lives at Assets/Resources/CityDressingSet.asset
/// and is generated automatically by the editor (Grab &amp; Go > Rebuild City Dressing Set). Edit freely in the Inspector.
/// </summary>
[CreateAssetMenu(menuName = "Grab & Go/City Dressing Set", fileName = "CityDressingSet")]
public class CityDressingSet : ScriptableObject
{
    [Tooltip("Bumped by the editor generator when new fields are added (forces a rebuild).")]
    public int generatorVersion;

    [Header("Buildings (street front and skyline)")]
    public GameObject[] buildings;
    [Tooltip("Occasional variants for extra variety (e.g. lit-window versions).")]
    public GameObject[] accentBuildings;
    [Range(0f, 1f)] public float accentChance = 0.2f;

    [Header("Building colour variety")]
    [Tooltip("Random tint per building, applied only to materials whose name contains one of tintMaterialKeywords.")]
    public Color[] buildingTints =
    {
        new Color(1f, 1f, 1f), new Color(1f, 0.8f, 0.62f), new Color(0.62f, 0.8f, 1f),
        new Color(1f, 0.68f, 0.74f), new Color(0.64f, 0.95f, 0.76f), new Color(0.8f, 0.7f, 1f),
        new Color(1f, 0.9f, 0.52f), new Color(0.74f, 0.79f, 0.9f), new Color(1f, 0.6f, 0.5f)
    };
    public string[] tintMaterialKeywords = { "Color" };

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
