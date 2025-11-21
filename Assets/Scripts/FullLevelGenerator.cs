// Assets/Scripts/FullLevelGenerator.cs
// Full generator with pickup hover fix: pickups' bottoms sit on road surface using prefab bounds.
// Editor & runtime generation, preserve editor-generated objects, overlap checks, mountains off-road.

using System.Collections.Generic;
using UnityEngine;
using PathCreation;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[DisallowMultipleComponent]
[ExecuteAlways]
public class FullLevelGenerator : MonoBehaviour
{
    public enum Difficulty { Low, Medium, High }

    [Header("Path / Road")]
    public PathCreator pathCreator;
    [Tooltip("Full road width in world units")]
    public float roadWidth = 2.5f;

    [Header("Behavior")]
    [Tooltip("If true, generator runs automatically when entering Play mode")]
    public bool autoGenerateInPlay = true;
    [Tooltip("If true, debug logs appear")]
    public bool debugMode = true;
    [Tooltip("If true, ignore spacing checks for testing")]
    public bool forceSpawn = false;
    [Tooltip("If true, editor-generated objects (created with 'Generate In Editor') will be preserved when entering Play.")]
    public bool preserveEditorGenerated = true;

    [Header("Pickup hover")]
    [Tooltip("Vertical offset so pickups sit slightly above road surface (meters)")]
    public float pickupHoverHeight = 0.12f;
    [Tooltip("If true, pickups will be aligned to road normal and sit on top of the surface")]
    public bool alignPickupToSurface = true;

    [Header("Obstacle placement options")]
    [Tooltip("If false obstacles will NOT be placed on the road center — they will be forced to spawn outside the road like decor.")]
    public bool placeObstaclesOnRoad = false;
    [Tooltip("Maximum prefab height (world units) allowed for obstacles. Taller prefabs will be skipped.")]
    public float maxObstacleHeight = 8f;

    [Header("Prefab Pools (Inspector or runtime Resources)")]
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

    [Header("Per-type radii (meters) - used if prefab bounds can't be measured)")]
    public float obstacleSpawnRadius = 1f;
    public float pickupSpawnRadius = 0.4f;
    public float decorSpawnRadius = 0.6f;
    public float minCenterClearance = 0.6f;

    [Header("Mountains / Wall fallback")]
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

    // internal lists for overlap checks
    List<Vector3> placedPositions = new List<Vector3>();
    List<float> placedRadii = new List<float>();

    // parents
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

    #region Public entry points
    public void GenerateAll()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            GenerateAllInEditor();
            return;
        }
#endif
        GenerateAllRuntime();
    }

    public void ClearAll()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            ClearGeneratedInEditor();
            return;
        }
#endif
        ClearGeneratedRuntime();
    }
    #endregion

    #region Validation & parents
    bool ValidatePath()
    {
        if (pathCreator == null)
        {
            Debug.LogError("[FullLevelGenerator] Assign PathCreator (Road Creator).");
            return false;
        }

        var vPath = pathCreator.path;
        if (vPath == null || vPath.length <= 0f)
        {
            Debug.LogError("[FullLevelGenerator] PathCreator.path invalid or length=0.");
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
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            var c = parent.GetChild(i);
            Destroy(c.gameObject);
        }
    }
    #endregion

    #region Editor actions
#if UNITY_EDITOR
    [ContextMenu("Generate In Editor")]
    public void GenerateAllInEditor()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("[FullLevelGenerator] GenerateInEditor should be used only in Edit mode.");
            return;
        }

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

        EditorSceneManager.MarkSceneDirty(gameObject.scene);

        if (debugMode) Debug.Log("[FullLevelGenerator] Editor generation finished.");
    }

    [ContextMenu("Clear Generated In Editor")]
    public void ClearGeneratedInEditor()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("[FullLevelGenerator] Use ClearAll (runtime) in Play mode.");
            return;
        }
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
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            var c = parent.GetChild(i);
            Undo.DestroyObjectImmediate(c.gameObject);
        }
    }
