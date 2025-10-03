using UnityEngine;
using UnityEngine.InputSystem;  // ← New Input System

[RequireComponent(typeof(Collider))]
public class Doors : MonoBehaviour
{
    [Header("Scene References")]
    public Animator door;                // Animator with bools: Open, Closed
    public GameObject openText;          // "Press E to open" UI (worldspace or screen)

    [Header("Audio")]
    public AudioSource doorSound;

    [Header("Input System")]
    [Tooltip("Assign your Interact InputAction here (e.g., from your Actions asset).\n" +
             "Type: Button. Expected binding: Keyboard E / Gamepad South, etc.")]
    public InputActionReference interactAction; // e.g. Player/Interact

    [Header("Behavior")]
    [Tooltip("Automatically close when player leaves trigger.")]
    public bool autoCloseOnExit = true;

    private bool _inReach;
    private bool _isOpen;

    // Cache action to avoid .action chaining GC
    private InputAction _interact;

    private void Reset()
    {
        // Ensure trigger collider
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = true;
    }

    private void Awake()
    {
        if (door == null)
            Debug.LogWarning($"{name}: Door Animator not set.");

        if (openText != null)
            openText.SetActive(false);

        // Prepare InputAction
        if (interactAction != null)
            _interact = interactAction.action;
    }

    private void OnEnable()
    {
        if (_interact != null)
        {
            _interact.Enable();
            _interact.performed += OnInteract;
        }
        else
        {
            Debug.LogWarning($"{name}: No Interact action assigned. Assign an InputActionReference.");
        }
    }

    private void OnDisable()
    {
        if (_interact != null)
        {
            _interact.performed -= OnInteract;
            _interact.Disable();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Use CompareTag for speed/safety
        if (other.CompareTag("Reach"))
        {
            _inReach = true;
            if (openText != null) openText.SetActive(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Reach"))
        {
            _inReach = false;
            if (openText != null) openText.SetActive(false);

            if (autoCloseOnExit && _isOpen)
                DoorCloses();
        }
    }

    private void OnInteract(InputAction.CallbackContext ctx)
    {
        if (!_inReach) return;

        // Toggle door on press (performed)
        if (!_isOpen)
            DoorOpens();
        else
            DoorCloses();
    }

    private void DoorOpens()
    {
        _isOpen = true;
        if (door != null)
        {
            door.SetBool("Open", true);
            door.SetBool("Closed", false);
        }
        if (doorSound != null) doorSound.Play();
        // Optional: hide prompt once opened
        // if (openText != null) openText.SetActive(false);
        Debug.Log("Door: Open");
    }

    private void DoorCloses()
    {
        _isOpen = false;
        if (door != null)
        {
            door.SetBool("Open", false);
            door.SetBool("Closed", true);
        }
        Debug.Log("Door: Close");
    }
}




