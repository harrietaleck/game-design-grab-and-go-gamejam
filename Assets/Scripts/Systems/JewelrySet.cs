using System;
using UnityEngine;

/// <summary>High-value loot AJ can drop, loaded from Assets/Resources/JewelrySet.asset.</summary>
[CreateAssetMenu(menuName = "Grab & Go/Jewelry Set", fileName = "JewelrySet")]
public class JewelrySet : ScriptableObject
{
    [Serializable]
    public class Item
    {
        public GameObject prefab;
        [Min(1)] public int weight = 1;
        [Min(10)] public int value = 10;
    }

    public Item[] items;
    [Tooltip("Largest dimension of a jewelry pickup, in world units.")]
    public float pickupSize = 0.7f;
}

/// <summary>Adds the jewelry to AJ's drop table at the start of the gameplay scene.</summary>
public class JewelryLoot : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        GameStartFlow.RegisterBootstrap(flow =>
        {
            if (flow.GetComponent<JewelryLoot>() == null)
                flow.gameObject.AddComponent<JewelryLoot>();
        });
    }

    private void Start()
    {
        var set = Resources.Load<JewelrySet>("JewelrySet");
        var dropper = FindFirstObjectByType<ThiefCollectibleDropper>();
        if (set == null || set.items == null || dropper == null || dropper.dropTable == null) return;

        // Jewelry uses the most valuable existing drop's pickup sound.
        ThiefCollectibleDropper.DropEntry richest = null;
        foreach (var e in dropper.dropTable)
            if (e != null && (richest == null || e.fallbackValue > richest.fallbackValue)) richest = e;

        var table = new System.Collections.Generic.List<ThiefCollectibleDropper.DropEntry>(dropper.dropTable);
        foreach (var item in set.items)
        {
            if (item.prefab == null) continue;
            table.Add(new ThiefCollectibleDropper.DropEntry
            {
                prefab = item.prefab,
                weight = item.weight,
                fallbackValue = item.value,
                rotateOnSpawn = true,
                spinAroundUp = true,
                spinSpeed = 140f,
                targetSize = set.pickupSize,
                pickupAudio = richest != null ? richest.pickupAudio : null,
                pickupAudioVolume = 1f
            });
        }
        dropper.dropTable = table.ToArray();
    }
}