#endif
    #endregion

    #region Runtime generation (respect preserve flag)
    void GenerateAllRuntime()
    {
        if (!Application.isPlaying)
        {
            Debug.Log("[FullLevelGenerator] Runtime generate skipped (not in Play mode).");
            return;
        }

        if (!ValidatePath()) return;

        PrepareParents();

        if (preserveEditorGenerated && HasGeneratedChildren())
        {
            if (debugMode) Debug.Log("[FullLevelGenerator] Preserving editor-generated objects. Caching them for overlap checks and skipping runtime regeneration.");
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

        if (debugMode) Debug.Log("[FullLevelGenerator] Runtime generation finished.");
    }
    #endregion

    #region Cache existing editor objects
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

        System.Action<Transform> scanParent = (parent) =>
        {
            if (parent == null) return;
            foreach (Transform child in parent)
            {
                Vector3 p = child.position;
                placedPositions.Add(p);

                float r = minSpacing * 0.5f;
                var rends = child.GetComponentsInChildren<Renderer>();
                if (rends != null && rends.Length > 0)
                {
                    Bounds b = rends[0].bounds;
                    for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                    float rx = b.extents.x;
                    float rz = b.extents.z;
                    r = Mathf.Max(rx, rz);
                    if (r < 0.01f) r = minSpacing * 0.5f;
                }
                placedRadii.Add(r);
            }
        };

        scanParent(wallsParent);
        scanParent(obstaclesParent);
        scanParent(pickupsParent);
        scanParent(movingParent);
        scanParent(decorParent);
        scanParent(checkpointsParent);
        scanParent(gatesParent);

        if (debugMode) Debug.Log($"[FullLevelGenerator] Cached {placedPositions.Count} existing generated objects for overlap checks.");
    }
    #endregion

    #region Mountain/Wall placement (runtime & editor)
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

            if (mountainLeftPrefabs.Count > 0)
            {
                var pf = mountainLeftPrefabs[Random.Range(0, mountainLeftPrefabs.Count)];
                float radius = GetPrefabRadius(pf, 0.5f);
                float lateral = halfW + radius + 0.12f;
                Vector3 pos = center - right * lateral;
                pos.y = GetGroundYAt(pos);
                Quaternion rot = Quaternion.LookRotation(tangent, Vector3.up);
                var go = Instantiate(pf, pos, rot, wallsParent);
                go.transform.Rotate(0f, Random.Range(-10f, 10f), 0f);
            }

            if (mountainRightPrefabs.Count > 0)
            {
                var pf = mountainRightPrefabs[Random.Range(0, mountainRightPrefabs.Count)];
                float radius = GetPrefabRadius(pf, 0.5f);
                float lateral = halfW + radius + 0.12f;
                Vector3 pos = center + right * lateral;
                pos.y = GetGroundYAt(pos);
                Quaternion rot = Quaternion.LookRotation(tangent, Vector3.up);
                var go = Instantiate(pf, pos, rot, wallsParent);
                go.transform.Rotate(0f, Random.Range(-10f, 10f), 0f);
            }
        }
        if (debugMode) Debug.Log("[FullLevelGenerator] Mountain walls placed (runtime).");
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
                var pf = mountainLeftPrefabs[Random.Range(0, mountainLeftPrefabs.Count)];
                float radius = GetPrefabRadius(pf, 0.5f);
                float lateral = halfW + radius + 0.12f;
                Vector3 pos = center - right * lateral;
                pos.y = GetGroundYAt(pos);
                Quaternion rot = Quaternion.LookRotation(tangent, Vector3.up);
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(pf, wallsParent.gameObject.scene);
                inst.transform.SetParent(wallsParent, false);
                inst.transform.position = pos;
                inst.transform.rotation = rot;
                inst.transform.Rotate(0f, Random.Range(-10f, 10f), 0f);
                Undo.RegisterCreatedObjectUndo(inst, "Instantiate mountain left");
            }

            if (mountainRightPrefabs.Count > 0)
            {
                var pf = mountainRightPrefabs[Random.Range(0, mountainRightPrefabs.Count)];
                float radius = GetPrefabRadius(pf, 0.5f);
                float lateral = halfW + radius + 0.12f;
                Vector3 pos = center + right * lateral;
                pos.y = GetGroundYAt(pos);
                Quaternion rot = Quaternion.LookRotation(tangent, Vector3.up);
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(pf, wallsParent.gameObject.scene);
                inst.transform.SetParent(wallsParent, false);
                inst.transform.position = pos;
                inst.transform.rotation = rot;
                inst.transform.Rotate(0f, Random.Range(-10f, 10f), 0f);
                Undo.RegisterCreatedObjectUndo(inst, "Instantiate mountain right");
            }
        }
        if (debugMode) Debug.Log("[FullLevelGenerator] Mountain walls placed (editor).");
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
        if (debugMode) Debug.Log("[FullLevelGenerator] Box walls placed (runtime).");
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
        if (debugMode) Debug.Log("[FullLevelGenerator] Box walls placed (editor).");
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

    #region Gates (runtime/editor)
    void GenerateGatesRuntime()
    {
        var vPath = pathCreator.path;
        if (gatePrefabs == null || gatePrefabs.Count == 0) return;
        var p0 = vPath.GetPointAtDistance(0f);
        var t0 = vPath.GetDirectionAtDistance(0f).normalized;
        var g0 = Instantiate(gatePrefabs[Random.Range(0, gatePrefabs.Count)], p0, Quaternion.LookRotation(t0, Vector3.up), gatesParent);
        g0.name = "Gate_Start";
        var p1 = vPath.GetPointAtDistance(vPath.length);
        var t1 = vPath.GetDirectionAtDistance(vPath.length).normalized;
        var g1 = Instantiate(gatePrefabs[Random.Range(0, gatePrefabs.Count)], p1, Quaternion.LookRotation(t1, Vector3.up), gatesParent);
        g1.name = "Gate_End";
    }

