#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Unity.Collections;

[InitializeOnLoad]
public static class EnableLeakStacksOnLoad
{
    static EnableLeakStacksOnLoad()
    {
        // Turn on detailed leak stacks in the Editor
        NativeLeakDetection.Mode = NativeLeakDetectionMode.EnabledWithStackTrace;
        Debug.Log("[LeakStacks] NativeLeakDetection: EnabledWithStackTrace");

        // Optional: ensure Console shows full stacks for warnings/errors
        // Do this once manually too: Console gear icon → Stack Trace Logging → Full
    }
}
#endif
