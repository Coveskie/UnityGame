using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem; // New Input System

[RequireComponent(typeof(Collider))]
public class AdvancedDoors : MonoBehaviour
{
    [Header("Scene References")]
    public Animator door;
    public GameObject lockOB;
    public GameObject keyOB;
    public GameObject openText;
    public GameObject closeText;
    public GameObject lockedText;

    [Header("Audio")]
    public AudioSource openSound;
    public AudioSource closeSound;
    public AudioSource lockedSound;
    public AudioSource unlockedSound;

    [Header("Input System")]
    [Tooltip("Drag your Interact action (Button) from your InputActionAsset here.")]
    public InputActionReference interactAction; // same Interact you use for movement

    private InputAction _interact;

    // State
    private bool inReach;
    private bool doorIsOpen;
    private bool doorIsClosed = true;
    public bool locked;
    public bool unlocked;

    private void Reset()
    {
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = true;
    }

    private void Awake()
    {
        if (openText) openText.SetActive(false);
        if (closeText) closeText.SetActive(false);
        if (lockedText) lockedText.SetActive(false);

        if (interactAction != null)
            _interact = interactAction.action;
        else
            Debug.LogWarning($"{name}: No Interact InputActionReference assigned.");
    }

    private void OnEnable()
    {
        if (_interact != null)
        {
            _interact.Enable();
            _interact.performed += OnInteractPerformed;
        }
    }

    private void OnDisable()
    {
        if (_interact != null)
        {
            _interact.performed -= OnInteractPerformed;
            _interact.Disable();
        }
    }

    private void Start()
    {
        inReach = false;
        doorIsClosed = true;
        doorIsOpen = false;

        // Ensure Animator reflects initial state
        if (door)
        {
            door.SetBool("Open", false);
            door.SetBool("Closed", true);
        }
    }

    private void Update()
    {
        // Mirror old logic: lock/unlock follows lockOB active state
        if (lockOB && lockOB.activeInHierarchy)
        {
            locked = true;
            unlocked = false;
        }
        else
        {
            unlocked = true;
            locked = false;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Reach")) return;

        inReach = true;

        // Match original behavior: show open/close prompts based on door state.
        if (doorIsClosed)
        {
            if (openText) openText.SetActive(true);
        }
        else if (doorIsOpen)
        {
            if (closeText) closeText.SetActive(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Reach")) return;

        inReach = false;

        if (openText) openText.SetActive(false);
        if (closeText) closeText.SetActive(false);
        if (lockedText) lockedText.SetActive(false);
    }

    private void OnInteractPerformed(InputAction.CallbackContext ctx)
    {
        if (!inReach) return;

        // If player has a key visible and presses Interact, unlock via coroutine (same as old script)
        if (keyOB && keyOB.activeInHierarchy)
        {
            if (unlockedSound) unlockedSound.Play();
            unlocked = true;
            locked = false;
            keyOB.SetActive(false);
            StartCoroutine(unlockDoor());
            return;
        }

        // If locked, show locked feedback
        if (locked)
        {
            if (openText) openText.SetActive(false);
            if (lockedText) lockedText.SetActive(true);
            if (lockedSound) lockedSound.Play();
            return;
        }

        // Unlocked: toggle open/close based on current state
        if (doorIsClosed)
        {
            DoOpen();
        }
        else if (doorIsOpen)
        {
            DoClose();
        }
    }

    private void DoOpen()
    {
        if (door)
        {
            door.SetBool("Open", true);
            door.SetBool("Closed", false);
        }
        if (openText) openText.SetActive(false);
        if (closeText) closeText.SetActive(true);   // prompt will now show Close
        if (lockedText) lockedText.SetActive(false);

        if (openSound) openSound.Play();

        doorIsOpen = true;
        doorIsClosed = false;
    }

    private void DoClose()
    {
        if (door)
        {
            door.SetBool("Open", false);
            door.SetBool("Closed", true);
        }
        if (closeText) closeText.SetActive(false);
        if (openText) openText.SetActive(true);    // prompt will now show Open
        if (lockedText) lockedText.SetActive(false);

        if (closeSound) closeSound.Play();

        doorIsClosed = true;
        doorIsOpen = false;
    }

    private IEnumerator unlockDoor()
    {
        // Preserve your tiny delay
        yield return new WaitForSeconds(0.05f);

        unlocked = true;
        locked = false;

        if (lockOB) lockOB.SetActive(false);
        if (lockedText) lockedText.SetActive(false);

        // If player is still in range and the door is closed, restore the "Open" prompt
        if (inReach && doorIsClosed && openText) openText.SetActive(true);
    }
}