#if UNITY_EDITOR
    void GenerateGatesEditor()
    {
        var vPath = pathCreator.path;
        if (gatePrefabs == null || gatePrefabs.Count == 0) return;
        var p0 = vPath.GetPointAtDistance(0f);
        var t0 = vPath.GetDirectionAtDistance(0f).normalized;
        var inst0 = (GameObject)PrefabUtility.InstantiatePrefab(gatePrefabs[Random.Range(0, gatePrefabs.Count)], gatesParent.gameObject.scene);
        inst0.transform.SetParent(gatesParent, false);
        inst0.transform.position = p0;
        inst0.transform.rotation = Quaternion.LookRotation(t0, Vector3.up);
        inst0.name = "Gate_Start";
        Undo.RegisterCreatedObjectUndo(inst0, "Create gate start");

        var p1 = vPath.GetPointAtDistance(vPath.length);
        var t1 = vPath.GetDirectionAtDistance(vPath.length).normalized;
        var inst1 = (GameObject)PrefabUtility.InstantiatePrefab(gatePrefabs[Random.Range(0, gatePrefabs.Count)], gatesParent.gameObject.scene);
        inst1.transform.SetParent(gatesParent, false);
        inst1.transform.position = p1;
        inst1.transform.rotation = Quaternion.LookRotation(t1, Vector3.up);
        inst1.name = "Gate_End";
        Undo.RegisterCreatedObjectUndo(inst1, "Create gate end");
    }
