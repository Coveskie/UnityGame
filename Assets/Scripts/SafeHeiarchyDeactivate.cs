// Assets/Scripts/Utility/SafeHierarchyDeactivate.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class SafeHierarchyDeactivate
{
    /// <param name="extraFrames">Additional frames to wait after WaitForEndOfFrame to let the Gfx worker drop fences.</param>
    public static IEnumerator ParticleAware(GameObject root, int extraFrames = 2, float timeoutPerPS = 0.75f)
    {
        if (!root) yield break;

        var psList = new List<ParticleSystem>();
        root.GetComponentsInChildren(true, psList);

        // Stop emission + clear for all children
        foreach (var ps in psList)
        {
            if (!ps) continue;
            var em = ps.emission; em.enabled = false;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Clear(true);
        }

        // Let the render thread process the clears at the end of *this* frame
        yield return new WaitForEndOfFrame();

        // Give the worker a couple more frames to retire geometry fences
        for (int i = 0; i < Mathf.Max(0, extraFrames); i++)
            yield return null;

        // Optional bounded wait while sub-emitters wind down
        float elapsed = 0f;
        while (elapsed < timeoutPerPS)
        {
            bool anyAlive = false;
            foreach (var ps in psList)
            {
                if (ps && ps.IsAlive(true)) { anyAlive = true; break; }
            }
            if (!anyAlive) break;
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        root.SetActive(false);
    }
}
