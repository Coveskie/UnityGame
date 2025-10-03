using System;
using UnityEngine;

namespace Game.Inventory
{
    [Serializable]
    public struct ItemStack
    {
        public ItemDefinition item;
        public int count;

        public bool IsEmpty => item == null || count <= 0;
        public int SpaceLeft => item == null ? 0 : Mathf.Max(0, item.maxStack - count);

        public int Add(int amount)
        {
            if (item == null) return amount;
            int toAdd = Mathf.Min(amount, SpaceLeft);
            count += toAdd;
            return amount - toAdd; // remainder
        }

        public int Remove(int amount)
        {
            if (IsEmpty) return amount;
            int toRemove = Mathf.Min(amount, count);
            count -= toRemove;
            return amount - toRemove;
        }
    }
}
