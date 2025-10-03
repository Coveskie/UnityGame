// Assets/Editor/PS_StopActionFixer.cs
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class PS_StopActionFixer
{
    [MenuItem("Tools/VFX/Set StopAction=None on all ParticleSystems in Scene")]
    public static void FixScenePS()
    {
        int changed = 0;
        foreach (var ps in Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var main = ps.main;
            if (main.stopAction != ParticleSystemStopAction.None)
            {
                Undo.RecordObject(ps, "Set PS StopAction None");
                main.stopAction = ParticleSystemStopAction.None;
                changed++;
            }
        }
        Debug.Log($"[PS_StopActionFixer] Changed {changed} ParticleSystems to StopAction=None.");
    }
}
#endif
