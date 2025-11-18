// Assets/Scripts/FullLevelGenerator.cs
// Single-file generator: FullLevelGenerator + EnvironmentGenerator + SwayAnimation
// - Editor & runtime generation
// - Mountain walls placed outside road
// - Pickup hover + surface alignment
// - Coin patterns: Single, Line, Arc
// - Enemies spawn on Left/Right/Both
// - Environment generation (under & outer) integrated
// - Editor persistence (Undo & PrefabUtility)
// - Overlap and spacing checks, and skip very tall prefabs

using System;
using System.Collections.Generic;
using UnityEngine;
using PathCreation;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

#region FullLevelGenerator
[DisallowMultipleComponent]
[ExecuteAlways]
public class FullLevelGenerator : MonoBehaviour
{
    public enum Difficulty { Low, Medium, High }
    public enum EnemySide { Both, Left, Right }
    public enum CoinPattern { Single, Line, Arc }

    [Header("Path / Road")]
    public PathCreator pathCreator;
    [Tooltip("Full road width in world units")]
    public float roadWidth = 2.5f;

    [Header("Behavior")]
    public bool autoGenerateInPlay = true;
    public bool debugMode = false;
    public bool forceSpawn = false;
    public bool preserveEditorGenerated = true;

    [Header("Prefab Pools")]
    public List<GameObject> mountainLeftPrefabs = new List<GameObject>();
    public List<GameObject> mountainRightPrefabs = new List<GameObject>();
    public List<GameObject> obstaclePrefabs = new List<GameObject>();
    public List<GameObject> pickupPrefabs = new List<GameObject>();
    public List<GameObject> decorPrefabs = new List<GameObject>();
    public GameObject movingObstaclePrefab;
    public List<GameObject> gatePrefabs = new List<GameObject>();
    public GameObject checkpointPrefab;

    [Header("Spawn Tuning")]
    public int segmentsCount = 0;
    public float segmentLength = 6f;
    public float segmentRandOffset = 1.2f;
    public float minSpacing = 1.2f;
    [Range(0f, 1f)] public float lateralRange = 0.9f;
    public bool usePhysicsOverlapCheck = true;
    public LayerMask overlapLayers = ~0;

    [Header("Prefab radii fallback")]
    public float obstacleSpawnRadius = 1f;
    public float pickupSpawnRadius = 0.4f;
    public float decorSpawnRadius = 0.6f;
    public float minCenterClearance = 0.6f;

    [Header("Obstacle options")]
    public bool placeObstaclesOnRoad = false;
    public float maxObstacleHeight = 8f;

    [Header("Pickup hover/align")]
    public float pickupHoverHeight = 0.12f;
    public bool alignPickupToSurface = true;

    [Header("Mountains / fallback walls")]
    public bool autoGenerateFallbackBoxWalls = false;
    public float wallHeight = 2f;
    public float wallThickness = 0.3f;
    public float wallSegmentLength = 2f;
    public float wallSegmentOverlap = 0.2f;
    public string wallTag = "RoadWall";

    [Header("Moving obstacles")]
    public float movingDistance = 2f;
    public float movingSpeed = 1.5f;
    public int movingCount = 4;

    [Header("Difficulty")]
    public Difficulty difficulty = Difficulty.Medium;

    [Header("Enemy & coin behavior")]
    public EnemySide enemySide = EnemySide.Both;
    public CoinPattern coinPattern = CoinPattern.Single;
    public float coinLineLength = 6f;
    public float coinSpacing = 0.6f;

    [Header("Environment integration (optional)")]
    public EnvironmentGenerator environmentGenerator; // optional: same GameObject allowed

    // internal
    List<Vector3> placedPositions = new List<Vector3>();
    List<float> placedRadii = new List<float>();

    Transform wallsParent, obstaclesParent, pickupsParent, movingParent, decorParent, checkpointsParent, gatesParent;
    const string parentPrefix = "Generated_";

    #region Unity lifecycle
    void Start()
    {
        if (Application.isPlaying && autoGenerateInPlay)
        {
            TryRuntimeLoadPrefabs();
            Invoke(nameof(GenerateAll), 0.05f);
        }
    }
    #endregion

    #region Public API
    public void GenerateAll()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying) { GenerateAllInEditor(); return; }
#endif
        GenerateAllRuntime();
    }

    public void ClearAll()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying) { ClearGeneratedInEditor(); return; }
#endif
        ClearGeneratedRuntime();
    }
    #endregion

    #region Validation & parents
    bool ValidatePath()
    {
        if (pathCreator == null)
        {
            Debug.LogError("[FullLevelGenerator] Assign PathCreator.");
            return false;
        }
        var vPath = pathCreator.path;
        if (vPath == null || vPath.length <= 0f)
        {
            Debug.LogError("[FullLevelGenerator] Path invalid or length=0.");
            return false;
        }
        return true;
    }

    void PrepareParents()
    {
        wallsParent = FindOrCreate(parentPrefix + "RoadWalls");
        obstaclesParent = FindOrCreate(parentPrefix + "Obstacles");
        pickupsParent = FindOrCreate(parentPrefix + "Pickups");
        movingParent = FindOrCreate(parentPrefix + "Moving");
        decorParent = FindOrCreate(parentPrefix + "Decor");
        checkpointsParent = FindOrCreate(parentPrefix + "Checkpoints");
        gatesParent = FindOrCreate(parentPrefix + "Gates");
    }

    Transform FindOrCreate(string name)
    {
        var t = transform.Find(name);
        if (t == null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.transform;
        }
        return t;
    }
    #endregion

    #region Clear helpers
    void ClearGeneratedRuntime()
    {
        ClearChildrenRuntime(wallsParent);
        ClearChildrenRuntime(obstaclesParent);
        ClearChildrenRuntime(pickupsParent);
        ClearChildrenRuntime(movingParent);
        ClearChildrenRuntime(decorParent);
        ClearChildrenRuntime(checkpointsParent);
        ClearChildrenRuntime(gatesParent);
        placedPositions.Clear();
        placedRadii.Clear();
    }

    void ClearChildrenRuntime(Transform parent)
    {
        if (parent == null) return;
        for (int i = parent.childCount - 1; i >= 0; i--) Destroy(parent.GetChild(i).gameObject);
    }
    #endregion

    #region Editor generation
#if UNITY_EDITOR
    [ContextMenu("Generate In Editor")]
    public void GenerateAllInEditor()
    {
        if (Application.isPlaying) { Debug.LogWarning("[FullLevelGenerator] Editor generation should be in Edit mode."); return; }
        if (!ValidatePath()) return;

        PrepareParents();
        ClearGeneratedEditor();

        placedPositions.Clear();
        placedRadii.Clear();

        if ((mountainLeftPrefabs.Count > 0) || (mountainRightPrefabs.Count > 0))
            GenerateMountainWallsEditor();
        else if (autoGenerateFallbackBoxWalls)
            GenerateFallbackBoxWallsEditor();

        GenerateGatesEditor();
        GenerateSpawnablesEditor();
        GenerateMovingObstaclesEditor();
        GenerateCheckpointsEditor();

        // environment generator integration
        if (environmentGenerator != null)
        {
            environmentGenerator.pathCreator = this.pathCreator;
            environmentGenerator.roadWidth = this.roadWidth;
            environmentGenerator.Generate();
        }

        EditorSceneManager.MarkSceneDirty(gameObject.scene);
        if (debugMode) Debug.Log("[FullLevelGenerator] Editor generation complete.");
    }

    [ContextMenu("Clear Generated In Editor")]
    public void ClearGeneratedInEditor()
    {
        if (Application.isPlaying) { Debug.LogWarning("[FullLevelGenerator] Use ClearAll in Play mode."); return; }
        PrepareParents();
        ClearGeneratedEditor();
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }

    void ClearGeneratedEditor()
    {
        ClearChildrenEditor(wallsParent);
        ClearChildrenEditor(obstaclesParent);
        ClearChildrenEditor(pickupsParent);
        ClearChildrenEditor(movingParent);
        ClearChildrenEditor(decorParent);
        ClearChildrenEditor(checkpointsParent);
        ClearChildrenEditor(gatesParent);
        placedPositions.Clear();
        placedRadii.Clear();
    }

    void ClearChildrenEditor(Transform parent)
    {
        if (parent == null) return;
        for (int i = parent.childCount - 1; i >= 0; i--) Undo.DestroyObjectImmediate(parent.GetChild(i).gameObject);
    }
