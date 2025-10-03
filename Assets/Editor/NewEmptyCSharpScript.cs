#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class CloseAllGraphWindowsOnLoad
{
    static CloseAllGraphWindowsOnLoad()
    {
        // Run once after domain reload
        EditorApplication.delayCall += () =>
        {
            int closed = 0;
            foreach (var w in Resources.FindObjectsOfTypeAll<EditorWindow>())
            {
                var t = w.GetType();
                var name = t.FullName ?? "";
                // Animator and other Graph windows use these internal types/names:
                // - UnityEditor.Graphs.AnimationStateMachine.AnimatorControllerTool (older)
                // - UnityEditor.AnimatorControllerTool / AnimatorWindow (newer)
                // - Any type under UnityEditor.Graphs.*
                bool looksLikeGraph =
                    name.StartsWith("UnityEditor.Graphs.") ||
                    name.Contains("AnimatorControllerTool") ||
                    name.Contains("AnimatorWindow") ||
                    w.titleContent?.text == "Animator";

                if (looksLikeGraph)
                {
                    w.Close();
                    closed++;
                }
            }
            if (closed > 0)
                Debug.Log($"[CloseAllGraphWindowsOnLoad] Closed {closed} graph window(s) to stop Edge.WakeUp crashes.");
        };
    }
}
#endif
