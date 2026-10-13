#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public static class RebuildTilemapPrefabs
{
    private const string MenuPath = "Tools/My Tool/Rebuild Tilemap Prefabs";
    private const string UndoName = "Rebuild Tilemap Prefabs";

    private sealed class Snapshot
    {
        public Tilemap Map;
        public TileChangeData[] Cells;
        public TileFlags[] Flags;
        public GameObject[] GeneratedObjects;
    }

    [MenuItem(MenuPath, false, 200)]
    private static void RebuildAll()
    {
        // SceneManager enumerates ordinary loaded scenes, excluding the Tile Palette
        // and Prefab Mode preview scenes. Include inactive Tilemaps in those scenes.
        var snapshots = new List<Snapshot>();
        for (int s = 0; s < SceneManager.sceneCount; s++)
        {
            Scene scene = SceneManager.GetSceneAt(s);
            if (!scene.isLoaded || EditorSceneManager.IsPreviewScene(scene))
                continue;

            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Tilemap map in root.GetComponentsInChildren<Tilemap>(true))
            {
                Snapshot snapshot = Capture(map);
                if (snapshot.Cells.Length > 0)
                    snapshots.Add(snapshot);
            }
        }

        if (snapshots.Count == 0)
        {
            Debug.Log("No painted Tilemaps found in the open scenes.");
            return;
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(UndoName);
        int cellCount = 0;

        try
        {
            // Capture every Tilemap before changing any of them.
            foreach (Snapshot snapshot in snapshots)
                Undo.RegisterCompleteObjectUndo(snapshot.Map, UndoName);

            for (int i = 0; i < snapshots.Count; i++)
            {
                Snapshot snapshot = snapshots[i];
                Tilemap map = snapshot.Map;
                EditorUtility.DisplayProgressBar(UndoName, map.name,
                    (float)i / snapshots.Count);

                // A refresh alone can leave an existing instantiated object intact.
                // Removing the cells forces the next placement to instantiate anew.
                map.ClearAllTiles();

                try
                {
                    // Only objects returned by GetInstantiatedObject are eligible.
                    // Never delete arbitrary child objects under the Tilemap.
                    foreach (GameObject generated in snapshot.GeneratedObjects)
                        if (generated != null)
                            UnityEngine.Object.DestroyImmediate(generated);
                }
                finally
                {
                    // Always restore the painted cells, even if cleanup fails.
                    Restore(snapshot);
                }

                PrefabUtility.RecordPrefabInstancePropertyModifications(map);
                EditorSceneManager.MarkSceneDirty(map.gameObject.scene);
                cellCount += snapshot.Cells.Length;
            }

            Undo.CollapseUndoOperations(undoGroup);
            SceneView.RepaintAll();
            EditorApplication.QueuePlayerLoopUpdate();
            Debug.Log($"Rebuilt {cellCount} cells across {snapshots.Count} Tilemaps. " +
                "Generated tile objects now use the current prefab assets. " +
                "Use Undo to restore painted Tilemap data.");
        }
        catch (Exception exception)
        {
            Undo.RevertAllDownToGroup(undoGroup);
            Debug.LogException(exception);
            Debug.LogError("Rebuild failed. Painted Tilemap data was rolled back.");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private static Snapshot Capture(Tilemap map)
    {
        var cells = new List<TileChangeData>();
        var flags = new List<TileFlags>();
        var generated = new HashSet<GameObject>();

        foreach (Vector3Int position in map.cellBounds.allPositionsWithin)
        {
            TileBase tile = map.GetTile(position);
            if (tile == null)
                continue;

            cells.Add(new TileChangeData(position, tile,
                map.GetColor(position), map.GetTransformMatrix(position)));
            flags.Add(map.GetTileFlags(position));
            GameObject instance = map.GetInstantiatedObject(position);
            if (instance != null)
                generated.Add(instance);
        }

        var objects = new GameObject[generated.Count];
        generated.CopyTo(objects);
        return new Snapshot
        {
            Map = map,
            Cells = cells.ToArray(),
            Flags = flags.ToArray(),
            GeneratedObjects = objects
        };
    }

    private static void Restore(Snapshot snapshot)
    {
        Tilemap map = snapshot.Map;
        map.SetTiles(snapshot.Cells, true);
        map.RefreshAllTiles();

        // Rule refreshes may reset color and transform. Restore the saved cell
        // values after rule evaluation, temporarily bypassing their lock flags.
        for (int i = 0; i < snapshot.Cells.Length; i++)
        {
            TileChangeData cell = snapshot.Cells[i];
            TileFlags flags = snapshot.Flags[i];
            map.SetTileFlags(cell.position,
                flags & ~(TileFlags.LockColor | TileFlags.LockTransform));
            map.SetColor(cell.position, cell.color);
            map.SetTransformMatrix(cell.position, cell.transform);
            map.SetTileFlags(cell.position, flags);
        }
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateRebuildAll()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode
            && !EditorApplication.isCompiling
            && !EditorApplication.isUpdating;
    }
}
#endif
