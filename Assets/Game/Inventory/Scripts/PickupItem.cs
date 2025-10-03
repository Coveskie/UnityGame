using UnityEngine;
using UnityEngine.InputSystem;
using Game.Inventory;   // <-- add this so it finds Inventory + ItemDefinition

[RequireComponent(typeof(Collider))]
public class PickupItem : MonoBehaviour
{
    [Header("Item Settings")]
    public ItemDefinition item;
    public int amount = 1;

    [Header("Player Detection")]
    public string playerTag = "Player";
    public GameObject promptUI; // optional "Press E to pick up"

    private bool inReach;

    private void Reset()
    {
        // Ensure collider is trigger
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            inReach = true;
            if (promptUI) promptUI.SetActive(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            inReach = false;
            if (promptUI) promptUI.SetActive(false);
        }
    }

    private void Update()
    {
        if (!inReach || Keyboard.current == null) return;

        // Press E to pick up
        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            if (Inventory.Instance != null && Inventory.Instance.TryAdd(item, amount))
            {
                if (promptUI) promptUI.SetActive(false);
                Destroy(gameObject); // remove from world
            }
            else
            {
                Debug.Log("Inventory full or item could not be added.");
            }
        }
    }
}