#endif
    #endregion

    #region Runtime generation
    void GenerateAllRuntime()
    {
        if (!Application.isPlaying) { Debug.Log("[FullLevelGenerator] Runtime generate skipped (not in Play mode)."); return; }
        if (!ValidatePath()) return;

        PrepareParents();

        if (preserveEditorGenerated && HasGeneratedChildren())
        {
            if (debugMode) Debug.Log("[FullLevelGenerator] Preserving editor-generated objects; caching them for overlap checks.");
            CacheExistingPlacedObjects();
            return;
        }

        ClearGeneratedRuntime();
        placedPositions.Clear();
        placedRadii.Clear();

        if ((mountainLeftPrefabs.Count > 0) || (mountainRightPrefabs.Count > 0))
            GenerateMountainWallsRuntime();
        else if (autoGenerateFallbackBoxWalls)
            GenerateFallbackBoxWallsRuntime();

        GenerateGatesRuntime();
        GenerateSpawnablesRuntime();
        GenerateMovingObstaclesRuntime();
        GenerateCheckpointsRuntime();

        // environment generator integration
        if (environmentGenerator != null)
        {
            environmentGenerator.pathCreator = this.pathCreator;
            environmentGenerator.roadWidth = this.roadWidth;
            environmentGenerator.Generate();
        }

        if (debugMode) Debug.Log("[FullLevelGenerator] Runtime generation complete.");
    }
    #endregion

    #region Cache existing editor-generated objects
    bool HasGeneratedChildren()
    {
        if (wallsParent != null && wallsParent.childCount > 0) return true;
        if (obstaclesParent != null && obstaclesParent.childCount > 0) return true;
        if (pickupsParent != null && pickupsParent.childCount > 0) return true;
        if (movingParent != null && movingParent.childCount > 0) return true;
        if (decorParent != null && decorParent.childCount > 0) return true;
        if (checkpointsParent != null && checkpointsParent.childCount > 0) return true;
        if (gatesParent != null && gatesParent.childCount > 0) return true;
        return false;
    }

    void CacheExistingPlacedObjects()
    {
        placedPositions.Clear();
        placedRadii.Clear();

        Action<Transform> scan = (parent) =>
        {
            if (parent == null) return;
            foreach (Transform c in parent)
            {
                placedPositions.Add(c.position);
                float r = minSpacing * 0.5f;
                var rends = c.GetComponentsInChildren<Renderer>();
                if (rends != null && rends.Length > 0)
                {
                    Bounds b = rends[0].bounds;
                    for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                    r = Mathf.Max(b.extents.x, b.extents.z);
                    if (r < 0.01f) r = minSpacing * 0.5f;
                }
                placedRadii.Add(r);
            }
        };

        scan(wallsParent); scan(obstaclesParent); scan(pickupsParent); scan(movingParent); scan(decorParent); scan(checkpointsParent); scan(gatesParent);

        if (debugMode) Debug.Log($"[FullLevelGenerator] Cached {placedPositions.Count} editor objects.");
    }
    #endregion

    #region Mountain & fallback walls
    void GenerateMountainWallsRuntime()
    {
        var vPath = pathCreator.path;
        float pathLen = vPath.length;
        float halfW = roadWidth * 0.5f;
        float step = Mathf.Max(0.5f, wallSegmentLength);

        for (float d = 0f; d <= pathLen; d += step)
        {
            Vector3 center = vPath.GetPointAtDistance(d);
            Vector3 tangent = vPath.GetDirectionAtDistance(d).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;

            // Left
            if (mountainLeftPrefabs.Count > 0)
            {
                var pf = mountainLeftPrefabs[UnityEngine.Random.Range(0, mountainLeftPrefabs.Count)];
                float r = GetPrefabRadius(pf, 0.5f);
                float lateral = halfW + r + 0.12f;
                Vector3 pos = center - right * lateral;
                pos.y = GetGroundYAt(pos);
                Quaternion rot = Quaternion.LookRotation(tangent, Vector3.up);
                var go = Instantiate(pf, pos, rot, wallsParent);
                go.transform.Rotate(0f, UnityEngine.Random.Range(-10f, 10f), 0f);
            }
            // Right
            if (mountainRightPrefabs.Count > 0)
            {
                var pf = mountainRightPrefabs[UnityEngine.Random.Range(0, mountainRightPrefabs.Count)];
                float r = GetPrefabRadius(pf, 0.5f);
                float lateral = halfW + r + 0.12f;
                Vector3 pos = center + right * lateral;
                pos.y = GetGroundYAt(pos);
                Quaternion rot = Quaternion.LookRotation(tangent, Vector3.up);
                var go = Instantiate(pf, pos, rot, wallsParent);
                go.transform.Rotate(0f, UnityEngine.Random.Range(-10f, 10f), 0f);
            }
        }
    }

#if UNITY_EDITOR
    void GenerateMountainWallsEditor()
    {
        var vPath = pathCreator.path;
        float pathLen = vPath.length;
        float halfW = roadWidth * 0.5f;
        float step = Mathf.Max(0.5f, wallSegmentLength);

        for (float d = 0f; d <= pathLen; d += step)
        {
            Vector3 center = vPath.GetPointAtDistance(d);
            Vector3 tangent = vPath.GetDirectionAtDistance(d).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;

            if (mountainLeftPrefabs.Count > 0)
            {
                var pf = mountainLeftPrefabs[UnityEngine.Random.Range(0, mountainLeftPrefabs.Count)];
                float r = GetPrefabRadius(pf, 0.5f);
                float lateral = halfW + r + 0.12f;
                Vector3 pos = center - right * lateral;
                pos.y = GetGroundYAt(pos);
                Quaternion rot = Quaternion.LookRotation(tangent, Vector3.up);
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(pf, wallsParent.gameObject.scene);
                inst.transform.SetParent(wallsParent, false);
                inst.transform.position = pos;
                inst.transform.rotation = rot;
                inst.transform.Rotate(0f, UnityEngine.Random.Range(-10f, 10f), 0f);
                Undo.RegisterCreatedObjectUndo(inst, "Instantiate mountain left");
            }

            if (mountainRightPrefabs.Count > 0)
            {
                var pf = mountainRightPrefabs[UnityEngine.Random.Range(0, mountainRightPrefabs.Count)];
                float r = GetPrefabRadius(pf, 0.5f);
                float lateral = halfW + r + 0.12f;
                Vector3 pos = center + right * lateral;
                pos.y = GetGroundYAt(pos);
                Quaternion rot = Quaternion.LookRotation(tangent, Vector3.up);
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(pf, wallsParent.gameObject.scene);
                inst.transform.SetParent(wallsParent, false);
                inst.transform.position = pos;
                inst.transform.rotation = rot;
                inst.transform.Rotate(0f, UnityEngine.Random.Range(-10f, 10f), 0f);
                Undo.RegisterCreatedObjectUndo(inst, "Instantiate mountain right");
            }
        }
    }
