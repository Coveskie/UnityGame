#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class SceneFirstAid
{
    [MenuItem("Tools/Scene First Aid/Spawn Emergency Camera (Display 1)")]
    public static void SpawnEmergencyCamera()
    {
        var go = new GameObject("EMERGENCY_CAMERA");
        var cam = go.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.depth = 100;
        cam.targetTexture = null;
        cam.targetDisplay = 0;
        cam.cullingMask = ~0;
#if UNITY_2023_1_OR_NEWER
        if (Object.FindAnyObjectByType<AudioListener>() == null)
#else
        if (Object.FindObjectOfType<AudioListener>() == null)
#endif
            go.AddComponent<AudioListener>();
        Selection.activeGameObject = go;
        Debug.Log("[SceneFirstAid] Spawned EMERGENCY_CAMERA on Display 1.");
    }
}
#endif
