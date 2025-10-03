// Assets/Scripts/VFX/ParentParticleDeactivationGuard.cs
using System.Collections;
using UnityEngine;

[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
public class ParentParticleDeactivationGuard : MonoBehaviour
{
    static bool _quitting;
    bool _handling;

    void OnApplicationQuit() => _quitting = true;

    void OnDisable()
    {
        if (!Application.isPlaying || _quitting) return;
        if (_handling) return;

        // If we were disabled by someone, re-enable and safely deactivate the **entire** hierarchy.
        _handling = true;

        // If a parent is already inactive, we can't re-enable ourselves; bail quietly.
        if (!gameObject.activeInHierarchy)
        {
            _handling = false;
            return;
        }

        // Cancel this disable and do it the safe way
        gameObject.SetActive(true);
        StartCoroutine(DoSafeDisable());
    }

    IEnumerator DoSafeDisable()
    {
        yield return SafeHierarchyDeactivate.ParticleAware(gameObject);
        _handling = false;
    }
}