#endif

    void GenerateFallbackBoxWallsRuntime()
    {
        var vPath = pathCreator.path;
        float pathLen = vPath.length;
        float halfW = roadWidth * 0.5f;
        float step = Mathf.Max(0.5f, wallSegmentLength);

        for (float d = 0f; d <= pathLen; d += step)
        {
            Vector3 center = vPath.GetPointAtDistance(d);
            Vector3 tangent = vPath.GetDirectionAtDistance(d).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;
            float pieceLength = step + wallSegmentOverlap;
            float remaining = pathLen - d;
            if (remaining < step) pieceLength = remaining + wallSegmentOverlap;
            CreateBoxWallPieceRuntime(center, tangent, right, -1, halfW, pieceLength);
            CreateBoxWallPieceRuntime(center, tangent, right, +1, halfW, pieceLength);
        }
    }

#if UNITY_EDITOR
    void GenerateFallbackBoxWallsEditor()
    {
        var vPath = pathCreator.path;
        float pathLen = vPath.length;
        float halfW = roadWidth * 0.5f;
        float step = Mathf.Max(0.5f, wallSegmentLength);

        for (float d = 0f; d <= pathLen; d += step)
        {
            Vector3 center = vPath.GetPointAtDistance(d);
            Vector3 tangent = vPath.GetDirectionAtDistance(d).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;
            float pieceLength = step + wallSegmentOverlap;
            float remaining = pathLen - d;
            if (remaining < step) pieceLength = remaining + wallSegmentOverlap;
            CreateBoxWallPieceEditor(center, tangent, right, -1, halfW, pieceLength);
            CreateBoxWallPieceEditor(center, tangent, right, +1, halfW, pieceLength);
        }
    }
#endif

    void CreateBoxWallPieceRuntime(Vector3 center, Vector3 tangent, Vector3 right, int sideSign, float halfWidth, float pieceLength)
    {
        float lateralOffset = (halfWidth + (wallThickness * 0.5f)) * sideSign;
        Vector3 pos = center + right * lateralOffset;
        pos.y += wallHeight * 0.5f;
        Quaternion rot = Quaternion.LookRotation(tangent, Vector3.up);
        GameObject piece = new GameObject($"Wall_{(sideSign < 0 ? "L" : "R")}_{(wallsParent.childCount)}");
        piece.transform.SetParent(wallsParent, false);
        piece.transform.position = pos;
        piece.transform.rotation = rot;
        BoxCollider box = piece.AddComponent<BoxCollider>();
        box.size = new Vector3(wallThickness, wallHeight, Mathf.Max(0.01f, pieceLength));
        try { if (!string.IsNullOrEmpty(wallTag)) piece.tag = wallTag; } catch { }
    }

#if UNITY_EDITOR
    void CreateBoxWallPieceEditor(Vector3 center, Vector3 tangent, Vector3 right, int sideSign, float halfWidth, float pieceLength)
    {
        float lateralOffset = (halfWidth + (wallThickness * 0.5f)) * sideSign;
        Vector3 pos = center + right * lateralOffset;
        pos.y += wallHeight * 0.5f;
        Quaternion rot = Quaternion.LookRotation(tangent, Vector3.up);
        GameObject piece = new GameObject($"Wall_{(sideSign < 0 ? "L" : "R")}_{(wallsParent.childCount)}");
        piece.transform.SetParent(wallsParent, false);
        piece.transform.position = pos;
        piece.transform.rotation = rot;
        BoxCollider box = piece.AddComponent<BoxCollider>();
        box.size = new Vector3(wallThickness, wallHeight, Mathf.Max(0.01f, pieceLength));
        try { if (!string.IsNullOrEmpty(wallTag)) piece.tag = wallTag; } catch { }
        Undo.RegisterCreatedObjectUndo(piece, "Create wall piece");
    }
#endif
    #endregion

    #region Gates
    void GenerateGatesRuntime()
    {
        var vPath = pathCreator.path;
        if (gatePrefabs == null || gatePrefabs.Count == 0) return;
        var p0 = vPath.GetPointAtDistance(0f);
        var t0 = vPath.GetDirectionAtDistance(0f).normalized;
        var g0 = Instantiate(gatePrefabs[UnityEngine.Random.Range(0, gatePrefabs.Count)], p0, Quaternion.LookRotation(t0, Vector3.up), gatesParent);
        g0.name = "Gate_Start";
        var p1 = vPath.GetPointAtDistance(vPath.length);
        var t1 = vPath.GetDirectionAtDistance(vPath.length).normalized;
        var g1 = Instantiate(gatePrefabs[UnityEngine.Random.Range(0, gatePrefabs.Count)], p1, Quaternion.LookRotation(t1, Vector3.up), gatesParent);
        g1.name = "Gate_End";
    }

#if UNITY_EDITOR
    void GenerateGatesEditor()
    {
        var vPath = pathCreator.path;
        if (gatePrefabs == null || gatePrefabs.Count == 0) return;
        var p0 = vPath.GetPointAtDistance(0f);
        var t0 = vPath.GetDirectionAtDistance(0f).normalized;
        var inst0 = (GameObject)PrefabUtility.InstantiatePrefab(gatePrefabs[UnityEngine.Random.Range(0, gatePrefabs.Count)], gatesParent.gameObject.scene);
        inst0.transform.SetParent(gatesParent, false);
        inst0.transform.position = p0;
        inst0.transform.rotation = Quaternion.LookRotation(t0, Vector3.up);
        inst0.name = "Gate_Start";
        Undo.RegisterCreatedObjectUndo(inst0, "Create gate start");

        var p1 = vPath.GetPointAtDistance(vPath.length);
        var t1 = vPath.GetDirectionAtDistance(vPath.length).normalized;
        var inst1 = (GameObject)PrefabUtility.InstantiatePrefab(gatePrefabs[UnityEngine.Random.Range(0, gatePrefabs.Count)], gatesParent.gameObject.scene);
        inst1.transform.SetParent(gatesParent, false);
        inst1.transform.position = p1;
        inst1.transform.rotation = Quaternion.LookRotation(t1, Vector3.up);
        inst1.name = "Gate_End";
        Undo.RegisterCreatedObjectUndo(inst1, "Create gate end");
    }
#endif
    #endregion

    #region Spawnables: runtime & editor (with pickup fixes, coin patterns, enemy side)
    void GenerateSpawnablesRuntime()
    {
        var vPath = pathCreator.path;
        float totalLen = vPath.length;
        int steps = segmentsCount > 0 ? segmentsCount : Mathf.Max(1, Mathf.FloorToInt(totalLen / Mathf.Max(0.0001f, segmentLength)));

        float obsDensity = GetObstacleDensity();
        float pickDensity = GetPickupDensity();
        float decorDensity = GetDecorDensity();

        for (int i = 0; i < steps; i++)
        {
            float baseDist = segmentsCount > 0 ? (i / (float)steps) * totalLen : i * segmentLength;
            float randOff = UnityEngine.Random.Range(-segmentRandOffset, segmentRandOffset);
            float segCenter = Mathf.Clamp(baseDist + randOff, 0f, totalLen);

            int attempts = 1 + Mathf.FloorToInt(1 + obsDensity * 2f);

            for (int a = 0; a < attempts; a++)
            {
                if (obstaclePrefabs.Count == 0) break;
                if (UnityEngine.Random.value <= obsDensity)
                {
                    float localOffset = UnityEngine.Random.Range(-segmentLength * 0.45f, segmentLength * 0.45f);
                    float dist = Mathf.Clamp(segCenter + localOffset, 0f, totalLen);
                    TrySpawnAtDistanceRuntime(dist, SpawnType.Obstacle);
                }
            }

            if (pickupPrefabs.Count > 0 && UnityEngine.Random.value <= pickDensity)
            {
                float localOffset = UnityEngine.Random.Range(-segmentLength * 0.45f, segmentLength * 0.45f);
                float dist = Mathf.Clamp(segCenter + localOffset, 0f, totalLen);

                // coin pattern
                SpawnCoinGroup(dist);
            }

            if (decorPrefabs.Count > 0 && UnityEngine.Random.value <= decorDensity)
            {
                float localOffset = UnityEngine.Random.Range(-segmentLength * 0.45f, segmentLength * 0.45f);
                float dist = Mathf.Clamp(segCenter + localOffset, 0f, totalLen);
                TrySpawnAtDistanceRuntime(dist, SpawnType.Decor);
            }
        }
        if (debugMode) Debug.Log($"[FullLevelGenerator] Spawnables finished. Placed {placedPositions.Count} items.");
    }

