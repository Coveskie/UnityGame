// Assets/Editor/ParticlesSafetyBoot.cs
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class ParticlesSafetyBoot
{
    static ParticlesSafetyBoot()
    {
        EditorApplication.playModeStateChanged += s =>
        {
            if (s != PlayModeStateChange.EnteredPlayMode) return;

            int added = 0;
            foreach (var ps in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
            {
                if (!ps) continue;
                var go = ps.gameObject;
                if (!go.TryGetComponent<ParticleDeactivationGuard>(out _))
                {
                    go.AddComponent<ParticleDeactivationGuard>();
                    added++;
                }

                // Optional hardening: disable GPU instancing at runtime while debugging
                var r = ps.GetComponent<ParticleSystemRenderer>();
                if (r && r.enableGPUInstancing) r.enableGPUInstancing = false;

                // Ensure Stop Action is Disable
                var m = ps.main; 
                if (m.stopAction != ParticleSystemStopAction.Disable)
                {
                    m.stopAction = ParticleSystemStopAction.Disable;
                }
            }
            Debug.Log($"[ParticlesSafetyBoot] Guards added: {added}. GPU instancing disabled on all particle renderers (debug).");
        };
    }
}
#endif
