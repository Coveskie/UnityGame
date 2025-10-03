// Assets/Scripts/Utility/SafeDeactivate.cs
using System.Collections;
using UnityEngine;

public static class SafeDeactivate
{
    public static IEnumerator ParticleAware(GameObject go, float timeout = 0.75f)
    {
        if (!go) yield break;
        var ps = go.GetComponentInChildren<ParticleSystem>();
        if (ps)
        {
            // If it’s still alive, stop + clear, then wait a render frame
            if (ps.IsAlive(true))
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.Clear(true);
                yield return new WaitForEndOfFrame(); // let worker finish fences

                // optional: wait while sub-emitters wind down (bounded)
                float t = 0f;
                while (ps.IsAlive(true) && t < timeout)
                {
                    t += Time.unscaledDeltaTime;
                    yield return null;
                }
            }
        }
        go.SetActive(false);
    }
}
