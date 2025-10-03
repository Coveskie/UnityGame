using System.Collections;
using UnityEngine;

public static class ParticleSafe
{
    public static IEnumerator StopAndDestroy(GameObject go, ParticleSystem ps = null, float timeout = 1f)
    {
        if (!go) yield break;
        if (!ps) ps = go.GetComponentInChildren<ParticleSystem>();
        if (ps)
        {
            // Stop emission and clear buffers safely
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Clear(true);
            // allow render thread/worker to finish a frame
            yield return new WaitForEndOfFrame();

            // wait until no sub-systems are alive (or timeout)
            float t = 0f;
            while (ps.IsAlive(true) && t < timeout)
            {
                t += Time.deltaTime;
                yield return null;
            }
        }
        Object.Destroy(go);
    }
}