#endif
    #endregion

    #region Spawnables (runtime/editor)
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
            float randOff = Random.Range(-segmentRandOffset, segmentRandOffset);
            float segCenter = Mathf.Clamp(baseDist + randOff, 0f, totalLen);

            int attempts = 1 + Mathf.FloorToInt(1 + obsDensity * 2f);

            for (int a = 0; a < attempts; a++)
            {
                if (obstaclePrefabs.Count == 0) break;
                if (Random.value <= obsDensity)
                {
                    float localOffset = Random.Range(-segmentLength * 0.45f, segmentLength * 0.45f);
                    float dist = Mathf.Clamp(segCenter + localOffset, 0f, totalLen);
                    TrySpawnAtDistanceRuntime(dist, SpawnType.Obstacle);
                }
            }

            if (pickupPrefabs.Count > 0 && Random.value <= pickDensity)
            {
                float localOffset = Random.Range(-segmentLength * 0.45f, segmentLength * 0.45f);
                float dist = Mathf.Clamp(segCenter + localOffset, 0f, totalLen);
                TrySpawnAtDistanceRuntime(dist, SpawnType.Pickup);
            }

            if (decorPrefabs.Count > 0 && Random.value <= decorDensity)
            {
                float localOffset = Random.Range(-segmentLength * 0.45f, segmentLength * 0.45f);
                float dist = Mathf.Clamp(segCenter + localOffset, 0f, totalLen);
                TrySpawnAtDistanceRuntime(dist, SpawnType.Decor);
            }
        }
        if (debugMode) Debug.Log($"[FullLevelGenerator] Spawnables done. Placed {placedPositions.Count} items.");
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
            float randOff = Random.Range(-segmentRandOffset, segmentRandOffset);
            float segCenter = Mathf.Clamp(baseDist + randOff, 0f, totalLen);

            int attempts = 1 + Mathf.FloorToInt(1 + obsDensity * 2f);

            for (int a = 0; a < attempts; a++)
            {
                if (obstaclePrefabs.Count == 0) break;
                if (Random.value <= obsDensity)
                {
                    float localOffset = Random.Range(-segmentLength * 0.45f, segmentLength * 0.45f);
                    float dist = Mathf.Clamp(segCenter + localOffset, 0f, totalLen);
                    TrySpawnAtDistanceEditor(dist, SpawnType.Obstacle);
                }
            }

            if (pickupPrefabs.Count > 0 && Random.value <= pickDensity)
            {
                float localOffset = Random.Range(-segmentLength * 0.45f, segmentLength * 0.45f);
                float dist = Mathf.Clamp(segCenter + localOffset, 0f, totalLen);
                TrySpawnAtDistanceEditor(dist, SpawnType.Pickup);
            }

            if (decorPrefabs.Count > 0 && Random.value <= decorDensity)
            {
                float localOffset = Random.Range(-segmentLength * 0.45f, segmentLength * 0.45f);
                float dist = Mathf.Clamp(segCenter + localOffset, 0f, totalLen);
                TrySpawnAtDistanceEditor(dist, SpawnType.Decor);
            }
        }
        if (debugMode) Debug.Log($"[FullLevelGenerator] Spawnables done (editor). Placed {placedPositions.Count} items.");
    }