#if UNITY_EDITOR
    void GenerateSpawnablesEditor()
    {
        var vPath = pathCreator.path;
        float totalLen = vPath.length;
        int steps = segmentsCount > 0 ? segmentsCount : Mathf.Max(1, Mathf.FloorToInt(totalLen / Mathf.Max(0.0001f, segmentLength)));

        float obsDensity = GetObstacleDensity();
        float pickDensity = GetPickupDensity();
        float decorDensity = GetDecorDensity();

        for (int i = 0; i < steps; i++)
        {
            float baseDist = segmentsCount > 0 ? (i / (float)steps) * totalLen : i * segmentLength;
            float randOff = UnityEngine.Random.Range(-segmentRandOffset, segmentRandOffset);
            float segCenter = Mathf.Clamp(baseDist + randOff, 0f, totalLen);

            int attempts = 1 + Mathf.FloorToInt(1 + obsDensity * 2f);

            for (int a = 0; a < attempts; a++)
            {
                if (obstaclePrefabs.Count == 0) break;
                if (UnityEngine.Random.value <= obsDensity)
                {
                    float localOffset = UnityEngine.Random.Range(-segmentLength * 0.45f, segmentLength * 0.45f);
                    float dist = Mathf.Clamp(segCenter + localOffset, 0f, totalLen);
                    TrySpawnAtDistanceEditor(dist, SpawnType.Obstacle);
                }
            }

            if (pickupPrefabs.Count > 0 && UnityEngine.Random.value <= pickDensity)
            {
                float localOffset = UnityEngine.Random.Range(-segmentLength * 0.45f, segmentLength * 0.45f);
                float dist = Mathf.Clamp(segCenter + localOffset, 0f, totalLen);
                SpawnCoinGroupEditor(dist);
            }

            if (decorPrefabs.Count > 0 && UnityEngine.Random.value <= decorDensity)
            {
                float localOffset = UnityEngine.Random.Range(-segmentLength * 0.45f, segmentLength * 0.45f);
                float dist = Mathf.Clamp(segCenter + localOffset, 0f, totalLen);
                TrySpawnAtDistanceEditor(dist, SpawnType.Decor);
            }
        }
        if (debugMode) Debug.Log($"[FullLevelGenerator] Spawnables finished (editor). Placed {placedPositions.Count} items.");
    }
#endif

    enum SpawnType { Obstacle, Pickup, Decor }

    // coin group runtime
    void SpawnCoinGroup(float distanceAlongPath)
    {
        var vPath = pathCreator.path;
        if (coinPattern == CoinPattern.Single) { TrySpawnAtDistanceRuntime(distanceAlongPath, SpawnType.Pickup); return; }

        if (coinPattern == CoinPattern.Line)
        {
            int count = Mathf.Max(2, Mathf.FloorToInt(coinLineLength / coinSpacing));
            float start = -coinLineLength * 0.5f;
            for (int i = 0; i < count; i++)
            {
                float offset = start + i * coinSpacing;
                float dist = Mathf.Clamp(distanceAlongPath + offset, 0f, vPath.length);
                TrySpawnAtDistanceRuntime(dist, SpawnType.Pickup);
            }
            return;
        }

        if (coinPattern == CoinPattern.Arc)
        {
            int c = Mathf.Max(3, Mathf.FloorToInt(coinLineLength / coinSpacing));
            float radius = Mathf.Max(1.0f, coinLineLength * 0.25f);
            Vector3 center = vPath.GetPointAtDistance(distanceAlongPath);
            Vector3 tangent = vPath.GetDirectionAtDistance(distanceAlongPath).normalized;
            for (int i = 0; i < c; i++)
            {
                float ang = Mathf.Lerp(-60f, 60f, i / (float)(c - 1)) * Mathf.Deg2Rad;
                Vector3 dir = Quaternion.AngleAxis(Mathf.Rad2Deg * ang, Vector3.up) * tangent;
                Vector3 pos = center + dir * radius;
                float d = FindClosestDistanceAlong(vPath, pos);
                TrySpawnAtDistanceRuntime(d, SpawnType.Pickup);
            }
            return;
        }
    }

#if UNITY_EDITOR
    // coin group editor
    void SpawnCoinGroupEditor(float distanceAlongPath)
    {
        var vPath = pathCreator.path;
        if (coinPattern == CoinPattern.Single) { TrySpawnAtDistanceEditor(distanceAlongPath, SpawnType.Pickup); return; }

        if (coinPattern == CoinPattern.Line)
        {
            int count = Mathf.Max(2, Mathf.FloorToInt(coinLineLength / coinSpacing));
            float start = -coinLineLength * 0.5f;
            for (int i = 0; i < count; i++)
            {
                float offset = start + i * coinSpacing;
                float dist = Mathf.Clamp(distanceAlongPath + offset, 0f, vPath.length);
                TrySpawnAtDistanceEditor(dist, SpawnType.Pickup);
            }
            return;
        }

        if (coinPattern == CoinPattern.Arc)
        {
            int c = Mathf.Max(3, Mathf.FloorToInt(coinLineLength / coinSpacing));
            float radius = Mathf.Max(1.0f, coinLineLength * 0.25f);
            Vector3 center = vPath.GetPointAtDistance(distanceAlongPath);
            Vector3 tangent = vPath.GetDirectionAtDistance(distanceAlongPath).normalized;
            for (int i = 0; i < c; i++)
            {
                float ang = Mathf.Lerp(-60f, 60f, i / (float)(c - 1)) * Mathf.Deg2Rad;
                Vector3 dir = Quaternion.AngleAxis(Mathf.Rad2Deg * ang, Vector3.up) * tangent;
                Vector3 pos = center + dir * radius;
                float d = FindClosestDistanceAlong(vPath, pos);
                TrySpawnAtDistanceEditor(d, SpawnType.Pickup);
            }
            return;
        }
    }
