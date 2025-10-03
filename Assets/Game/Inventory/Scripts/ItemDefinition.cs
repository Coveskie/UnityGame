using UnityEngine;

namespace Game.Inventory
{
    public enum ItemSize { Small, Big }

    [CreateAssetMenu(menuName = "Inventory/Item Definition")]
    public class ItemDefinition : ScriptableObject
    {
        public string id = "item_id";
        public string displayName = "Item";
        public Sprite icon;
        public bool stackable = true;
        public int maxStack = 99;

        [Header("Inventory Rules")]
        public ItemSize size = ItemSize.Small;   // Big for Lanterns, Small for keys etc.
    }
}
