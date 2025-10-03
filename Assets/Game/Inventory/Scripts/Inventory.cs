using UnityEngine;
using System;

namespace Game.Inventory
{
    public class Inventory : MonoBehaviour
    {
        public static Inventory Instance { get; private set; }

        [Serializable]
        public class Slot
        {
            public ItemDefinition item;
            public int count;
            public bool IsEmpty => item == null || count <= 0;
        }

        // Slot 0 = big (held), Slots 1..4 = small
        public Slot[] slots = new Slot[5];
        public ItemDefinition heldItem;   // currently held big item (Lantern etc.)

        public event Action OnChanged;

        private void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            if (slots == null || slots.Length != 5) slots = new Slot[5];
            for (int i = 0; i < slots.Length; i++)
                if (slots[i] == null) slots[i] = new Slot();
        }

        public void SetHeld(ItemDefinition def)
        {
            heldItem = def;
            OnChanged?.Invoke();
        }

        public void ClearHeld()
        {
            heldItem = null;
            OnChanged?.Invoke();
        }

        // Adds only small items (keys etc.)
        public bool TryAdd(ItemDefinition def, int amount = 1)
        {
            if (!def || amount <= 0) return false;
            if (def.size == ItemSize.Big) return false; // Big items not stored in slots

            int start = 1, end = 4;

            // Stack into existing
            if (def.stackable)
            {
                for (int i = start; i <= end && amount > 0; i++)
                {
                    var s = slots[i];
                    if (!s.IsEmpty && s.item == def && s.count < def.maxStack)
                    {
                        int space = def.maxStack - s.count;
                        int add = Mathf.Min(space, amount);
                        s.count += add;
                        amount -= add;
                    }
                }
            }

            // Fill empty
            for (int i = start; i <= end && amount > 0; i++)
            {
                var s = slots[i];
                if (s.IsEmpty)
                {
                    s.item = def;
                    s.count = def.stackable ? Mathf.Min(def.maxStack, amount) : 1;
                    amount -= s.count;
                }
            }

            OnChanged?.Invoke();
            return amount <= 0;
        }

        public void RemoveAt(int index, int amount = 1)
        {
            if (index < 1 || index > 4) return; // protect big slot
            var s = slots[index];
            if (s.IsEmpty) return;

            s.count -= amount;
            if (s.count <= 0) { s.item = null; s.count = 0; }
            OnChanged?.Invoke();
        }

        public Slot GetSlot(int i)
        {
            if (i < 0 || i >= slots.Length) return null;
            return slots[i];
        }
    }
}