#endif

    bool TrySpawnAtDistanceRuntime(float distanceAlongPath, SpawnType type)
    {
        var path = pathCreator.path;
        float d = Mathf.Clamp(distanceAlongPath, 0f, path.length);
        Vector3 center = path.GetPointAtDistance(d);
        Vector3 tangent = path.GetDirectionAtDistance(d).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;

        float halfW = roadWidth * 0.5f;
        float margin = 0.4f;
        float effectiveHalf = Mathf.Max(0.01f, halfW - margin);

        float lateralOffset = UnityEngine.Random.Range(-lateralRange, lateralRange) * effectiveHalf;
        float spawnRadius = pickupSpawnRadius;
        GameObject prefab = null;
        Transform parent = null;

        switch (type)
        {
            case SpawnType.Obstacle:
                if (obstaclePrefabs.Count == 0) return false;
                prefab = obstaclePrefabs[UnityEngine.Random.Range(0, obstaclePrefabs.Count)];
                parent = obstaclesParent;
                spawnRadius = GetPrefabRadius(prefab, obstacleSpawnRadius);
                break;
            case SpawnType.Pickup:
                if (pickupPrefabs.Count == 0) return false;
                prefab = pickupPrefabs[UnityEngine.Random.Range(0, pickupPrefabs.Count)];
                parent = pickupsParent;
                spawnRadius = GetPrefabRadius(prefab, pickupSpawnRadius);
                break;
            case SpawnType.Decor:
                if (decorPrefabs.Count == 0) return false;
                prefab = decorPrefabs[UnityEngine.Random.Range(0, decorPrefabs.Count)];
                parent = decorParent;
                spawnRadius = GetPrefabRadius(prefab, decorSpawnRadius);
                break;
        }

        if (prefab == null) return false;

        // Skip tall obstacles
        if (type == SpawnType.Obstacle)
        {
            float prefabHeight = GetPrefabBoundsHeight(prefab);
            if (prefabHeight > maxObstacleHeight) { if (debugMode) Debug.Log($"Skip obstacle '{prefab.name}' height {prefabHeight} > {maxObstacleHeight}"); return false; }
        }

        Vector3 spawnPos;
        Quaternion spawnRotation = Quaternion.LookRotation(tangent, Vector3.up);

        // If decor or forcing obstacles off-road => place outside road by radius
        if (type == SpawnType.Decor || (type == SpawnType.Obstacle && !placeObstaclesOnRoad))
        {
            // optionally consider enemySide for obstacle side
            float sideSign = (lateralOffset >= 0f) ? 1f : -1f;
            if (type == SpawnType.Obstacle && enemySide == EnemySide.Left) sideSign = -1f;
            if (type == SpawnType.Obstacle && enemySide == EnemySide.Right) sideSign = 1f;

            float desiredLateral = sideSign * (halfW + spawnRadius + 0.12f);
            spawnPos = center + right * desiredLateral;
        }
        else
        {
            // Normal on-road placement (lateralOffset inside road)
            // if enemySide forces side, override
            if (type == SpawnType.Obstacle && enemySide != EnemySide.Both)
            {
                float sign = (enemySide == EnemySide.Left) ? -1f : 1f;
                float desiredLateral = sign * Mathf.Clamp(Mathf.Abs(lateralOffset) * effectiveHalf, minCenterClearance, effectiveHalf);
                spawnPos = center + right * desiredLateral;
            }
            else
            {
                spawnPos = center + right * lateralOffset;
            }
        }

        // Pickup: align bottom with ground using prefab half-height + hover
        if (type == SpawnType.Pickup)
        {
            float groundY = GetGroundYAt(spawnPos);
            float halfH = GetPrefabHalfHeight(prefab);
            float desiredY = (halfH > 0.001f) ? (groundY + halfH + pickupHoverHeight) : (groundY + pickupHoverHeight);
            spawnPos.y = desiredY;

            if (alignPickupToSurface)
            {
                RaycastHit hit;
                Vector3 from = new Vector3(spawnPos.x, spawnPos.y + 1.5f + halfH, spawnPos.z);
                if (Physics.Raycast(from, Vector3.down, out hit, 5f))
                {
                    Quaternion alignRot = Quaternion.FromToRotation(Vector3.up, hit.normal);
                    spawnRotation = Quaternion.Lerp(Quaternion.LookRotation(tangent, Vector3.up), alignRot * Quaternion.LookRotation(tangent, Vector3.up), 0.6f);
                }
            }
        }
        else
        {
            spawnPos.y = GetGroundYAt(spawnPos) + 0.01f;
            spawnRotation = Quaternion.LookRotation(tangent, Vector3.up);
        }

        // Keep obstacles away from center lane
        if (type == SpawnType.Obstacle)
        {
            float lateralDist = Mathf.Abs(Vector3.Dot(spawnPos - center, right));
            if (lateralDist < minCenterClearance)
            {
                float sign = (lateralOffset >= 0f) ? 1f : -1f;
                if (enemySide == EnemySide.Left) sign = -1f;
                if (enemySide == EnemySide.Right) sign = 1f;
                float targetLat = Mathf.Clamp(sign * minCenterClearance, -effectiveHalf, effectiveHalf);
                spawnPos = center + right * targetLat;
                spawnPos.y = GetGroundYAt(spawnPos) + 0.01f;
            }
        }

        if (!forceSpawn && !CanPlaceAt(spawnPos, spawnRadius))
        {
            if (debugMode) Debug.Log($"Skip spawn spacing at {spawnPos} r={spawnRadius}");
            return false;
        }

        if (usePhysicsOverlapCheck && spawnRadius > 0.01f)
        {
            var hits = Physics.OverlapSphere(spawnPos, spawnRadius, overlapLayers, QueryTriggerInteraction.Ignore);
            if (hits != null && hits.Length > 0) { if (debugMode) Debug.Log($"Skip spawn overlap at {spawnPos} hits={hits.Length}"); return false; }
        }

        var go = Instantiate(prefab, spawnPos, spawnRotation, (type == SpawnType.Obstacle) ? obstaclesParent : (type == SpawnType.Pickup) ? pickupsParent : decorParent);
        go.transform.Rotate(Vector3.up, UnityEngine.Random.Range(-30f, 30f));
        placedPositions.Add(spawnPos);
        placedRadii.Add(spawnRadius);
        return true;
    }

