using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInput))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Refs")]
    public Camera playerCamera;

    [Header("Movement")]
    public float walkSpeed = 6f;
    public float runSpeed = 12f;
    public float jumpPower = 7f;
    public float gravity = 10f;

    [Header("Look")]
    public float lookSpeed = 2f;
    public float lookXLimit = 45f;

    [Header("Crouch")]
    public float defaultHeight = 2f;
    public float crouchHeight = 1f;
    public float crouchSpeed = 3f;

    [Header("Input (assign either References OR use Names)")]
    public InputActionReference moveRef;
    public InputActionReference lookRef;
    public InputActionReference jumpRef;
    public InputActionReference runRef;
    public InputActionReference crouchRef;

    [Tooltip("Used only if the corresponding *Ref is not assigned.")]
    public string moveActionName = "Move";
    public string lookActionName = "Look";
    public string jumpActionName = "Jump";
    public string runActionName = "Run";     // <- change this if your asset uses 'Sprint'
    public string crouchActionName = "Crouch";

    private CharacterController _controller;
    private PlayerInput _playerInput;

    private InputAction _moveAction;
    private InputAction _lookAction;
    private InputAction _jumpAction;
    private InputAction _runAction;
    private InputAction _crouchAction;

    private Vector3 _velocity = Vector3.zero;
    private float _rotationX = 0f;
    private bool _canMove = true;

    // === Freeze support ===
    public bool IsFrozen { get; private set; }
    private int _freezeCount = 0;

    /// <summary>
    /// Ref-counted freeze. Call Freeze(true) to lock player; Freeze(false) to release.
    /// Safe for multiple callers (freezeCount).
    /// </summary>
    public void Freeze(bool on)
    {
        if (on) _freezeCount++;
        else _freezeCount = Mathf.Max(0, _freezeCount - 1);

        bool shouldFreeze = _freezeCount > 0;

        if (shouldFreeze == IsFrozen) return;

        IsFrozen = shouldFreeze;
        _canMove = !shouldFreeze;

        if (IsFrozen)
        {
            // Hard stop: zero horizontal motion and prevent gravity drift during the cinematic.
            _velocity = Vector3.zero;
            // Keep the cursor locked (no UI pop) during freeze.
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _playerInput = GetComponent<PlayerInput>();

        var asset = _playerInput != null ? _playerInput.actions : null;
        if (asset == null)
        {
            Debug.LogError("[PlayerMovement] No InputActionAsset on PlayerInput. Assign one in the PlayerInput component.");
            return;
        }

        // Prefer explicit references (safest). If not provided, fall back to names.
        _moveAction = moveRef ? moveRef.action : asset.FindAction(moveActionName, throwIfNotFound: false);
        _lookAction = lookRef ? lookRef.action : asset.FindAction(lookActionName, throwIfNotFound: false);
        _jumpAction = jumpRef ? jumpRef.action : asset.FindAction(jumpActionName, throwIfNotFound: false);
        _runAction = runRef ? runRef.action : asset.FindAction(runActionName, throwIfNotFound: false);
        _crouchAction = crouchRef ? crouchRef.action : asset.FindAction(crouchActionName, throwIfNotFound: false);

        // Helpful diagnostics if anything is missing
        if (_moveAction == null || _lookAction == null || _jumpAction == null || _runAction == null || _crouchAction == null)
        {
            if (_moveAction == null) Debug.LogWarning(MissingMsg("Move", moveActionName));
            if (_lookAction == null) Debug.LogWarning(MissingMsg("Look", lookActionName));
            if (_jumpAction == null) Debug.LogWarning(MissingMsg("Jump", jumpActionName));
            if (_runAction == null) Debug.LogWarning(MissingMsg("Run", runActionName));
            if (_crouchAction == null) Debug.LogWarning(MissingMsg("Crouch", crouchActionName));
            LogAvailableActions(asset);
        }
    }

    private void OnEnable()
    {
        if (_moveAction != null) _moveAction.Enable();
        if (_lookAction != null) _lookAction.Enable();
        if (_jumpAction != null) _jumpAction.Enable();
        if (_runAction != null) _runAction.Enable();
        if (_crouchAction != null) _crouchAction.Enable();
    }

    private void OnDisable()
    {
        // Guard against nulls to avoid NREs
        if (_moveAction != null) _moveAction.Disable();
        if (_lookAction != null) _lookAction.Disable();
        if (_jumpAction != null) _jumpAction.Disable();
        if (_runAction != null) _runAction.Disable();
        if (_crouchAction != null) _crouchAction.Disable();
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        if (_controller != null) _controller.height = defaultHeight;
    }

    private void Update()
    {
        if (_controller == null) return;

        // === Hard freeze path ===
        if (IsFrozen)
        {
            // Do not read input, do not move, do not rotate, do not apply gravity.
            // We also maintain whatever camera rotation another system (e.g., CameraFocusController)
            // is applying—so no snap-back.
            _controller.Move(Vector3.zero);
            return;
        }

        Vector2 move = (_canMove && _moveAction != null) ? _moveAction.ReadValue<Vector2>() : Vector2.zero;
        Vector2 look = (_canMove && _lookAction != null) ? _lookAction.ReadValue<Vector2>() : Vector2.zero;

        bool isRunning = _runAction != null && _runAction.IsPressed();
        bool isCrouched = _crouchAction != null && _crouchAction.IsPressed();

        float currentSpeed = isCrouched ? crouchSpeed : (isRunning ? runSpeed : walkSpeed);

        Vector3 forward = transform.TransformDirection(Vector3.forward);
        Vector3 right = transform.TransformDirection(Vector3.right);

        float velX = currentSpeed * move.y;
        float velZ = currentSpeed * move.x;

        float prevY = _velocity.y;
        _velocity = forward * velX + right * velZ;

        if (_controller.isGrounded)
        {
            _velocity.y = 0f;
            if (_canMove && _jumpAction != null && _jumpAction.WasPressedThisFrame() && !isCrouched)
            {
                _velocity.y = jumpPower;
            }
        }
        else
        {
            _velocity.y = prevY;
        }

        if (!_controller.isGrounded)
            _velocity.y -= gravity * Time.deltaTime;

        _controller.height = isCrouched ? crouchHeight : defaultHeight;

        _controller.Move(_velocity * Time.deltaTime);

        if (_canMove && playerCamera != null)
        {
            _rotationX += -look.y * lookSpeed;
            _rotationX = Mathf.Clamp(_rotationX, -lookXLimit, lookXLimit);
            playerCamera.transform.localRotation = Quaternion.Euler(_rotationX, 0f, 0f);
            transform.rotation *= Quaternion.Euler(0f, look.x * lookSpeed, 0f);
        }
    }

    private static string MissingMsg(string field, string name)
        => $"[PlayerMovement] Input action missing: {field} (looked for '{name}'). " +
           $"Assign an InputActionReference or change the action name to match your asset.";

    private static void LogAvailableActions(InputActionAsset asset)
    {
        if (asset == null) return;
        foreach (var map in asset.actionMaps)
        {
            foreach (var act in map.actions)
                Debug.Log($"[PlayerMovement] Available action: '{map.name}/{act.name}'");
        }
    }
}
