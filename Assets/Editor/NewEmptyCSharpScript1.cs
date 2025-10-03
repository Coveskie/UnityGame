#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using System.IO;

public static class AnimatorControllerResetter
{
    [MenuItem("Tools/Animator/Clone & Reset Selected Controller")]
    public static void CloneAndReset()
    {
        var src = Selection.activeObject as AnimatorController;
        if (!src)
        {
            EditorUtility.DisplayDialog("Clone & Reset", "Select an AnimatorController asset first.", "OK");
            return;
        }

        // 1) Duplicate next to source as a backup
        string srcPath = AssetDatabase.GetAssetPath(src);
        string dir = Path.GetDirectoryName(srcPath);
        string nameNoExt = Path.GetFileNameWithoutExtension(srcPath);
        string backupPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(dir, nameNoExt + "_BACKUP.controller"));
        AssetDatabase.CopyAsset(srcPath, backupPath);

        // 2) Create a fresh controller to replace the selected asset
        string tempPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(dir, nameNoExt + "_TEMP.controller"));
        var fresh = new AnimatorController();
        AssetDatabase.CreateAsset(fresh, tempPath);

        var layer = new AnimatorControllerLayer { name = "Base Layer", defaultWeight = 1f, stateMachine = new AnimatorStateMachine() };
        layer.stateMachine.name = "BaseSM";
        var idle = layer.stateMachine.AddState("Idle");
        fresh.AddLayer(layer);

        EditorUtility.SetDirty(fresh);
        AssetDatabase.SaveAssets();

        // 3) Swap asset contents: delete original, move temp to original path
        AssetDatabase.DeleteAsset(srcPath);
        AssetDatabase.MoveAsset(tempPath, srcPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[AnimatorControllerResetter] Backed up to {backupPath} and replaced original with a minimal fresh controller at {srcPath}.");
        var newCtrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(srcPath);
        EditorGUIUtility.PingObject(newCtrl);
    }
}
#endif
