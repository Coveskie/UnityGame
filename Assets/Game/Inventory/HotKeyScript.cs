using UnityEngine;
using UnityEngine.InputSystem;   // new Input System
using UnityEngine.EventSystems;  // to detect typing in UI
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(InventoryUI))]
public class InventoryTabHotkey : MonoBehaviour
{
    [Header("Key")]
    public Key toggleKey = Key.Tab;

    [Header("Behavior")]
    public bool ignoreWhenTyping = true;  // don't toggle if an input field is focused

    InventoryUI ui;

    void Awake()
    {
        ui = GetComponent<InventoryUI>();
        if (!ui) Debug.LogError("[InventoryTabHotkey] InventoryUI not found on the same GameObject.");
    }

    bool IsTypingIntoUI()
    {
        if (!ignoreWhenTyping) return false;
        if (!EventSystem.current) return false;

        var go = EventSystem.current.currentSelectedGameObject;
        if (!go) return false;

        // Block toggle if the user is typing in any input field
        return go.GetComponent<TMP_InputField>() != null || go.GetComponent<InputField>() != null;
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;                // new Input System not active?
        if (IsTypingIntoUI()) return;          // don't toggle while typing in fields

        if (kb[toggleKey].wasPressedThisFrame)
        {
            ui.Toggle();
            // Debug.Log("[InventoryTabHotkey] Tab pressed → toggled inventory.");
        }
    }
}
