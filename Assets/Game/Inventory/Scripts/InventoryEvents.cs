using System;
using UnityEngine;

/// <summary>
/// Generic inventory event hub. ANY item script can raise these with its ScriptableObject definition.
/// </summary>
public static class InventoryEvents
{
    /// <summary>Raised when a world item is picked up. Pass the ScriptableObject definition.</summary>
    public static event Action<ScriptableObject> OnItemPickedUp;

    /// <summary>Raised when a world item is dropped. Pass the ScriptableObject definition.</summary>
    public static event Action<ScriptableObject> OnItemDropped;

    public static void RaisePickedUp(ScriptableObject def) => OnItemPickedUp?.Invoke(def);
    public static void RaiseDropped(ScriptableObject def) => OnItemDropped?.Invoke(def);
}
