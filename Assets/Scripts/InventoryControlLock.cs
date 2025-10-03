// Assets/Game/Inventory/UI/InventoryControlLocker.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class InventoryControlLocker : MonoBehaviour
{
    [Header("References")]
    public InventoryUI inventoryUI;          // drag your InventoryUI here
    public PlayerInput playerInput;          // new Input System's PlayerInput

    [Header("Action Maps (if using PlayerInput)")]
    public string gameplayActionMap = "Player";
    public string uiActionMap = "UI";

    [Header("If NOT using action maps, disable these while inventory is open")]
    public List<Behaviour> disableWhileOpen; // e.g., PlayerMovement, Look, Interactor, etc.

    [Header("Cursor")]
    public bool manageCursor = true;

    [Header("Optional Pause")]
    public bool pauseWhileOpen = false;

    [Header("Safety")]
    [Tooltip("Waits one render frame before (en|dis)abling behaviours to avoid same-frame VFX destruction.")]
    public bool deferDisableOneFrame = true;

    float _prevTimeScale = 1f;
    Coroutine _toggleRoutine;

    void Awake()
    {
        if (inventoryUI) inventoryUI.OnInventoryToggled += HandleToggle;
    }

    void OnDestroy()
    {
        if (inventoryUI) inventoryUI.OnInventoryToggled -= HandleToggle;
    }

    void HandleToggle(bool open)
    {
        if (_toggleRoutine != null) StopCoroutine(_toggleRoutine);
        _toggleRoutine = StartCoroutine(ToggleRoutine(open));
    }

    IEnumerator ToggleRoutine(bool open)
    {
        // Switch input maps immediately so controls feel snappy
        if (playerInput != null)
        {
            var targetMap = open ? uiActionMap : gameplayActionMap;
            if (!string.IsNullOrEmpty(targetMap) &&
                playerInput.actions.FindActionMap(targetMap, true) != null)
            {
                playerInput.SwitchCurrentActionMap(targetMap);
            }
        }

        // Cursor now, not deferred
        if (manageCursor)
        {
            if (open) { Cursor.visible = true; Cursor.lockState = CursorLockMode.None; }
            else { Cursor.visible = false; Cursor.lockState = CursorLockMode.Locked; }
        }

        // If we’re about to disable gameplay behaviours, defer one frame so any
        // particle jobs finish before potential OnDisable() teardown in those scripts.
        if (deferDisableOneFrame)
            yield return new WaitForEndOfFrame();

        // Fallback: hard-disable gameplay scripts when not using action maps
        if (playerInput == null && disableWhileOpen != null)
        {
            foreach (var b in disableWhileOpen)
                if (b) b.enabled = !open;
        }

        // Pause last (after the frame) so WaitForEndOfFrame still runs
        if (pauseWhileOpen)
        {
            if (open) { _prevTimeScale = Time.timeScale; Time.timeScale = 0f; }
            else { Time.timeScale = _prevTimeScale; }
        }
    }
}