#if UNITY_EDITOR
    bool TrySpawnAtDistanceEditor(float distanceAlongPath, SpawnType type)
    {
        var path = pathCreator.path;
        float d = Mathf.Clamp(distanceAlongPath, 0f, path.length);
        Vector3 center = path.GetPointAtDistance(d);
        Vector3 tangent = path.GetDirectionAtDistance(d).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;

        float halfW = roadWidth * 0.5f;
        float margin = 0.4f;
        float effectiveHalf = Mathf.Max(0.01f, halfW - margin);

        float lateralOffset = UnityEngine.Random.Range(-lateralRange, lateralRange) * effectiveHalf;
        GameObject prefab = null;
        Transform parent = null;

        switch (type)
        {
            case SpawnType.Obstacle:
                if (obstaclePrefabs.Count == 0) return false;
                prefab = obstaclePrefabs[UnityEngine.Random.Range(0, obstaclePrefabs.Count)];
                parent = obstaclesParent;
                break;
            case SpawnType.Pickup:
                if (pickupPrefabs.Count == 0) return false;
                prefab = pickupPrefabs[UnityEngine.Random.Range(0, pickupPrefabs.Count)];
                parent = pickupsParent;
                break;
            case SpawnType.Decor:
                if (decorPrefabs.Count == 0) return false;
                prefab = decorPrefabs[UnityEngine.Random.Range(0, decorPrefabs.Count)];
                parent = decorParent;
                break;
        }

        if (prefab == null) return false;

        float spawnRadius = GetPrefabRadius(prefab, (type == SpawnType.Obstacle) ? obstacleSpawnRadius : (type == SpawnType.Pickup) ? pickupSpawnRadius : decorSpawnRadius);

        // Skip tall obstacles in editor
        if (type == SpawnType.Obstacle)
        {
            float prefabHeight = GetPrefabBoundsHeight(prefab);
            if (prefabHeight > maxObstacleHeight) { if (debugMode) Debug.Log($"(Editor) Skip obstacle '{prefab.name}' height {prefabHeight} > {maxObstacleHeight}"); return false; }
        }

        Vector3 spawnPos;
        Quaternion spawnRotation = Quaternion.LookRotation(tangent, Vector3.up);

        if (type == SpawnType.Decor || (type == SpawnType.Obstacle && !placeObstaclesOnRoad))
        {
            float sideSign = (lateralOffset >= 0f) ? 1f : -1f;
            if (type == SpawnType.Obstacle && enemySide == EnemySide.Left) sideSign = -1f;
            if (type == SpawnType.Obstacle && enemySide == EnemySide.Right) sideSign = 1f;
            float desiredLateral = sideSign * (halfW + spawnRadius + 0.12f);
            spawnPos = center + right * desiredLateral;
        }
        else
        {
            if (type == SpawnType.Obstacle && enemySide != EnemySide.Both)
            {
                float sign = (enemySide == EnemySide.Left) ? -1f : 1f;
                float desiredLateral = sign * Mathf.Clamp(Mathf.Abs(lateralOffset) * effectiveHalf, minCenterClearance, effectiveHalf);
                spawnPos = center + right * desiredLateral;
            }
            else spawnPos = center + right * lateralOffset;
        }

        if (type == SpawnType.Pickup)
        {
            float groundY = GetGroundYAt(spawnPos);
            float halfH = GetPrefabHalfHeight(prefab);
            float desiredY = (halfH > 0.001f) ? (groundY + halfH + pickupHoverHeight) : (groundY + pickupHoverHeight);
            spawnPos.y = desiredY;

            if (alignPickupToSurface)
            {
                RaycastHit hit;
                Vector3 from = new Vector3(spawnPos.x, spawnPos.y + 1.5f + halfH, spawnPos.z);
                if (Physics.Raycast(from, Vector3.down, out hit, 5f))
                {
                    Quaternion alignRot = Quaternion.FromToRotation(Vector3.up, hit.normal);
                    spawnRotation = Quaternion.Lerp(Quaternion.LookRotation(tangent, Vector3.up), alignRot * Quaternion.LookRotation(tangent, Vector3.up), 0.6f);
                }
            }
        }
        else
        {
            spawnPos.y = GetGroundYAt(spawnPos) + 0.01f;
            spawnRotation = Quaternion.LookRotation(tangent, Vector3.up);
        }

        if (type == SpawnType.Obstacle)
        {
            float lateralDist = Mathf.Abs(Vector3.Dot(spawnPos - center, right));
            if (lateralDist < minCenterClearance)
            {
                float sign = (lateralOffset >= 0f) ? 1f : -1f;
                if (enemySide == EnemySide.Left) sign = -1f;
                if (enemySide == EnemySide.Right) sign = 1f;
                float targetLat = Mathf.Clamp(sign * minCenterClearance, -effectiveHalf, effectiveHalf);
                spawnPos = center + right * targetLat;
                spawnPos.y = GetGroundYAt(spawnPos) + 0.01f;
            }
        }

        if (!forceSpawn && !CanPlaceAt(spawnPos, spawnRadius))
        {
            if (debugMode) Debug.Log($"(Editor) Skip spawn spacing at {spawnPos} r={spawnRadius}");
            return false;
        }

        if (usePhysicsOverlapCheck && spawnRadius > 0.01f)
        {
            var hits = Physics.OverlapSphere(spawnPos, spawnRadius, overlapLayers, QueryTriggerInteraction.Ignore);
            if (hits != null && hits.Length > 0) { if (debugMode) Debug.Log($"(Editor) Skip spawn overlap at {spawnPos} hits={hits.Length}"); return false; }
        }

        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, (parent != null ? parent.gameObject.scene : prefab.scene));
        inst.transform.SetParent(parent, false);
        inst.transform.position = spawnPos;
        inst.transform.rotation = spawnRotation;
        inst.transform.Rotate(Vector3.up, UnityEngine.Random.Range(-30f, 30f));
        Undo.RegisterCreatedObjectUndo(inst, "Instantiate spawnable");

        placedPositions.Add(spawnPos);
        placedRadii.Add(spawnRadius);
        return true;
    }
#endif
    #endregion

    #region Moving obstacles & checkpoints
    void GenerateMovingObstaclesRuntime()
    {
        if (movingObstaclePrefab == null || pathCreator == null) return;
        var vPath = pathCreator.path;
        float totalLen = vPath.length; if (totalLen <= 0f) return;
        int steps = Mathf.Max(1, movingCount);
        for (int i = 0; i < steps; i++)
        {
            float t = (i + 0.5f) / steps;
            float dist = Mathf.Clamp(t * totalLen, 0f, totalLen);
            Vector3 center = vPath.GetPointAtDistance(dist);
            Vector3 tangent = vPath.GetDirectionAtDistance(dist).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;
            float halfW = roadWidth * 0.5f;
            float lateralOffset = UnityEngine.Random.Range(-lateralRange, lateralRange) * halfW * 0.6f;
            Vector3 pos = center + right * lateralOffset;
            pos.y = GetGroundYAt(pos) + 0.02f;
            GameObject go = Instantiate(movingObstaclePrefab, pos, Quaternion.LookRotation(tangent, Vector3.up), movingParent);
            var mover = go.GetComponent<SimpleMover>();
            if (mover == null) mover = go.AddComponent<SimpleMover>();
            mover.distance = movingDistance;
            mover.speed = movingSpeed;
            float r = GetPrefabRadius(movingObstaclePrefab, obstacleSpawnRadius);
            placedPositions.Add(pos); placedRadii.Add(r);
        }
    }

#if UNITY_EDITOR
    void GenerateMovingObstaclesEditor()
    {
        if (movingObstaclePrefab == null || pathCreator == null) return;
        var vPath = pathCreator.path;
        float totalLen = vPath.length; if (totalLen <= 0f) return;
        int steps = Mathf.Max(1, movingCount);
        for (int i = 0; i < steps; i++)
        {
            float t = (i + 0.5f) / steps;
            float dist = Mathf.Clamp(t * totalLen, 0f, totalLen);
            Vector3 center = vPath.GetPointAtDistance(dist);
            Vector3 tangent = vPath.GetDirectionAtDistance(dist).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;
            float halfW = roadWidth * 0.5f;
            float lateralOffset = UnityEngine.Random.Range(-lateralRange, lateralRange) * halfW * 0.6f;
            Vector3 pos = center + right * lateralOffset;
            pos.y = GetGroundYAt(pos) + 0.02f;
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(movingObstaclePrefab, movingParent.gameObject.scene);
            inst.transform.SetParent(movingParent, false);
            inst.transform.position = pos;
            inst.transform.rotation = Quaternion.LookRotation(tangent, Vector3.up);
            var mover = inst.GetComponent<SimpleMover>();
            if (mover == null) mover = inst.AddComponent<SimpleMover>();
            mover.distance = movingDistance; mover.speed = movingSpeed;
            Undo.RegisterCreatedObjectUndo(inst, "Create moving obstacle");
            float r = GetPrefabRadius(movingObstaclePrefab, obstacleSpawnRadius);
            placedPositions.Add(pos); placedRadii.Add(r);
        }
    }
#endif

    void GenerateCheckpointsRuntime()
    {
        if (checkpointPrefab == null || pathCreator == null) return;
        var vPath = pathCreator.path;
        float totalLen = vPath.length; if (totalLen <= 0f) return;
        int count = Mathf.Clamp(3, 1, 50);
        for (int i = 0; i < count; i++)
        {
            float t = (i + 1f) / (count + 1f);
            float dist = Mathf.Clamp01(t) * totalLen;
            Vector3 center = vPath.GetPointAtDistance(dist);
            Vector3 tangent = vPath.GetDirectionAtDistance(dist).normalized;
            Quaternion rot = Quaternion.LookRotation(tangent, Vector3.up);
            Vector3 pos = center + Vector3.up * 0.5f;
            GameObject go = Instantiate(checkpointPrefab, pos, rot, checkpointsParent);
            go.name = $"Checkpoint_{i + 1}";
            Collider col = go.GetComponent<Collider>();
            if (col == null) { BoxCollider bc = go.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.size = new Vector3(roadWidth * 0.8f, 2f, 1f); }
            else col.isTrigger = true;
        }
    }

