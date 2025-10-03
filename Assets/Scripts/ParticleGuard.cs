// Assets/Scripts/VFX/ParticleDeactivationGuard.cs
using System.Collections;
using UnityEngine;

[DefaultExecutionOrder(100)]           // run a bit later than gameplay scripts
[DisallowMultipleComponent]
public class ParticleDeactivationGuard : MonoBehaviour
{
    ParticleSystem _ps;
    bool _handling;                    // prevents re-entrancy loops
    static bool _isQuitting;           // ignore during app quit

    void Awake()
    {
        _ps = GetComponentInChildren<ParticleSystem>();
    }

    void OnApplicationQuit()
    {
        _isQuitting = true;
    }

    // If something disables us while particles are alive, defer the disable safely.
    void OnDisable()
    {
        // Editor recompiles / scene unload / quitting
        if (!Application.isPlaying || _isQuitting) return;

        if (_handling) return;         // already processing a safe disable
        if (_ps == null) return;

        // If still alive, we were disabled too soon — re-enable and safely defer.
        if (_ps.IsAlive(true))
        {
            StartCoroutine(ReDisableSafely());
        }
    }

    IEnumerator ReDisableSafely()
    {
        _handling = true;

        // If we're already inactive (parent got disabled), briefly re-activate just this GO
        // to run a safe path; if hierarchy prevents that, bail out.
        if (!gameObject.activeSelf)
        {
            // Try to re-enable this object (will do nothing if parent inactive)
            gameObject.SetActive(true);
            // If parent is inactive, we cannot fix this here — just exit quietly.
            if (!gameObject.activeInHierarchy)
            {
                _handling = false;
                yield break;
            }
        }

        // Defer to the particle-aware deactivation utility (uses WaitForEndOfFrame)
        yield return SafeDeactivate.ParticleAware(gameObject);

        _handling = false;
    }
}
