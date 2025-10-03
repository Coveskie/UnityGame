using UnityEngine;
using Game.Inventory;

public class HeldToInventoryBridge : MonoBehaviour
{
    [Tooltip("Leave empty to auto-find Inventory.Instance at runtime.")]
    public Inventory inventory;

    void Awake()
    {
        if (!inventory) inventory = Inventory.Instance;
    }

    /// <summary>Call this when you equip a big item (pass the equipped GameObject).</summary>
    public void Equip(GameObject equippedGO)
    {
        if (!inventory) inventory = Inventory.Instance;
        if (!inventory) return;

        var data = equippedGO ? equippedGO.GetComponentInParent<BigItemData>() : null;
        if (data && data.itemDefinition && data.itemDefinition.size == ItemSize.Big)
        {
            inventory.SetHeld(data.itemDefinition); // UI big slot will show its icon
        }
    }

    /// <summary>Call this when you unequip / drop the big item in hands.</summary>
    public void Unequip()
    {
        if (!inventory) inventory = Inventory.Instance;
        if (!inventory) return;

        inventory.ClearHeld(); // UI big slot clears
    }
}