#endif

    enum SpawnType { Obstacle, Pickup, Decor }

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

        float lateralOffset = Random.Range(-lateralRange, lateralRange) * effectiveHalf;
        float spawnRadius = pickupSpawnRadius;
        GameObject prefab = null;
        Transform parent = null;

        switch (type)
        {
            case SpawnType.Obstacle:
                if (obstaclePrefabs.Count == 0) return false;
                prefab = obstaclePrefabs[Random.Range(0, obstaclePrefabs.Count)];
                parent = obstaclesParent;
                spawnRadius = GetPrefabRadius(prefab, obstacleSpawnRadius);
                break;
            case SpawnType.Pickup:
                if (pickupPrefabs.Count == 0) return false;
                prefab = pickupPrefabs[Random.Range(0, pickupPrefabs.Count)];
                parent = pickupsParent;
                spawnRadius = GetPrefabRadius(prefab, pickupSpawnRadius);
                break;
            case SpawnType.Decor:
                if (decorPrefabs.Count == 0) return false;
                prefab = decorPrefabs[Random.Range(0, decorPrefabs.Count)];
                parent = decorParent;
                spawnRadius = GetPrefabRadius(prefab, decorSpawnRadius);
                break;
        }

        if (prefab == null) return false;

        // Skip very tall obstacle prefabs
        if (type == SpawnType.Obstacle)
        {
            float prefabHeight = GetPrefabBoundsHeight(prefab);
            if (prefabHeight > maxObstacleHeight)
            {
                if (debugMode) Debug.Log($"[FullLevelGenerator] Skip obstacle '{prefab.name}' height {prefabHeight} > maxObstacleHeight {maxObstacleHeight}");
                return false;
            }
        }

        Vector3 spawnPos;
        Quaternion spawnRotation = Quaternion.LookRotation(tangent, Vector3.up);

        if (type == SpawnType.Decor)
        {
            float lateralSign = (lateralOffset >= 0f) ? 1f : -1f;
            float desiredLateral = lateralSign * (halfW + spawnRadius + 0.12f);
            spawnPos = center + right * desiredLateral;
        }
        else if (type == SpawnType.Obstacle && !placeObstaclesOnRoad)
        {
            float lateralSign = (lateralOffset >= 0f) ? 1f : -1f;
            float desiredLateral = lateralSign * (halfW + spawnRadius + 0.12f);
            spawnPos = center + right * desiredLateral;
        }
        else
        {
            spawnPos = center + right * lateralOffset;
        }

        // PICKUP: align bottom with ground using prefab half-height + hover
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
                else
                {
                    spawnRotation = Quaternion.LookRotation(tangent, Vector3.up);
                }
            }
            else
            {
                spawnRotation = Quaternion.LookRotation(tangent, Vector3.up);
            }
        }
        else
        {
            spawnPos.y = GetGroundYAt(spawnPos) + 0.01f;
            spawnRotation = Quaternion.LookRotation(tangent, Vector3.up);
        }

        // keep obstacles away from center lane
        if (type == SpawnType.Obstacle)
        {
            float lateralDist = Mathf.Abs(Vector3.Dot(spawnPos - center, right));
            if (lateralDist < minCenterClearance)
            {
                float sign = (lateralOffset >= 0f) ? 1f : -1f;
                float targetLat = Mathf.Clamp(sign * minCenterClearance, -effectiveHalf, effectiveHalf);
                spawnPos = center + right * targetLat;
                spawnPos.y = GetGroundYAt(spawnPos) + 0.01f;
            }
        }

        if (!forceSpawn && !CanPlaceAt(spawnPos, spawnRadius))
        {
            if (debugMode) Debug.Log($"[FullLevelGenerator] Skip spawn (spacing) at {spawnPos} radius={spawnRadius}");
            return false;
        }

        if (usePhysicsOverlapCheck && spawnRadius > 0.01f)
        {
            var hits = Physics.OverlapSphere(spawnPos, spawnRadius, overlapLayers, QueryTriggerInteraction.Ignore);
            if (hits != null && hits.Length > 0)
            {
                if (debugMode) Debug.Log($"[FullLevelGenerator] Skip spawn (physics overlap) at {spawnPos} hits={hits.Length}");
                return false;
            }
        }

        var go = Instantiate(prefab, spawnPos, spawnRotation, parent);
        go.transform.Rotate(Vector3.up, Random.Range(-30f, 30f));
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

        float lateralOffset = Random.Range(-lateralRange, lateralRange) * effectiveHalf;
        GameObject prefab = null;
        Transform parent = null;

        switch (type)
        {
            case SpawnType.Obstacle:
                if (obstaclePrefabs.Count == 0) return false;
                prefab = obstaclePrefabs[Random.Range(0, obstaclePrefabs.Count)];
                parent = obstaclesParent;
                break;
            case SpawnType.Pickup:
                if (pickupPrefabs.Count == 0) return false;
                prefab = pickupPrefabs[Random.Range(0, pickupPrefabs.Count)];
                parent = pickupsParent;
                break;
            case SpawnType.Decor:
                if (decorPrefabs.Count == 0) return false;
                prefab = decorPrefabs[Random.Range(0, decorPrefabs.Count)];
                parent = decorParent;
                break;
        }

        if (prefab == null) return false;

        float spawnRadius = GetPrefabRadius(prefab, (type == SpawnType.Obstacle) ? obstacleSpawnRadius : (type == SpawnType.Pickup) ? pickupSpawnRadius : decorSpawnRadius);

        // Skip very tall obstacles in editor
        if (type == SpawnType.Obstacle)
        {
            float prefabHeight = GetPrefabBoundsHeight(prefab);
            if (prefabHeight > maxObstacleHeight)
            {
                if (debugMode) Debug.Log($"[FullLevelGenerator] (Editor) Skip obstacle '{prefab.name}' height {prefabHeight} > maxObstacleHeight {maxObstacleHeight}");
                return false;
            }
        }

        Vector3 spawnPos;
        Quaternion spawnRotation = Quaternion.LookRotation(tangent, Vector3.up);

        if (type == SpawnType.Decor)
        {
            float lateralSign = (lateralOffset >= 0f) ? 1f : -1f;
            float desiredLateral = lateralSign * (halfW + spawnRadius + 0.12f);
            spawnPos = center + right * desiredLateral;
        }
        else if (type == SpawnType.Obstacle && !placeObstaclesOnRoad)
        {
            float lateralSign = (lateralOffset >= 0f) ? 1f : -1f;
            float desiredLateral = lateralSign * (halfW + spawnRadius + 0.12f);
            spawnPos = center + right * desiredLateral;
        }
        else
        {
            spawnPos = center + right * lateralOffset;
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
                else
                {
                    spawnRotation = Quaternion.LookRotation(tangent, Vector3.up);
                }
            }
            else
            {
                spawnRotation = Quaternion.LookRotation(tangent, Vector3.up);
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
                float targetLat = Mathf.Clamp(sign * minCenterClearance, -effectiveHalf, effectiveHalf);
                spawnPos = center + right * targetLat;
                spawnPos.y = GetGroundYAt(spawnPos) + 0.01f;
            }
        }

        if (!forceSpawn && !CanPlaceAt(spawnPos, spawnRadius))
        {
            if (debugMode) Debug.Log($"[FullLevelGenerator] Skip spawn (spacing) at {spawnPos} radius={spawnRadius}");
            return false;
        }

        if (usePhysicsOverlapCheck && spawnRadius > 0.01f)
        {
            var hits = Physics.OverlapSphere(spawnPos, spawnRadius, overlapLayers, QueryTriggerInteraction.Ignore);
            if (hits != null && hits.Length > 0)
            {
                if (debugMode) Debug.Log($"[FullLevelGenerator] Skip spawn (physics overlap) at {spawnPos} hits={hits.Length}");
                return false;
            }
        }

        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, (parent != null ? parent.gameObject.scene : prefab.scene));
        inst.transform.SetParent(parent, false);
        inst.transform.position = spawnPos;
        inst.transform.rotation = spawnRotation;
        inst.transform.Rotate(Vector3.up, Random.Range(-30f, 30f));
        Undo.RegisterCreatedObjectUndo(inst, "Instantiate spawnable");

        placedPositions.Add(spawnPos);
        placedRadii.Add(spawnRadius);
        return true;
    }
