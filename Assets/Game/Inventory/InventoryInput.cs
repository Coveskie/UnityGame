using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class InventoryInput : MonoBehaviour
{
    [Header("Input System")]
    [Tooltip("Reference to an InputAction that toggles the inventory (performed on press).")]
    public InputActionReference toggleInventoryAction;

    [Header("Target")]
    [Tooltip("If null, will auto-find the first InventoryUI in the scene.")]
    public InventoryUI targetInventory;

    [Header("Diagnostics")]
    public bool debugLogs = true;

    void Awake()
    {
        if (!targetInventory)
            targetInventory = FindAnyObjectByType<InventoryUI>();

        if (!toggleInventoryAction)
        {
            Debug.LogError("[InventoryInput] No toggleInventoryAction assigned. Bind an InputActionReference (e.g. to <Keyboard>/tab).", this);
        }
    }

    void OnEnable()
    {
        if (toggleInventoryAction)
        {
            toggleInventoryAction.action.performed += OnTogglePerformed;
            toggleInventoryAction.action.Enable();
        }
    }

    void OnDisable()
    {
        if (toggleInventoryAction)
        {
            toggleInventoryAction.action.performed -= OnTogglePerformed;
            toggleInventoryAction.action.Disable();
        }
    }

    void OnTogglePerformed(InputAction.CallbackContext ctx)
    {
        if (!targetInventory)
        {
            if (debugLogs) Debug.LogWarning("[InventoryInput] No InventoryUI found/assigned.", this);
            return;
        }

        targetInventory.Toggle();
        if (debugLogs) Debug.Log($"[InventoryInput] Toggle -> {(targetInventory.IsOpen ? "Open" : "Close")}", this);
    }
}