#if UNITY_EDITOR
    void GenerateCheckpointsEditor()
    {
        if (checkpointPrefab == null || pathCreator == null) return;
        var vPath = pathCreator.path;
        float totalLen = vPath.length; if (totalLen <= 0f) return;
        int count = Mathf.Clamp(3, 1, 50);
        for (int i = 0; i < count; i++)
        {
            float t = (i + 1f) / (count + 1f);
            float dist = Mathf.Clamp01(t) * totalLen;
            Vector3 center = vPath.GetPointAtDistance(dist);
            Vector3 tangent = vPath.GetDirectionAtDistance(dist).normalized;
            Quaternion rot = Quaternion.LookRotation(tangent, Vector3.up);
            Vector3 pos = center + Vector3.up * 0.5f;
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(checkpointPrefab, checkpointsParent.gameObject.scene);
            inst.transform.SetParent(checkpointsParent, false);
            inst.transform.position = pos;
            inst.transform.rotation = rot;
            inst.name = $"Checkpoint_{i + 1}";
            var col = inst.GetComponent<Collider>();
            if (col == null) { var bc = inst.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.size = new Vector3(roadWidth * 0.8f, 2f, 1f); }
            else col.isTrigger = true;
            Undo.RegisterCreatedObjectUndo(inst, "Create checkpoint");
        }
    }
#endif
    #endregion

    #region Utilities: ground, bounds, radii, placement check, densities, runtime loader
    float GetGroundYAt(Vector3 pos)
    {
        RaycastHit hit;
        Vector3 from = new Vector3(pos.x, pos.y + 10f, pos.z);
        if (Physics.Raycast(from, Vector3.down, out hit, 50f)) return hit.point.y;
        if (pathCreator != null) { var vPath = pathCreator.path; float closest = FindClosestDistanceAlong(vPath, pos); return vPath.GetPointAtDistance(closest).y; }
        return pos.y;
    }

    float FindClosestDistanceAlong(VertexPath vPath, Vector3 worldPos)
    {
        float bestDistAlong = 0f;
        Vector3 bestPoint = vPath.GetPointAtDistance(0f);
        float bestSqr = (worldPos - bestPoint).sqrMagnitude;
        float step = Mathf.Max(0.5f, segmentLength);
        for (float d = 0; d <= vPath.length; d += step)
        {
            Vector3 p = vPath.GetPointAtDistance(d);
            float sq = (worldPos - p).sqrMagnitude;
            if (sq < bestSqr) { bestSqr = sq; bestPoint = p; bestDistAlong = d; }
        }
        float low = Mathf.Max(0f, bestDistAlong - step);
        float high = Mathf.Min(vPath.length, bestDistAlong + step);
        for (int i = 0; i < 6; i++)
        {
            float m1 = Mathf.Lerp(low, high, 0.33f);
            float m2 = Mathf.Lerp(low, high, 0.66f);
            Vector3 p1 = vPath.GetPointAtDistance(m1);
            Vector3 p2 = vPath.GetPointAtDistance(m2);
            float s1 = (worldPos - p1).sqrMagnitude; float s2 = (worldPos - p2).sqrMagnitude;
            if (s1 < s2) high = m2; else low = m1;
        }
        return Mathf.Clamp((low + high) * 0.5f, 0f, vPath.length);
    }

    float GetPrefabRadius(GameObject prefab, float fallback)
    {
        if (prefab == null) return fallback;
        var rends = prefab.GetComponentsInChildren<Renderer>();
        if (rends != null && rends.Length > 0)
        {
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            float rx = b.extents.x; float rz = b.extents.z; float r = Mathf.Max(rx, rz);
            if (r > 0.01f) return Mathf.Max(r, fallback * 0.5f);
        }
        return fallback;
    }

    float GetPrefabHalfHeight(GameObject prefab)
    {
        if (prefab == null) return 0f;
        var rends = prefab.GetComponentsInChildren<Renderer>();
        if (rends == null || rends.Length == 0) return 0f;
        Bounds b = rends[0].bounds; for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        return b.extents.y;
    }

    float GetPrefabBoundsHeight(GameObject prefab)
    {
        if (prefab == null) return 0f;
        var rends = prefab.GetComponentsInChildren<Renderer>(); if (rends == null || rends.Length == 0) return 0f;
        Bounds b = rends[0].bounds; for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        return b.size.y;
    }

    bool CanPlaceAt(Vector3 pos, float radius)
    {
        float padding = 0.12f;
        for (int i = 0; i < placedPositions.Count; i++)
        {
            float otherR = (i < placedRadii.Count) ? placedRadii[i] : minSpacing * 0.5f;
            float minAllowed = radius + otherR + padding;
            if ((placedPositions[i] - pos).sqrMagnitude < (minAllowed * minAllowed)) return false;
        }
        return true;
    }

    float GetObstacleDensity() { switch (difficulty) { case Difficulty.Low: return 0.25f; case Difficulty.Medium: return 0.55f; case Difficulty.High: return 0.85f; } return 0.55f; }
    float GetPickupDensity() { switch (difficulty) { case Difficulty.Low: return 0.6f; case Difficulty.Medium: return 0.4f; case Difficulty.High: return 0.25f; } return 0.4f; }
    float GetDecorDensity() { switch (difficulty) { case Difficulty.Low: return 0.5f; case Difficulty.Medium: return 0.7f; case Difficulty.High: return 0.9f; } return 0.7f; }

    void TryRuntimeLoadPrefabs()
    {
        if (mountainLeftPrefabs.Count > 0 && mountainRightPrefabs.Count > 0 && obstaclePrefabs.Count > 0) return;
        var loaded = Resources.LoadAll<GameObject>("PerFAbs");
        if (loaded == null || loaded.Length == 0) { if (debugMode) Debug.Log("[FullLevelGenerator] No prefabs in Resources/PerFAbs"); return; }
        foreach (var pf in loaded)
        {
            var name = (pf != null) ? pf.name.ToLower() : "";
            if (string.IsNullOrEmpty(name)) continue;
            if (name.Contains("coin") || name.Contains("pickup") || name.Contains("treasure")) { if (!pickupPrefabs.Contains(pf)) pickupPrefabs.Add(pf); continue; }
            if (name.Contains("enemy") || name.Contains("mob")) { if (!obstaclePrefabs.Contains(pf)) obstaclePrefabs.Add(pf); if (movingObstaclePrefab == null) movingObstaclePrefab = pf; continue; }
            if (name.Contains("cliff") || name.Contains("cliffedge") || name.Contains("ridge") || name.Contains("mountain")) { if (!mountainLeftPrefabs.Contains(pf)) mountainLeftPrefabs.Add(pf); if (!mountainRightPrefabs.Contains(pf)) mountainRightPrefabs.Add(pf); continue; }
            if (name.Contains("rock") || name.Contains("ledge") || name.Contains("smallrock")) { if (!obstaclePrefabs.Contains(pf)) obstaclePrefabs.Add(pf); continue; }
            if (!decorPrefabs.Contains(pf)) decorPrefabs.Add(pf);
        }
        if (debugMode) Debug.Log($"[FullLevelGenerator] Runtime loaded {loaded.Length} prefabs from Resources/PerFAbs");
    }
    #endregion
}
#endregion

#region EnvironmentGenerator (in same file)
[ExecuteAlways]
public class EnvironmentGenerator : MonoBehaviour
{
    [Header("Road Source")]
    public PathCreator pathCreator;
    public float roadWidth = 2.5f;

    [Header("Prefabs")]
    public List<GameObject> underRoadPrefabs = new List<GameObject>();
    public List<GameObject> outerPrefabs = new List<GameObject>();
    public List<GameObject> plantPrefabs = new List<GameObject>();

    [Header("Distribution")]
    public float spacing = 4f;
    public float outerDistance = 6f;
    public float underDepth = -2.5f;
    public int density = 1;

    [Header("Visibility / optimization")]
    public bool hideOuterObjects = false;
    public string hiddenLayerName = "HiddenEnvironment";
    public bool addSwayToPlants = true;

    Transform underParent, outerParent;

