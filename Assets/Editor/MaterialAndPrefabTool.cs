/*
CÔNG DỤNG:
Tool này gộp 2 chức năng vào cùng một cửa sổ:
1. GEN MATERIAL: Chọn nhiều Texture trong Project, dùng 1 Material mẫu (Template Material)
   để tự tạo ra các Material mới và gán Texture tương ứng vào _MainTex hoặc _BaseMap.
2. GEN PREFAB: Không chọn Material thì giữ Material gốc và tên Model; chọn 1 Material thì
   dùng Material đó và giữ tên Model. Chọn nhiều Material thì tạo toàn bộ tổ hợp Prefab
   theo cú pháp "TênModel_Màu", ví dụ bucket + BeachSet_Blue -> bucket_Blue.
   Có 2 chế độ tạo Prefab:
   - Generate Prefabs: Model bên trong có Rotation Y = 0.
   - Generate Prefabs Y=180: Model bên trong có Rotation Y = 180.
   Cả hai đều reset Position = (0,0,0), Scale = (1,1,1) và apply Material tương ứng.
*/

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MaterialAndPrefabTool : EditorWindow
{
    // =========================================================
    // PART 1 - GENERATE MATERIALS
    // =========================================================

    [SerializeField] private Material templateMaterial;
    [SerializeField] private DefaultAsset materialOutputFolder;

    // =========================================================
    // PART 2 - GENERATE PREFABS
    // =========================================================

    [SerializeField] private DefaultAsset prefabOutputFolder;
    [SerializeField] private List<GameObject> models = new List<GameObject>();
    [SerializeField] private List<Material> materials = new List<Material>();
    [SerializeField] private bool overwriteExisting = true;

    private Vector2 prefabScroll;

    [MenuItem("Tools/My Tool/Material & Prefab Generator")]
    public static void ShowWindow()
    {
        GetWindow<MaterialAndPrefabTool>("Material & Prefab Tool");
    }

    private void OnGUI()
    {
        DrawMaterialGenerator();

        EditorGUILayout.Space(12);
        DrawSeparator();
        EditorGUILayout.Space(12);

        DrawPrefabGenerator();
    }

    // =========================================================
    // UI - PART 1
    // =========================================================

    private void DrawMaterialGenerator()
    {
        EditorGUILayout.LabelField(
            "PART 1 - GENERATE MATERIALS FROM TEXTURES",
            EditorStyles.boldLabel
        );

        EditorGUILayout.Space(4);

        templateMaterial = (Material)EditorGUILayout.ObjectField(
            "Template Material",
            templateMaterial,
            typeof(Material),
            false
        );

        materialOutputFolder = (DefaultAsset)EditorGUILayout.ObjectField(
            "Output Folder",
            materialOutputFolder,
            typeof(DefaultAsset),
            false
        );

        EditorGUILayout.HelpBox(
            "Chọn các Texture trong Project rồi bấm Generate Materials.",
            MessageType.Info
        );

        GUI.enabled = templateMaterial != null && materialOutputFolder != null;

        if (GUILayout.Button("GENERATE MATERIALS", GUILayout.Height(32)))
        {
            GenerateMaterials();
        }

        GUI.enabled = true;
    }

    // =========================================================
    // UI - PART 2
    // =========================================================

    private void DrawPrefabGenerator()
    {
        EditorGUILayout.LabelField(
            "PART 2 - BATCH PREFAB GENERATOR",
            EditorStyles.boldLabel
        );

        EditorGUILayout.Space(4);

        prefabOutputFolder = (DefaultAsset)EditorGUILayout.ObjectField(
            "Output Folder",
            prefabOutputFolder,
            typeof(DefaultAsset),
            false
        );

        overwriteExisting = EditorGUILayout.Toggle(
            "Overwrite Existing",
            overwriteExisting
        );

        EditorGUILayout.HelpBox(
            "Chọn Model/FBX và Material (tùy chọn), rồi bấm Load Current Selection. Không chọn Material: giữ Material gốc. Chọn 0 hoặc 1 Material: giữ tên Model. Chọn nhiều Material: thêm hậu tố màu.",
            MessageType.Info
        );

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Load Current Selection"))
        {
            LoadPrefabSelection();
        }

        if (GUILayout.Button("Clear"))
        {
            models.Clear();
            materials.Clear();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6);

        prefabScroll = EditorGUILayout.BeginScrollView(
            prefabScroll,
            GUILayout.Height(220)
        );

        EditorGUILayout.LabelField(
            $"Models ({models.Count})",
            EditorStyles.boldLabel
        );

        foreach (GameObject model in models)
        {
            if (model != null)
            {
                EditorGUILayout.LabelField("• " + model.name);
            }
        }

        EditorGUILayout.Space(8);

        EditorGUILayout.LabelField(
            $"Materials ({materials.Count})",
            EditorStyles.boldLabel
        );

        foreach (Material mat in materials)
        {
            if (mat != null)
            {
                EditorGUILayout.LabelField("• " + mat.name);
            }
        }

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(6);

        int prefabCount = models.Count * Mathf.Max(1, materials.Count);

        EditorGUILayout.HelpBox(
            $"Sẽ tạo: {prefabCount} prefab",
            MessageType.None
        );

        GUI.enabled =
            models.Count > 0 &&
            prefabOutputFolder != null;

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("GENERATE PREFABS", GUILayout.Height(36)))
        {
            GeneratePrefabs(0f);
        }

        if (GUILayout.Button("GENERATE PREFABS Y=180", GUILayout.Height(36)))
        {
            GeneratePrefabs(180f);
        }

        EditorGUILayout.EndHorizontal();

        GUI.enabled = true;
    }

    // =========================================================
    // PART 1 - MATERIAL LOGIC
    // =========================================================

    private void GenerateMaterials()
    {
        string folderPath =
            AssetDatabase.GetAssetPath(materialOutputFolder);

        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            Debug.LogError("Material Output Folder không hợp lệ.");
            return;
        }

        int createdCount = 0;

        foreach (Object selectedObject in Selection.objects)
        {
            Texture2D texture = selectedObject as Texture2D;

            if (texture == null)
            {
                continue;
            }

            Material newMaterial = new Material(templateMaterial);

            if (newMaterial.HasProperty("_MainTex"))
            {
                newMaterial.SetTexture("_MainTex", texture);
            }
            else if (newMaterial.HasProperty("_BaseMap"))
            {
                newMaterial.SetTexture("_BaseMap", texture);
            }
            else
            {
                Debug.LogWarning(
                    $"Không tìm thấy _MainTex hoặc _BaseMap trong Template Material. Texture bỏ qua: {texture.name}"
                );

                DestroyImmediate(newMaterial);
                continue;
            }

            newMaterial.name = texture.name;

            string materialPath = Path.Combine(
                folderPath,
                texture.name + ".mat"
            ).Replace("\\", "/");

            materialPath =
                AssetDatabase.GenerateUniqueAssetPath(materialPath);

            AssetDatabase.CreateAsset(
                newMaterial,
                materialPath
            );

            createdCount++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"[Material Generator] Đã tạo {createdCount} material."
        );

        EditorUtility.DisplayDialog(
            "Generate Materials",
            $"Đã tạo {createdCount} material.",
            "OK"
        );
    }

    // =========================================================
    // PART 2 - PREFAB SELECTION
    // =========================================================

    private void LoadPrefabSelection()
    {
        models.Clear();
        materials.Clear();

        foreach (Object obj in Selection.objects)
        {
            if (obj is Material mat)
            {
                if (!materials.Contains(mat))
                {
                    materials.Add(mat);
                }
            }
            else if (obj is GameObject go)
            {
                string path = AssetDatabase.GetAssetPath(go);

                if (
                    !string.IsNullOrEmpty(path) &&
                    !models.Contains(go)
                )
                {
                    models.Add(go);
                }
            }
        }

        Repaint();
    }

    // =========================================================
    // PART 2 - PREFAB LOGIC
    // =========================================================

    private void GeneratePrefabs(float modelRotationY)
    {
        string outputPath =
            AssetDatabase.GetAssetPath(prefabOutputFolder);

        if (
            string.IsNullOrEmpty(outputPath) ||
            !AssetDatabase.IsValidFolder(outputPath)
        )
        {
            EditorUtility.DisplayDialog(
                "Invalid Output Folder",
                "Prefab Output Folder không hợp lệ.",
                "OK"
            );

            return;
        }

        int successCount = 0;
        int skippedCount = 0;
        int failedCount = 0;

        int total = models.Count * Mathf.Max(1, materials.Count);
        int current = 0;

        try
        {
            foreach (GameObject model in models)
            {
                if (model == null)
                {
                    continue;
                }

                int variantCount = Mathf.Max(1, materials.Count);

                for (int materialIndex = 0; materialIndex < variantCount; materialIndex++)
                {
                    Material mat = materials.Count > 0
                        ? materials[materialIndex]
                        : null;

                    if (materials.Count > 0 && mat == null)
                    {
                        continue;
                    }

                    current++;

                    string prefabName = materials.Count > 1
                        ? $"{model.name}_{GetColorName(mat.name)}"
                        : model.name;

                    string prefabPath =
                        $"{outputPath}/{prefabName}.prefab";

                    EditorUtility.DisplayProgressBar(
                        "Generating Prefabs",
                        prefabName,
                        total > 0
                            ? (float)current / total
                            : 1f
                    );

                    GameObject existingPrefab =
                        AssetDatabase.LoadAssetAtPath<GameObject>(
                            prefabPath
                        );

                    if (existingPrefab != null)
                    {
                        if (!overwriteExisting)
                        {
                            skippedCount++;
                            continue;
                        }

                        if (!AssetDatabase.DeleteAsset(prefabPath))
                        {
                            Debug.LogError(
                                $"Không xóa được prefab cũ: {prefabPath}"
                            );

                            failedCount++;
                            continue;
                        }
                    }

                    bool success = CreateRegularPrefab(
                        model,
                        mat,
                        prefabName,
                        prefabPath,
                        modelRotationY
                    );

                    if (success)
                    {
                        successCount++;
                    }
                    else
                    {
                        failedCount++;
                    }
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Generate Prefabs",
            $"Created: {successCount}\n" +
            $"Skipped: {skippedCount}\n" +
            $"Failed: {failedCount}",
            "OK"
        );
    }

    private static bool CreateRegularPrefab(
        GameObject model,
        Material mat,
        string prefabName,
        string prefabPath,
        float modelRotationY
    )
    {
        Scene previewScene =
            EditorSceneManager.NewPreviewScene();

        try
        {
            GameObject root =
                new GameObject(prefabName);

            SceneManager.MoveGameObjectToScene(
                root,
                previewScene
            );

            root.transform.localPosition =
                Vector3.zero;

            root.transform.localRotation =
                Quaternion.identity;

            root.transform.localScale =
                Vector3.one;

            GameObject modelInstance =
                PrefabUtility.InstantiatePrefab(
                    model,
                    previewScene
                ) as GameObject;

            if (modelInstance == null)
            {
                Debug.LogError(
                    $"Không instantiate được model: {model.name}"
                );

                return false;
            }

            modelInstance.name = model.name;

            modelInstance.transform.SetParent(
                root.transform,
                false
            );

            modelInstance.transform.localPosition =
                Vector3.zero;

            modelInstance.transform.localRotation =
                Quaternion.Euler(0f, modelRotationY, 0f);

            modelInstance.transform.localScale =
                Vector3.one;

            // No selected material: preserve every original material slot.
            if (mat != null)
            {
                ApplyMaterialToAllRenderers(
                    modelInstance,
                    mat
                );
            }

            PrefabUtility.SaveAsPrefabAsset(
                root,
                prefabPath,
                out bool saveSuccess
            );

            if (!saveSuccess)
            {
                Debug.LogError(
                    $"SaveAsPrefabAsset thất bại: {prefabPath}"
                );

                return false;
            }
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(
                previewScene
            );
        }

        AssetDatabase.ImportAsset(
            prefabPath,
            ImportAssetOptions.ForceUpdate
        );

        GameObject saved =
            AssetDatabase.LoadAssetAtPath<GameObject>(
                prefabPath
            );

        if (saved == null)
        {
            Debug.LogError(
                $"Không load lại được prefab: {prefabPath}"
            );

            return false;
        }

        PrefabAssetType type =
            PrefabUtility.GetPrefabAssetType(saved);

        if (type != PrefabAssetType.Regular)
        {
            Debug.LogError(
                $"Prefab không phải Regular: {prefabPath} | Type = {type}"
            );

            return false;
        }

        Debug.Log(
            $"OK REGULAR: {prefabPath}"
        );

        return true;
    }

    private static string GetColorName(
        string materialName
    )
    {
        if (string.IsNullOrEmpty(materialName))
        {
            return "Material";
        }

        int lastUnderscore =
            materialName.LastIndexOf('_');

        if (
            lastUnderscore >= 0 &&
            lastUnderscore < materialName.Length - 1
        )
        {
            return materialName.Substring(
                lastUnderscore + 1
            );
        }

        return materialName;
    }

    private static void ApplyMaterialToAllRenderers(
        GameObject root,
        Material mat
    )
    {
        Renderer[] renderers =
            root.GetComponentsInChildren<Renderer>(true);

        foreach (Renderer renderer in renderers)
        {
            Material[] slots =
                renderer.sharedMaterials;

            if (
                slots == null ||
                slots.Length == 0
            )
            {
                renderer.sharedMaterial = mat;
                continue;
            }

            for (int i = 0; i < slots.Length; i++)
            {
                slots[i] = mat;
            }

            renderer.sharedMaterials = slots;
        }
    }

    // =========================================================
    // UI HELPER
    // =========================================================

    private static void DrawSeparator()
    {
        Rect rect =
            EditorGUILayout.GetControlRect(
                false,
                1
            );

        EditorGUI.DrawRect(
            rect,
            new Color(
                0.4f,
                0.4f,
                0.4f,
                1f
            )
        );
    }
}
