#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class PopulateGeneratorFromPerFabs : EditorWindow
{
    const string targetFolder = "Assets/Scenes/PerFAbs";
    FullLevelGenerator targetGenerator;
    bool autoAssignMoving = true;

    [MenuItem("Tools/LevelGen/Populate From PerFabs")]
    public static void ShowWindow() => GetWindow<PopulateGeneratorFromPerFabs>("Populate PerFabs");

    void OnGUI()
    {
        GUILayout.Label("Populate FullLevelGenerator from PerFabs folder", EditorStyles.boldLabel);
        targetGenerator = (FullLevelGenerator)EditorGUILayout.ObjectField("Generator", targetGenerator, typeof(FullLevelGenerator), true);
        autoAssignMoving = EditorGUILayout.ToggleLeft("Auto-assign first 'Enemy' prefab to movingObstaclePrefab", autoAssignMoving);
        GUILayout.Space(6);

        if (GUILayout.Button("Populate from " + targetFolder))
        {
            if (targetGenerator == null)
            {
                EditorUtility.DisplayDialog("Populate PerFabs", "Assign a FullLevelGenerator in the Generator field first.", "OK");
                return;
            }
            Populate();
        }

        if (GUILayout.Button("Open folder in Project"))
        {
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { targetFolder });
            if (guids.Length == 0) EditorUtility.DisplayDialog("Open folder", "No prefabs found in " + targetFolder, "OK");
            else
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                Object obj = AssetDatabase.LoadAssetAtPath<Object>(path);
                Selection.activeObject = obj;
                EditorGUIUtility.PingObject(obj);
            }
        }
    }

    void Populate()
    {
        if (!AssetDatabase.IsValidFolder(targetFolder))
        {
            EditorUtility.DisplayDialog("Populate PerFabs", $"Folder not found: {targetFolder}\nCreate the folder and put your prefabs there.", "OK");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { targetFolder });
        if (guids == null || guids.Length == 0)
        {
            EditorUtility.DisplayDialog("Populate PerFabs", $"No prefabs found in {targetFolder}. Make sure prefabs are in that folder.", "OK");
            return;
        }

        List<GameObject> obstacles = new List<GameObject>();
        List<GameObject> pickups = new List<GameObject>();
        List<GameObject> decor = new List<GameObject>();
        GameObject enemyPrefab = null;

        foreach (var g in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            string name = prefab.name.ToLower();
            if (name.Contains("coin") || name.Contains("treasure") || name.Contains("pickup"))
            {
                if (!pickups.Contains(prefab)) pickups.Add(prefab);
                continue;
            }
            if (name.Contains("enemy") || name.Contains("bat") || name.Contains("mob"))
            {
                if (enemyPrefab == null) enemyPrefab = prefab;
                if (!obstacles.Contains(prefab)) obstacles.Add(prefab);
                continue;
            }
            if (name.Contains("cliff") || name.Contains("cliffedge") || name.Contains("ridge") || name.Contains("rock") || name.Contains("ledge") || name.Contains("swamp") || name.Contains("smallrock"))
            {
                if (name.Contains("small") || name.Contains("floating") || name.Contains("edge") || name.Contains("ledge"))
                {
                    if (!decor.Contains(prefab)) decor.Add(prefab);
                }
                else
                {
                    if (!obstacles.Contains(prefab)) obstacles.Add(prefab);
                }
                continue;
            }
            if (!decor.Contains(prefab)) decor.Add(prefab);
        }

        Undo.RecordObject(targetGenerator, "Populate FullLevelGenerator prefabs");
        targetGenerator.obstaclePrefabs = obstacles;
        targetGenerator.pickupPrefabs = pickups;
        targetGenerator.decorPrefabs = decor;
        if (autoAssignMoving && enemyPrefab != null) targetGenerator.movingObstaclePrefab = enemyPrefab;
        EditorUtility.SetDirty(targetGenerator);
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog("Populate PerFabs", $"Assigned: obstacles={obstacles.Count}, pickups={pickups.Count}, decor={decor.Count}\nEnemy assigned: { (enemyPrefab!=null ? enemyPrefab.name : "none") }", "OK");
    }
}
#endif
