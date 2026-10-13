#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CreateTilePrefab
{
    private const string MenuPath = "Tools/My Tool/Create Tile Prefab";

    [MenuItem(MenuPath)]
    private static void Create()
    {
        var sources = GetSources();
        var created = new List<UnityEngine.Object>();
        int skipped = 0;

        foreach (var source in sources)
        {
            string sourcePath = AssetDatabase.GetAssetPath(source);
            string folder = Path.GetDirectoryName(sourcePath).Replace('\\', '/');
            string name = Path.GetFileNameWithoutExtension(sourcePath) + "_Tile";
            string outputPath = folder + "/" + name + ".prefab";
            if (File.Exists(outputPath) || AssetDatabase.LoadMainAssetAtPath(outputPath) != null)
            {
                skipped++;
                Debug.LogWarning("Tile prefab already exists; skipped: " + outputPath, source);
                continue;
            }

            // Use a preview scene so the user's open scene stays untouched.
            var previewScene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject(name);
                SceneManager.MoveGameObjectToScene(root, previewScene);
                var child = PrefabUtility.InstantiatePrefab(source, previewScene) as GameObject;
                if (child == null)
                    throw new InvalidOperationException("Cannot instantiate " + sourcePath);

                child.transform.SetParent(root.transform, false);
                child.name = source.name;
                child.transform.localPosition = Vector3.zero;
                child.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                // Preserve the source prefab's scale and its nested prefab connection.
                bool success;
                var saved = PrefabUtility.SaveAsPrefabAsset(root, outputPath, out success);
                if (!success || saved == null)
                    throw new InvalidOperationException("Cannot save " + outputPath);
                created.Add(saved);
                Debug.Log("Created tile prefab: " + outputPath, saved);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, source);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }

        if (created.Count > 0)
        {
            Selection.objects = created.ToArray();
            EditorGUIUtility.PingObject(created[0]);
        }
        EditorUtility.DisplayDialog("Create Tile Prefab",
            "Đã tạo: " + created.Count + " prefab.\nBỏ qua do trùng tên: " + skipped +
            ".\nNếu có lỗi, xem Console.", "OK");
    }

    [MenuItem(MenuPath, true)]
    private static bool Validate()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode &&
               !EditorApplication.isCompiling && GetSources().Count > 0;
    }

    private static List<GameObject> GetSources()
    {
        var result = new List<GameObject>();
        foreach (var selected in Selection.objects)
        {
            string path = AssetDatabase.GetAssetPath(selected);
            if (!path.StartsWith("Assets/", StringComparison.Ordinal) ||
                !EditorUtility.IsPersistent(selected))
                continue;
            var source = AssetDatabase.LoadMainAssetAtPath(path) as GameObject;
            if (source != null && PrefabUtility.IsPartOfPrefabAsset(source) &&
                !result.Contains(source))
                result.Add(source);
        }
        return result;
    }
}
#endif
