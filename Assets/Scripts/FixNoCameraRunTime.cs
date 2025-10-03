using UnityEngine;

public class FixNoCameraRuntime : MonoBehaviour
{
    void Awake()
    {
        if (!HasDisplay1Renderer())
        {
            var go = new GameObject("EMERGENCY_CAMERA");
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.depth = 100;
            cam.targetTexture = null; // render to screen
            cam.targetDisplay = 0;    // Display 1
            cam.cullingMask = ~0;     // Everything

            if (!HasAudioListener())
                go.AddComponent<AudioListener>();

            Debug.Log("[FixNoCameraRuntime] Spawned EMERGENCY_CAMERA for Display 1.");
        }
    }

    bool HasDisplay1Renderer()
    {
        foreach (var c in Camera.allCameras)
        {
            if (c && c.isActiveAndEnabled && c.gameObject.activeInHierarchy &&
                c.targetTexture == null && c.targetDisplay == 0)
                return true;
        }
        return false;
    }

    bool HasAudioListener()
    {
#if UNITY_2023_1_OR_NEWER
        return FindAnyObjectByType<AudioListener>() != null;
#else
        return FindObjectOfType<AudioListener>() != null;
#endif
    }
}