    void PrepareParents()
    {
        underParent = transform.Find("Env_Under") ?? new GameObject("Env_Under").transform;
        underParent.SetParent(transform, false);
        outerParent = transform.Find("Env_Outer") ?? new GameObject("Env_Outer").transform;
        outerParent.SetParent(transform, false);
    }

    [ContextMenu("Generate Environment")]
    public void Generate()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying) { GenerateEditor(); return; }
#endif
        GenerateRuntime();
    }

    public void Clear()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying) { ClearEditor(); return; }
#endif
        ClearRuntime();
    }

    void ClearRuntime()
    {
        if (underParent != null) { for (int i = underParent.childCount - 1; i >= 0; i--) DestroyImmediate(underParent.GetChild(i).gameObject); }
        if (outerParent != null) { for (int i = outerParent.childCount - 1; i >= 0; i--) DestroyImmediate(outerParent.GetChild(i).gameObject); }
    }

#if UNITY_EDITOR
    void ClearEditor()
    {
        if (underParent != null) { for (int i = underParent.childCount - 1; i >= 0; i--) Undo.DestroyObjectImmediate(underParent.GetChild(i).gameObject); }
        if (outerParent != null) { for (int i = outerParent.childCount - 1; i >= 0; i--) Undo.DestroyObjectImmediate(outerParent.GetChild(i).gameObject); }
    }
#endif

    void GenerateRuntime()
    {
        if (pathCreator == null) return;
        PrepareParents();
        ClearRuntime();

        var vPath = pathCreator.path;
        float len = vPath.length;
        for (float d = 0f; d <= len; d += spacing)
        {
            for (int i = 0; i < density; i++)
            {
                // under-road
                if (underRoadPrefabs.Count > 0 && UnityEngine.Random.value < 0.7f)
                {
                    var pf = underRoadPrefabs[UnityEngine.Random.Range(0, underRoadPrefabs.Count)];
                    Vector3 center = vPath.GetPointAtDistance(d);
                    Vector3 pos = center + Vector3.up * underDepth;
                    pos += new Vector3(UnityEngine.Random.Range(-outerDistance * 0.5f, outerDistance * 0.5f), 0f, UnityEngine.Random.Range(-outerDistance * 0.5f, outerDistance * 0.5f));
                    var go = Instantiate(pf, pos, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f), underParent);
                    go.transform.localScale *= 0.9f + UnityEngine.Random.value * 0.5f;
                }
                // outer side
                if (outerPrefabs.Count > 0 && UnityEngine.Random.value < 0.85f)
                {
                    var pf = outerPrefabs[UnityEngine.Random.Range(0, outerPrefabs.Count)];
                    Vector3 center = vPath.GetPointAtDistance(d);
                    Vector3 tangent = vPath.GetDirectionAtDistance(d).normalized;
                    Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;
                    Vector3 pos = center + right * (roadWidth * 0.5f + outerDistance * (0.5f + UnityEngine.Random.value));
                    pos.y = GetGroundY(pos) + UnityEngine.Random.Range(-0.4f, 0.2f);
                    var go = Instantiate(pf, pos, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f), outerParent);
                    if (hideOuterObjects) SetLayerRecursively(go, LayerMask.NameToLayer(hiddenLayerName));
                    if (go.GetComponent<SwayAnimation>() == null && addSwayToPlants && plantPrefabs.Contains(pf)) go.AddComponent<SwayAnimation>();
                }
            }
        }
    }

#if UNITY_EDITOR
    void GenerateEditor()
    {
        if (pathCreator == null) return;
        PrepareParents();
        ClearEditor();

        var vPath = pathCreator.path;
        float len = vPath.length;
        for (float d = 0f; d <= len; d += spacing)
        {
            for (int i = 0; i < density; i++)
            {
                if (underRoadPrefabs.Count > 0 && UnityEngine.Random.value < 0.7f)
                {
                    var pf = underRoadPrefabs[UnityEngine.Random.Range(0, underRoadPrefabs.Count)];
                    Vector3 center = vPath.GetPointAtDistance(d);
                    Vector3 pos = center + Vector3.up * underDepth;
                    pos += new Vector3(UnityEngine.Random.Range(-outerDistance * 0.5f, outerDistance * 0.5f), 0f, UnityEngine.Random.Range(-outerDistance * 0.5f, outerDistance * 0.5f));
                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(pf, underParent.gameObject.scene);
                    inst.transform.SetParent(underParent, false);
                    inst.transform.position = pos;
                    inst.transform.rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
                    inst.transform.localScale *= 0.9f + UnityEngine.Random.value * 0.5f;
                    Undo.RegisterCreatedObjectUndo(inst, "Create under env");
                }
                if (outerPrefabs.Count > 0 && UnityEngine.Random.value < 0.85f)
                {
                    var pf = outerPrefabs[UnityEngine.Random.Range(0, outerPrefabs.Count)];
                    Vector3 center = vPath.GetPointAtDistance(d);
                    Vector3 tangent = vPath.GetDirectionAtDistance(d).normalized;
                    Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;
                    Vector3 pos = center + right * (roadWidth * 0.5f + outerDistance * (0.5f + UnityEngine.Random.value));
                    pos.y = GetGroundY(pos) + UnityEngine.Random.Range(-0.4f, 0.2f);
                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(pf, outerParent.gameObject.scene) as GameObject;
                    inst.transform.SetParent(outerParent, false);
                    inst.transform.position = pos;
                    inst.transform.rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
                    if (hideOuterObjects) SetLayerRecursively(inst, LayerMask.NameToLayer(hiddenLayerName));
                    if (inst.GetComponent<SwayAnimation>() == null && addSwayToPlants && plantPrefabs.Contains(pf)) inst.AddComponent<SwayAnimation>();
                    Undo.RegisterCreatedObjectUndo(inst, "Create outer env");
                }
            }
        }
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }
#endif

    float GetGroundY(Vector3 pos)
    {
        RaycastHit hit;
        if (Physics.Raycast(pos + Vector3.up * 5f, Vector3.down, out hit, 50f)) return hit.point.y;
        if (pathCreator != null) return pathCreator.path.GetPointAtDistance(FindClosestDistanceAlong(pathCreator.path, pos)).y;
        return pos.y;
    }

    float FindClosestDistanceAlong(VertexPath vPath, Vector3 worldPos)
    {
        float best = 0f; float bestSqr = float.MaxValue;
        for (float d = 0f; d <= vPath.length; d += spacing)
        {
            var p = vPath.GetPointAtDistance(d);
            var sq = (p - worldPos).sqrMagnitude;
            if (sq < bestSqr) { bestSqr = sq; best = d; }
        }
        return best;
    }

    void SetLayerRecursively(GameObject go, int layer)
    {
        if (layer < 0) return;
        go.layer = layer;
        foreach (Transform t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
    }
}
#endregion

#region SwayAnimation (in same file)
[DisallowMultipleComponent]
public class SwayAnimation : MonoBehaviour
{
    public float frequency = 0.9f;
    public float amplitude = 4f;
    public float noiseScale = 0.6f;
    long seed;
    Vector3 baseEuler;

    void Awake()
    {
        seed = Mathf.Abs(gameObject.name.GetHashCode()) + (long)(transform.position.x * 100f) + (long)(transform.position.z * 10f);
        baseEuler = transform.localEulerAngles;
    }

    void Update()
    {
        float t = Time.time * frequency;
        float phase = (Mathf.PerlinNoise((seed & 0xffff) * 0.001f, t * noiseScale) - 0.5f) * 2f;
        float x = Mathf.Sin(t + seed * 0.01f) * amplitude * 0.6f * phase;
        float z = Mathf.Cos(t * 0.7f + seed * 0.02f) * amplitude * 0.4f * phase;
        transform.localEulerAngles = baseEuler + new Vector3(x, 0f, z);
    }
}
#endregion
