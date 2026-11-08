using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Spend points on skateboards, bags and permanent upgrades. Boards and bags show as a spinning 3D preview.</summary>
public class ShopScreen : MonoBehaviour
{
    private enum Tab { Boards, Bags, Upgrades }

    private class Item
    {
        public string id, name, description;
        public int price;
        public GameObject model;
        public int equipIndex = int.MinValue;    // boards and bags only
    }

    private static readonly (string id, string name, string description, int price)[] Upgrades =
    {
        (ShopStore.DoubleJump, "DOUBLE JUMP SHOES", "Press jump again in mid-air. High enough to clear parked cars.", 180),
        (ShopStore.Sprint, "SPRINT SHOES", "Claire and AJ run 10% faster, so police struggle to catch up.", 150),
        (ShopStore.BigBag, "BIGGER BAG", "A larger bag. Crashes spill 15% of your loot instead of 25%.", 160),
        (ShopStore.ExtraCharge, "EXTRA DISTRACTION", "One more Distraction Pulse every run.", 120),
        (ShopStore.LongRide, "LONG RIDE", "Skateboard rides last 14 seconds instead of 9.", 130),
    };

    private Tab _tab = Tab.Boards;
    private RectTransform _grid;
    private TextMeshProUGUI _points, _detailName, _detailBody;
    private readonly List<GameObject> _spawned = new();
    private Transform _preview;

    public void Build(UnityEngine.Events.UnityAction onBack)
    {
        var card = RuntimeUI.CreateWindow(transform, "ShopCard", null, 0.82f);
        RuntimeUI.Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(1220f, 1060f), new Vector2(-330f, 0f));
        FancyText.Create(card.transform, "Title", "SHOP", 64f, UITheme.Fancy.Gold, new Vector2(0.5f, 1f), new Vector2(600f, 100f), new Vector2(-260f, -30f));

        _points = RuntimeUI.CreateText(card.transform, "Points", "", 34f, TextAlignmentOptions.Right, RuntimeUI.Gold, FontStyles.Bold);
        RuntimeUI.Place(_points.rectTransform, new Vector2(1f, 1f), new Vector2(500f, 70f), new Vector2(-50f, -44f));

        AddTab(card.transform, "BOARDS", Tab.Boards, -380f);
        AddTab(card.transform, "BAGS", Tab.Bags, 0f);
        AddTab(card.transform, "UPGRADES", Tab.Upgrades, 380f);

        _grid = RuntimeUI.CreateRect(card.transform, "Items");
        RuntimeUI.Place(_grid, new Vector2(0.5f, 1f), new Vector2(1120f, 700f), new Vector2(0f, -230f));

        var back = RuntimeUI.CreateButton(card.transform, "BackButton", "BACK", onBack);
        RuntimeUI.Place((RectTransform)back.transform, new Vector2(0.5f, 0f), new Vector2(360f, 84f), new Vector2(0f, 42f));

