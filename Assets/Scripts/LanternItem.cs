using System;
using System.Reflection;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class LanternItem : MonoBehaviour
{
    [Header("Data")]
    [Tooltip("ScriptableObject describing this item (name, icon, etc.). REQUIRED.")]
    [SerializeField] private ScriptableObject itemDefinition;

    [Header("Hold Pose (local to hand)")]
    public Vector3 holdLocalPosition = new Vector3(0.12f, -0.02f, 0.25f);
    public Vector3 holdLocalEulerAngles = new Vector3(0f, 90f, 0f);

    [Header("Held Visuals")]
    [Tooltip("Keeps the same world size when parented under the hand (even if the hand is scaled).")]
    public bool keepWorldSizeWhenHeld = true;
    [Tooltip("If true, force this exact local scale while held instead of preserving world size.")]
    public bool overrideHeldScale = false;
    public Vector3 heldLocalScale = Vector3.one;

    [Header("Options")]
    [Tooltip("Disable all colliders while the item is held.")]
    public bool disableCollidersWhileHeld = true;
    [Tooltip("Forward shove speed when dropped (uses playerForward).")]
    public float dropForwardVelocity = 0f;
    [Tooltip("Reserved for future smooth pickup pose; currently unused.")]
    public float pickupLerpTime = 0f;

    // Decoupled notifications (inventory/UI can subscribe without tight coupling)
    public event Action<LanternItem, ScriptableObject> OnPickedUp;
    public event Action<LanternItem, ScriptableObject> OnDropped;

    Rigidbody _rb;
    Collider[] _allColliders;
    Transform _originalParent;
    Vector3 _originalLocalScale;
    bool _isHeld;

    /// Public read-only flag
    public bool IsHeld => _isHeld;

    void Reset()
    {
        CacheComponents();
        TryAutoWireDefinition();
    }

    void OnValidate()
    {
        if (itemDefinition == null)
            TryAutoWireDefinition();
    }

    void Awake()
    {
        CacheComponents();

        if (itemDefinition == null)
        {
            TryAutoWireDefinition();
            if (itemDefinition == null)
            {
                Debug.LogError(
                    $"LanternItem on '{GetPath(transform)}' is missing ItemDefinition. " +
                    $"Assign a ScriptableObject to 'itemDefinition' (on this exact instance)."
                );
            }
        }
    }

    void CacheComponents()
    {
        if (!_rb) _rb = GetComponent<Rigidbody>();
        if (_allColliders == null || _allColliders.Length == 0)
            _allColliders = GetComponentsInChildren<Collider>(includeInactive: true);
        if (_originalParent == null) _originalParent = transform.parent;
        if (_originalLocalScale == default) _originalLocalScale = transform.localScale;
    }

    /// Editor convenience: scan self/parents for ANY ScriptableObject field that looks like a definition.
    void TryAutoWireDefinition()
    {
        if (itemDefinition != null) return;

        Transform t = transform;
        string[] preferredNames = { "definition", "itemDefinition", "data", "itemDef", "def" };

        while (t != null)
        {
            var comps = t.GetComponents<Component>();
            foreach (var comp in comps)
            {
                if (!comp) continue;
                var compType = comp.GetType();

                // First pass: prefer common field names, public or [SerializeField] private
                foreach (var name in preferredNames)
                {
                    var field = compType.GetField(name,
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field == null) continue;
                    if (!typeof(ScriptableObject).IsAssignableFrom(field.FieldType)) continue;

                    var so = field.GetValue(comp) as ScriptableObject;
                    if (so != null)
                    {
                        itemDefinition = so;
                        return;
                    }
                }

                // Second pass: ANY ScriptableObject field with a non-null value
                var fields = compType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (var f in fields)
                {
                    if (!typeof(ScriptableObject).IsAssignableFrom(f.FieldType)) continue;
                    var so = f.GetValue(comp) as ScriptableObject;
                    if (so != null)
                    {
                        itemDefinition = so;
                        return;
                    }
                }
            }

            t = t.parent;
        }
    }

    public ScriptableObject GetDefinition() => itemDefinition;

    /// Call from your interactor to pick this up to a hand anchor.
    public bool Pickup(Transform hand, Collider[] overlaps = null)
    {
        if (itemDefinition == null)
        {
            Debug.LogWarning(
                $"LanternItem missing ItemDefinition on '{GetPath(transform)}'. " +
                $"Assign the asset on the LanternItem component."
            );
        }
        if (_isHeld) return false;

        _isHeld = true;

        if (_rb)
        {
            // Only set velocities if currently non-kinematic; Unity forbids setting them on kinematic bodies
            if (!_rb.isKinematic)
            {
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
            }
            _rb.isKinematic = true;
            // _rb.Sleep(); // optional
        }

        if (disableCollidersWhileHeld && _allColliders != null)
        {
            foreach (var c in _allColliders)
                if (c) c.enabled = false;
        }

        // Preserve world size if desired
        Vector3 preParentLossy = transform.lossyScale;

        transform.SetParent(hand, worldPositionStays: false);
        transform.localPosition = holdLocalPosition;
        transform.localRotation = Quaternion.Euler(holdLocalEulerAngles);

        if (overrideHeldScale)
        {
            transform.localScale = heldLocalScale;
        }
        else if (keepWorldSizeWhenHeld)
        {
            // Adjust local scale so that lossy scale (visual world size) stays the same under the new parent
            Vector3 handLossy = hand.lossyScale;
            transform.localScale = SafeDivide(preParentLossy, handLossy);
        }
        // else: keep current localScale as-is

        // Notify listeners
        OnPickedUp?.Invoke(this, itemDefinition);
        // 🔔 Generic inventory event (UI listens and shows the icon)
        InventoryEvents.RaisePickedUp(itemDefinition);

        return true;
    }

    /// New main Drop API
    public void Drop(Vector3 worldPos, Quaternion worldRot, Vector3 playerForward)
    {
        if (!_isHeld) return;
        _isHeld = false;

        transform.SetParent(_originalParent, worldPositionStays: true);
        transform.SetPositionAndRotation(worldPos, worldRot);

        // Restore the original local scale we had under the original parent
        transform.localScale = _originalLocalScale;

        if (_rb)
        {
            _rb.isKinematic = false; // must be non-kinematic before applying velocity
            if (dropForwardVelocity > 0f && playerForward.sqrMagnitude > 0.0001f)
            {
                playerForward.Normalize();
                _rb.linearVelocity = playerForward * dropForwardVelocity;
            }
        }

        if (disableCollidersWhileHeld && _allColliders != null)
        {
            foreach (var c in _allColliders)
                if (c) c.enabled = true;
        }

        // Notify listeners
        OnDropped?.Invoke(this, itemDefinition);
        // 🔔 Generic inventory event (UI listens and clears the icon)
        InventoryEvents.RaiseDropped(itemDefinition);
    }

    // --------------------------
    // Back-compat overloads
    // --------------------------

    /// <summary>Drop in place (keeps current pose, no forward shove).</summary>
    public void Drop()
    {
        Drop(transform.position, transform.rotation, Vector3.zero);
    }

    /// <summary>Drop at position (keeps current rotation, no forward shove).</summary>
    public void Drop(Vector3 worldPos)
    {
        Drop(worldPos, transform.rotation, Vector3.zero);
    }

    /// <summary>Drop at position with a forward shove (keeps current rotation).</summary>
    public void Drop(Vector3 worldPos, Vector3 playerForward)
    {
        Drop(worldPos, transform.rotation, playerForward);
    }

    /// <summary>
    /// Back-compat: accepts Collider[] like the old API (ignored)
    /// </summary>
    public void Drop(Collider[] overlaps)
    {
        Drop(transform.position, transform.rotation, Vector3.zero);
    }

    // -----------------------
    // Utilities & Debugging
    // -----------------------
    static string GetPath(Transform t)
    {
        if (!t) return "<null>";
        var path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }

    static Vector3 SafeDivide(Vector3 a, Vector3 b)
    {
        float x = (Mathf.Abs(b.x) < 1e-6f) ? 1f : b.x;
        float y = (Mathf.Abs(b.y) < 1e-6f) ? 1f : b.y;
        float z = (Mathf.Abs(b.z) < 1e-6f) ? 1f : b.z;
        return new Vector3(a.x / x, a.y / y, a.z / z);
    }

#if UNITY_EDITOR
    [ContextMenu("LanternItem/Debug → Print ItemDefinition Status")]
    void DebugPrintDefinition()
    {
        string path = GetPath(transform);
        string defName = itemDefinition ? itemDefinition.name : "<null>";
        Debug.Log($"[LanternItem] {path} → itemDefinition: {defName}");
    }
#endif
}