#endif
    #endregion

    #region Moving / Checkpoints
    void GenerateMovingObstaclesRuntime()
    {
        if (movingObstaclePrefab == null || pathCreator == null) return;
        var vPath = pathCreator.path;
        float totalLen = vPath.length;
        if (totalLen <= 0f) return;

        int steps = Mathf.Max(1, movingCount);
        for (int i = 0; i < steps; i++)
        {
            float t = (i + 0.5f) / steps;
            float dist = Mathf.Clamp(t * totalLen, 0f, totalLen);
            Vector3 center = vPath.GetPointAtDistance(dist);
            Vector3 tangent = vPath.GetDirectionAtDistance(dist).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;

            float halfW = roadWidth * 0.5f;
            float lateralOffset = Random.Range(-lateralRange, lateralRange) * halfW * 0.6f;
            Vector3 pos = center + right * lateralOffset;
            pos.y = GetGroundYAt(pos) + 0.02f;

            GameObject go = Instantiate(movingObstaclePrefab, pos, Quaternion.LookRotation(tangent, Vector3.up), movingParent);
            var mover = go.GetComponent<SimpleMover>();
            if (mover == null) mover = go.AddComponent<SimpleMover>();
            mover.distance = movingDistance;
            mover.speed = movingSpeed;

            float r = GetPrefabRadius(movingObstaclePrefab, obstacleSpawnRadius);
            placedPositions.Add(pos);
            placedRadii.Add(r);
        }
    }