        var detail = RuntimeUI.CreateHudPanel(transform, "ShopDetail");
        RuntimeUI.Place(detail.rectTransform, new Vector2(0.5f, 0f), new Vector2(620f, 280f), new Vector2(620f, 90f));
        _detailName = RuntimeUI.CreateText(detail.transform, "Name", "", 34f, TextAlignmentOptions.Center, RuntimeUI.Gold, FontStyles.Bold);
        RuntimeUI.Place(_detailName.rectTransform, new Vector2(0.5f, 1f), new Vector2(570f, 70f), new Vector2(0f, -22f));
        RuntimeUI.FitOnOneLine(_detailName);
        _detailBody = RuntimeUI.CreateText(detail.transform, "Body", "", 26f, TextAlignmentOptions.Top, Color.white);
        RuntimeUI.Place(_detailBody.rectTransform, new Vector2(0.5f, 1f), new Vector2(570f, 170f), new Vector2(0f, -96f));
        RuntimeUI.FitInBox(_detailBody);
    }

    private void AddTab(Transform parent, string label, Tab tab, float x)
    {
        var button = RuntimeUI.CreateButton(parent, label + " Tab", label, () => Show(tab));
        RuntimeUI.Place((RectTransform)button.transform, new Vector2(0.5f, 1f), new Vector2(340f, 78f), new Vector2(x, -135f));
    }

    private void OnEnable()
    {
        if (_grid != null) Show(_tab);
    }

    private void OnDisable() => ClearPreview();

    private void Update()
    {
        if (_preview != null) _preview.Rotate(Vector3.up, 55f * Time.unscaledDeltaTime, Space.World);
    }

    private void Show(Tab tab)
    {
        _tab = tab;
        Refresh();
        var items = ItemsFor(tab);
        Item first = items.Count > 0 ? items[0] : null;
        foreach (var item in items) if (IsEquipped(item)) first = item;
        if (first != null) ShowDetail(first, null);
    }

    private List<Item> ItemsFor(Tab tab)
    {
        var items = new List<Item>();
        var set = ShopSet.Active;
        if (tab == Tab.Upgrades)
        {
            foreach (var u in Upgrades)
                items.Add(new Item { id = u.id, name = u.name, description = u.description, price = u.price });
        }
        else if (tab == Tab.Boards && set != null && set.boards != null)
        {
            for (int i = 0; i < set.boards.Length; i++)
                items.Add(new Item { id = "board" + i, name = "BOARD " + (i + 1), description = "A skateboard for the road pick-ups. Looks only.", price = i == 0 ? 0 : 40 + i * 20, model = set.boards[i], equipIndex = i });
        }
        else if (tab == Tab.Bags && set != null && set.bags != null)
        {
            items.Add(new Item { id = "bag-default", name = "ORIGINAL BAG", description = "Claire's own bag.", price = 0, equipIndex = -1 });
            for (int i = 0; i < set.bags.Length; i++)
                items.Add(new Item { id = "bag" + i, name = "BAG " + (i + 1), description = "A new look for Claire's bag. Looks only.", price = 30 + i * 15, model = set.bags[i], equipIndex = i });
        }
        return items;
    }

    private static bool IsOwned(Item item) => item.price == 0 || ShopStore.Owns(item.id);

    private bool IsEquipped(Item item)
    {
        if (item.equipIndex == int.MinValue) return false;
        return _tab == Tab.Boards ? ShopStore.EquippedBoard == item.equipIndex : ShopStore.EquippedBag == item.equipIndex;
    }

    private void Refresh()
    {
        _points.text = $"{ShopStore.Points} POINTS";
        for (int i = _grid.childCount - 1; i >= 0; i--) Destroy(_grid.GetChild(i).gameObject);
        _spawned.Clear();

        var items = ItemsFor(_tab);
        const float width = 545f, height = 78f, stepY = 92f;
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            float x = (i % 2 == 0 ? -1f : 1f) * (width * 0.5f + 10f);
            float y = -(i / 2) * stepY;
            var button = RuntimeUI.CreateButton(_grid, item.id, LabelFor(item), null);
            button.onClick.AddListener(() => OnItemClicked(item, button));
            RuntimeUI.Place((RectTransform)button.transform, new Vector2(0.5f, 1f), new Vector2(width, height), new Vector2(x, y));
            RuntimeUI.FitOnOneLine(button.GetComponentInChildren<TextMeshProUGUI>());
        }
    }

    private string LabelFor(Item item)
    {
        if (IsEquipped(item)) return $"{item.name}  -  EQUIPPED";
        if (IsOwned(item)) return item.equipIndex == int.MinValue ? $"{item.name}  -  OWNED" : item.name;
        return $"{item.name}  -  {item.price}";
    }

    private void OnItemClicked(Item item, Button button)
    {
        if (!IsOwned(item) && !ShopStore.Buy(item.id, item.price))
        {
            ShowDetail(item, $"You need {item.price - ShopStore.Points} more points.");
            return;
        }

        if (item.equipIndex != int.MinValue)
        {
            if (_tab == Tab.Boards) ShopStore.EquippedBoard = item.equipIndex;
            else ShopStore.EquippedBag = item.equipIndex;
        }
        Refresh();
        ShowDetail(item, null);
    }

    private void ShowDetail(Item item, string message)
    {
        _detailName.text = item.name;
        _detailBody.text = message ?? item.description;
        _detailBody.color = message != null ? RuntimeUI.Danger : Color.white;
        ClearPreview();
        if (item.model == null || Camera.main == null) return;

        var go = Instantiate(item.model, Camera.main.transform);
        foreach (var c in go.GetComponentsInChildren<Collider>(true)) Destroy(c);
        go.transform.localPosition = new Vector3(1.75f, 0.35f, 4.2f);

        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            float largest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            if (largest > 0.0001f) go.transform.localScale *= 1.5f / largest;
        }
        _preview = go.transform;
    }

    private void ClearPreview()
    {
        if (_preview != null) Destroy(_preview.gameObject);
        _preview = null;
    }
}
