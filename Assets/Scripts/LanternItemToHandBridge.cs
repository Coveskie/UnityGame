using UnityEngine;

[RequireComponent(typeof(LanternItem))]
public class LanternItemToInventoryBridge : MonoBehaviour
{
    LanternItem _lantern;

    void Awake()
    {
        _lantern = GetComponent<LanternItem>();
        _lantern.OnPickedUp += HandlePickedUp;
        _lantern.OnDropped += HandleDropped;
    }
    void OnDestroy()
    {
        if (_lantern != null)
        {
            _lantern.OnPickedUp -= HandlePickedUp;
            _lantern.OnDropped -= HandleDropped;
        }
    }

    void HandlePickedUp(LanternItem item, ScriptableObject def)
    {
        InventoryEvents.RaisePickedUp(def);
    }

    void HandleDropped(LanternItem item, ScriptableObject def)
    {
        InventoryEvents.RaiseDropped(def);
    }
}

