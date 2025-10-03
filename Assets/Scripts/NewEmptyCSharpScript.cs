// Assets/Editor/EnableLeakStacks.cs
#if UNITY_EDITOR
using UnityEngine;
using Unity.Collections;
[DefaultExecutionOrder(-10000)]
public class EnableLeakStacks : MonoBehaviour
{
    void Awake() => NativeLeakDetection.Mode = NativeLeakDetectionMode.EnabledWithStackTrace;
}
#endif