#if UNITY_EDITOR
    void GenerateMovingObstaclesEditor()
    {
        if (movingObstaclePrefab == null || pathCreator == null) return;
        var vPath = pathCreator.path;
        float totalLen = vPath.length;
        if (totalLen <= 0f) return;

        int steps = Mathf.Max(1, movingCount);
        for (int i = 0; i < steps; i++)
        {
            float t = (i + 0.5f) / steps;
            float dist = Mathf.Clamp(t * totalLen, 0f, totalLen);
            Vector3 center = vPath.GetPointAtDistance(dist);
            Vector3 tangent = vPath.GetDirectionAtDistance(dist).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;

            float halfW = roadWidth * 0.5f;
            float lateralOffset = Random.Range(-lateralRange, lateralRange) * halfW * 0.6f;
            Vector3 pos = center + right * lateralOffset;
            pos.y = GetGroundYAt(pos) + 0.02f;

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(movingObstaclePrefab, movingParent.gameObject.scene);
            inst.transform.SetParent(movingParent, false);
            inst.transform.position = pos;
            inst.transform.rotation = Quaternion.LookRotation(tangent, Vector3.up);
            var mover = inst.GetComponent<SimpleMover>();
            if (mover == null) mover = inst.AddComponent<SimpleMover>();
            mover.distance = movingDistance;
            mover.speed = movingSpeed;
            Undo.RegisterCreatedObjectUndo(inst, "Create moving obstacle");

            float r = GetPrefabRadius(movingObstaclePrefab, obstacleSpawnRadius);
            placedPositions.Add(pos);
            placedRadii.Add(r);
        }
    }
#endif

    void GenerateCheckpointsRuntime()
    {
        if (checkpointPrefab == null || pathCreator == null) return;
        var vPath = pathCreator.path;
        float totalLen = vPath.length;
        if (totalLen <= 0f) return;

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
            if (col == null)
            {
                BoxCollider bc = go.AddComponent<BoxCollider>();
                bc.isTrigger = true;
                bc.size = new Vector3(roadWidth * 0.8f, 2f, 1f);
            }
            else col.isTrigger = true;
        }
    }

#if UNITY_EDITOR
    void GenerateCheckpointsEditor()
    {
        if (checkpointPrefab == null || pathCreator == null) return;
        var vPath = pathCreator.path;
        float totalLen = vPath.length;
        if (totalLen <= 0f) return;

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
            if (col == null)
            {
                var bc = inst.AddComponent<BoxCollider>();
                bc.isTrigger = true;
                bc.size = new Vector3(roadWidth * 0.8f, 2f, 1f);
            }
            else col.isTrigger = true;
            Undo.RegisterCreatedObjectUndo(inst, "Create checkpoint");
        }
    }
