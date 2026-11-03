using UnityEngine;

/// <summary>
/// Which bag each character wears and how it sits. Lives at Assets/Resources/CharacterGearSet.asset
/// (generated automatically from "LowPoly Fantasy Bags"; edit freely in the Inspector).
/// </summary>
[CreateAssetMenu(menuName = "Grab & Go/Character Gear Set", fileName = "CharacterGearSet")]
public class CharacterGearSet : ScriptableObject
{
    [Tooltip("Bumped by the editor generator when its defaults change (forces a rebuild).")]
    public int generatorVersion;

    [System.Serializable]
    public class Backpack
    {
        public GameObject prefab;
        [Tooltip("Height of the bag in metres once fitted.")]
        public float height = 0.5f;
        [Tooltip("Extra distance behind the spine bone (metres).")]
        public float backOffset = 0.12f;
        [Tooltip("Raise (+) or lower (-) the bag along the spine (metres).")]
        public float heightOffset = 0f;
        [Tooltip("Turn the bag around if its straps face away from the body.")]
        public bool flip = false;
    }

    public Backpack claire = new Backpack { height = 0.45f };
    public Backpack aj = new Backpack { height = 0.55f };
    [Tooltip("Spine bones to try, in order (Mixamo names without the 'mixamorig:' prefix).")]
    public string[] boneNames = { "Spine2", "Spine1", "Spine" };
}
