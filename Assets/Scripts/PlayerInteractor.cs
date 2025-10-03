using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class PlayerInteractor : MonoBehaviour
{
    [Header("References")]
    public Transform handSocket;
    public Transform playerRoot;
    public ReachStick reachStick;           // assign your ReachStick component
    public Camera playerCamera;             // for 'looking at' checks

    [Header("Prompt UI")]
    public GameObject promptRoot;
    public Text promptText;
    public string pickupPrompt = "Press E to pick up";

    [Header("Filtering (optional)")]
    public string lanternTag = "";          // leave blank to accept any LanternItem
    public bool useLayerMask = false;
    public LayerMask reachMask = ~0;

    [Header("Looking At Settings")]
    public bool requireLookingAt = true;
    [Range(0.0f, 1.0f)]
    public float lookDotThreshold = 0.85f;
    public bool requireLineOfSight = true;
    public LayerMask lineOfSightMask = ~0;

    [Header("Overlap Settings")]
    [Tooltip("Extra safety: only show prompt when the reach trigger actually overlaps the target's colliders.")]
    public bool requireActualOverlap = true;

    [Header("Debug")]
    public bool drawGizmos = false;
    public Color gizmoColor = new Color(0f, 1f, 1f, 0.25f);

    // State
    private readonly HashSet<LanternItem> _inReach = new HashSet<LanternItem>();
    private LanternItem _currentTarget;
    private LanternItem _heldLantern;
    private Collider[] _playerColliders;
    private Collider _reachCollider;

    void Awake()
    {
        if (playerRoot != null)
            _playerColliders = playerRoot.GetComponentsInChildren<Collider>(includeInactive: false);

        if (reachStick != null)
        {
            reachStick.interactor = this;
            _reachCollider = reachStick.GetComponent<Collider>();
            if (_reachCollider != null && !_reachCollider.isTrigger)
                _reachCollider.isTrigger = true;
        }

        SetPromptVisible(false);
    }

    void Update()
    {
        // Keep the in-reach set honest even if an enter/exit was missed
        if (requireActualOverlap && _reachCollider != null)
            PruneInReachByOverlap();

        UpdateCurrentTarget();
        UpdatePrompt();
    }

    // ===== Input hooks (support all modes) =====
    public void OnInteract() => HandleInteract();
    public void OnInteract(InputValue value) { if (value.isPressed) HandleInteract(); }
    public void OnInteract(InputAction.CallbackContext ctx) { if (ctx.performed) HandleInteract(); }

    public void HandleInteract()
    {
        if (_heldLantern != null)
        {
            _heldLantern.Drop(_playerColliders);
            _heldLantern = null;
            return;
        }

        if (_currentTarget != null && !_currentTarget.IsHeld &&
            (!requireLookingAt || IsLookingAt(_currentTarget)) &&
            (!requireActualOverlap || IsOverlappingReach(_currentTarget)))
        {
            _currentTarget.Pickup(handSocket, _playerColliders);
            _heldLantern = _currentTarget;
        }
    }

    // ===== Called by ReachStick.cs =====
    public void OnReachTriggerEnter(Collider other)
    {
        if (!IsValidReachHit(other)) return;
        var lantern = other.GetComponent<LanternItem>() ?? other.GetComponentInParent<LanternItem>();
        if (lantern == null) return;
        _inReach.Add(lantern);
    }

    public void OnReachTriggerExit(Collider other)
    {
        var lantern = other.GetComponent<LanternItem>() ?? other.GetComponentInParent<LanternItem>();
        if (lantern == null) return;
        _inReach.Remove(lantern);
        if (_currentTarget == lantern) _currentTarget = null;
    }

    private bool IsValidReachHit(Collider other)
    {
        if (useLayerMask && ((reachMask.value & (1 << other.gameObject.layer)) == 0))
            return false;

        if (!string.IsNullOrEmpty(lanternTag))
        {
            if (other.CompareTag(lanternTag)) return true;
            var p = other.transform;
            while (p != null)
            {
                if (p.CompareTag(lanternTag)) return true;
                p = p.parent;
            }
            return false;
        }
        return true;
    }

    private void UpdateCurrentTarget()
    {
        if (_inReach.Count == 0) { _currentTarget = null; return; }

        Transform refPoint = handSocket != null ? handSocket : transform;
        float best = float.PositiveInfinity;
        LanternItem bestLantern = null;

        foreach (var lantern in _inReach)
        {
            if (lantern == null) continue;
            if (requireActualOverlap && !IsOverlappingReach(lantern)) continue; // extra gate
            float d = (lantern.transform.position - refPoint.position).sqrMagnitude;
            if (d < best) { best = d; bestLantern = lantern; }
        }

        _currentTarget = bestLantern;
    }

    private void UpdatePrompt()
    {
        bool show = false;

        if (_heldLantern == null && _currentTarget != null)
        {
            bool overlapOK = !requireActualOverlap || IsOverlappingReach(_currentTarget);
            bool lookOK = !requireLookingAt || IsLookingAt(_currentTarget);
            show = overlapOK && lookOK;
        }

        SetPromptVisible(show);
        if (show && promptText != null) promptText.text = pickupPrompt;
    }

    private void SetPromptVisible(bool visible)
    {
        if (promptRoot != null && promptRoot.activeSelf != visible)
            promptRoot.SetActive(visible);
    }

    // ===== Overlap helpers =====
    private void PruneInReachByOverlap()
    {
        if (_reachCollider == null) return;

        // Build a removal list to avoid modifying the set while iterating
        var removeList = new List<LanternItem>();
        foreach (var lantern in _inReach)
        {
            if (lantern == null) { removeList.Add(lantern); continue; }
            if (!IsOverlappingReach(lantern)) removeList.Add(lantern);
        }
        foreach (var l in removeList) _inReach.Remove(l);
        if (_currentTarget != null && removeList.Contains(_currentTarget))
            _currentTarget = null;
    }

    private bool IsOverlappingReach(LanternItem lantern)
    {
        if (_reachCollider == null || lantern == null) return false;

        // Check any collider on the lantern hierarchy
        var cols = lantern.GetComponentsInChildren<Collider>(includeInactive: false);
        foreach (var c in cols)
        {
            if (c == null) continue;
            if (_reachCollider.bounds.Intersects(c.bounds)) return true;
        }
        return false;
    }

    // ===== Looking-at logic =====
    private bool IsLookingAt(LanternItem target)
    {
        if (playerCamera == null || target == null) return true; // don't block if no camera assigned

        Vector3 lookPoint = GetLookPoint(target);
        Vector3 camPos = playerCamera.transform.position;
        Vector3 toTarget = (lookPoint - camPos).normalized;
        float dot = Vector3.Dot(playerCamera.transform.forward, toTarget);
        if (dot < lookDotThreshold) return false;

        if (requireLineOfSight)
        {
            float dist = Vector3.Distance(camPos, lookPoint);
            if (Physics.Raycast(camPos, toTarget, out RaycastHit hit, dist, lineOfSightMask, QueryTriggerInteraction.Ignore))
            {
                // If we hit something that's not the target (or its children), LOS is blocked
                if (hit.transform != target.transform && hit.transform.root != target.transform)
                    return false;
            }
        }
        return true;
    }

    private Vector3 GetLookPoint(LanternItem target)
    {
        var col = target.GetComponent<Collider>();
        if (col != null) return col.bounds.center;
        var childCol = target.GetComponentInChildren<Collider>();
        if (childCol != null) return childCol.bounds.center;
        return target.transform.position;
    }

    void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;
        if (_currentTarget == null || playerCamera == null) return;

        Gizmos.color = Color.green;
        Gizmos.DrawLine(playerCamera.transform.position, GetLookPoint(_currentTarget));
    }
}
