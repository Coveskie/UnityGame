using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Inventory
{
    [RequireComponent(typeof(Collider))]
    public class WorldPickup : MonoBehaviour
    {
        [Header("Item Settings")]
        public ItemDefinition item;
        public int amount = 1;

        [Header("Player Detection")]
        public string playerTag = "Player";
        public GameObject promptUI;

        private bool inReach;

        void Awake()
        {
            var col = GetComponent<Collider>();
            if (col && !col.isTrigger)
                Debug.LogWarning($"{name}: WorldPickup on non-trigger collider. Prefer a child trigger.");
        }

        void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag(playerTag)) return;
            inReach = true;
            if (promptUI) promptUI.SetActive(true);
        }

        void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag(playerTag)) return;
            inReach = false;
            if (promptUI) promptUI.SetActive(false);
        }

        void Update()
        {
            if (!inReach || Keyboard.current == null) return;

            if (Keyboard.current.eKey.wasPressedThisFrame)
            {
                if (!Inventory.Instance) return;

                if (item && item.size == ItemSize.Big)
                {
                    // Equip big item into big slot
                    Inventory.Instance.SetHeld(item);
                    if (promptUI) promptUI.SetActive(false);
                    Destroy(transform.root.gameObject);
                }
                else
                {
                    // Try to add small item to slots 1..4
                    if (Inventory.Instance.TryAdd(item, amount))
                    {
                        if (promptUI) promptUI.SetActive(false);
                        Destroy(transform.root.gameObject);
                    }
                    else
                    {
                        Debug.Log("Inventory full or cannot add item.");
                    }
                }
            }
        }
    }
}