#endif
    #endregion

    #region Utility: ground, bounds, radii, placement check
    float GetGroundYAt(Vector3 pos)
    {
        RaycastHit hit;
        Vector3 from = new Vector3(pos.x, pos.y + 10f, pos.z);
        if (Physics.Raycast(from, Vector3.down, out hit, 50f)) return hit.point.y;

        if (pathCreator != null)
        {
            var vPath = pathCreator.path;
            float closest = FindClosestDistanceAlong(vPath, pos);
            Vector3 p = vPath.GetPointAtDistance(closest);
            return p.y;
        }
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
            float s1 = (worldPos - p1).sqrMagnitude;
            float s2 = (worldPos - p2).sqrMagnitude;
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
            float rx = b.extents.x;
            float rz = b.extents.z;
            float r = Mathf.Max(rx, rz);
            if (r > 0.01f) return Mathf.Max(r, fallback * 0.5f);
        }
        return fallback;
    }

    float GetPrefabHalfHeight(GameObject prefab)
    {
        if (prefab == null) return 0f;
        var rends = prefab.GetComponentsInChildren<Renderer>();
        if (rends == null || rends.Length == 0) return 0f;
        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        return b.extents.y;
    }

    float GetPrefabBoundsHeight(GameObject prefab)
    {
        if (prefab == null) return 0f;
        var rends = prefab.GetComponentsInChildren<Renderer>();
        if (rends == null || rends.Length == 0) return 0f;
        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
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
    #endregion

    #region Densities & runtime loader
    float GetObstacleDensity()
    {
        switch (difficulty) { case Difficulty.Low: return 0.25f; case Difficulty.Medium: return 0.55f; case Difficulty.High: return 0.85f; }
        return 0.55f;
    }
    float GetPickupDensity()
    {
        switch (difficulty) { case Difficulty.Low: return 0.6f; case Difficulty.Medium: return 0.4f; case Difficulty.High: return 0.25f; }
        return 0.4f;
    }
    float GetDecorDensity()
    {
        switch (difficulty) { case Difficulty.Low: return 0.5f; case Difficulty.Medium: return 0.7f; case Difficulty.High: return 0.9f; }
        return 0.7f;
    }

    void TryRuntimeLoadPrefabs()
    {
        if (mountainLeftPrefabs.Count > 0 && mountainRightPrefabs.Count > 0 && obstaclePrefabs.Count > 0) return;

        var loaded = Resources.LoadAll<GameObject>("PerFAbs");
        if (loaded == null || loaded.Length == 0)
        {
            if (debugMode) Debug.Log("[FullLevelGenerator] No runtime prefabs found in Resources/PerFAbs. Assign in Inspector or move prefabs to Resources.");
            return;
        }

        foreach (var pf in loaded)
        {
            var name = (pf != null) ? pf.name.ToLower() : "";
            if (string.IsNullOrEmpty(name)) continue;
            if (name.Contains("coin") || name.Contains("pickup") || name.Contains("treasure")) { if (!pickupPrefabs.Contains(pf)) pickupPrefabs.Add(pf); continue; }
            if (name.Contains("enemy") || name.Contains("bat") || name.Contains("mob")) { if (!obstaclePrefabs.Contains(pf)) obstaclePrefabs.Add(pf); if (movingObstaclePrefab == null) movingObstaclePrefab = pf; continue; }
            if (name.Contains("cliff") || name.Contains("cliffedge") || name.Contains("ridge") || name.Contains("mountain")) { if (!mountainLeftPrefabs.Contains(pf)) mountainLeftPrefabs.Add(pf); if (!mountainRightPrefabs.Contains(pf)) mountainRightPrefabs.Add(pf); continue; }
            if (name.Contains("rock") || name.Contains("ledge") || name.Contains("smallrock")) { if (!obstaclePrefabs.Contains(pf)) obstaclePrefabs.Add(pf); continue; }
            if (!decorPrefabs.Contains(pf)) decorPrefabs.Add(pf);
        }

        if (debugMode) Debug.Log($"[FullLevelGenerator] Runtime loaded {loaded.Length} prefabs from Resources/PerFAbs (picked {obstaclePrefabs.Count} obstacles, {pickupPrefabs.Count} pickups, {decorPrefabs.Count} decors)");
    }
    #endregion
}
