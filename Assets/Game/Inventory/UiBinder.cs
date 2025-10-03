// Assets/Scripts/Inventory/InventoryUIBinder.cs
using Game.Inventory;
using UnityEngine;
using UnityEngine.UI;

public class InventoryUIBinder : MonoBehaviour
{
    [Header("Big Slot")]
    public Image bigSlotIcon;
    public Text bigSlotCountText;

    [Header("Small Slots (order: 1..4)")]
    public Image[] smallSlotIcons = new Image[4];
    public Text[] smallSlotCountTexts = new Text[4];

    [Header("Colors")]
    public Color emptyTint = new Color32(128, 128, 128, 255); // grey tile
    public Color filledTint = Color.white;

    void OnEnable()
    {
        if (Inventory.Instance != null)
        {
            Inventory.Instance.OnChanged += Refresh;
            Refresh();
        }
    }

    void OnDisable()
    {
        if (Inventory.Instance != null)
            Inventory.Instance.OnChanged -= Refresh;
    }

    public void Refresh()
    {
        var inv = Inventory.Instance;
        if (!inv) return;

        // Slot 0 = big
        ApplyToUI(inv.GetSlot(0), bigSlotIcon, bigSlotCountText);

        // Slots 1..4 = smalls
        for (int i = 0; i < 4; i++)
        {
            ApplyToUI(inv.GetSlot(i + 1), smallSlotIcons[i], smallSlotCountTexts[i]);
        }
    }

    void ApplyToUI(Inventory.Slot slot, Image iconImg, Text countText)
    {
        if (!iconImg || !countText) return;

        if (slot == null || slot.IsEmpty || slot.item.icon == null)
        {
            iconImg.sprite = null;
            iconImg.color = emptyTint;     // keep the tile grey when empty
            countText.text = "";
        }
        else
        {
            iconImg.sprite = slot.item.icon;
            iconImg.color = filledTint;    // show icon normally when filled
            countText.text = slot.item.stackable && slot.count > 1 ? slot.count.ToString() : "";
        }
    }
}
