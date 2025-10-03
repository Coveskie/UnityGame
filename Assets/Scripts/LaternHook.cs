using UnityEngine;

/// <summary>
/// A simple snap point for hanging lanterns.
/// Put this on a wall peg, ceiling hook, or similar.
/// </summary>
public class LanternHook : MonoBehaviour
{
    // Optional anchor transform where the lantern should snap.
    // If not set, the hook's own transform is used.
    public Transform hookAnchor;
}
