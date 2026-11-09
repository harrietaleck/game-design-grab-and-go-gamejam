using UnityEngine;

/// <summary>
/// The skateboards and bags for sale. Assets/Resources/ShopSet.asset, built by the editor.
/// Must stay in a file named after the class, or Unity cannot load the asset.
/// </summary>
[CreateAssetMenu(menuName = "Grab & Go/Shop Set", fileName = "ShopSet")]
public class ShopSet : ScriptableObject
{
    public GameObject[] boards;
    public GameObject[] bags;

    private static ShopSet s_active;
    public static ShopSet Active => s_active != null ? s_active : s_active = Resources.Load<ShopSet>("ShopSet");

    public GameObject EquippedBoardPrefab
    {
        get
        {
            if (boards == null || boards.Length == 0) return null;
            return boards[Mathf.Clamp(ShopStore.EquippedBoard, 0, boards.Length - 1)];
        }
    }
}
