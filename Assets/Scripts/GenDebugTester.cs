using UnityEngine;

// Attach to same GameObject as FullLevelGenerator (LevelBuilder) or any GameObject.
// In Inspector set 'generator' to your LevelBuilder (FullLevelGenerator).
// Click "Run Test" in the component context menu (or press Play with autoRunInPlay = true).
[ExecuteAlways]
public class LevelGenDebugTester : MonoBehaviour
{
    public FullLevelGenerator generator;
    public bool autoRunInPlay = false;
    public bool autoCreatePlaceholderIfEmpty = true;

    void Start()
    {
        if (Application.isPlaying && autoRunInPlay) RunTest();
    }

    [ContextMenu("Run Test")]
    public void RunTest()
    {
        if (generator == null)
        {
            generator = FindObjectOfType<FullLevelGenerator>();
            if (generator == null)
            {
                Debug.LogError("[LevelGenDebugTester] No FullLevelGenerator found in scene.");
                return;
            }
            else Debug.Log("[LevelGenDebugTester] Found FullLevelGenerator: " + generator.name);
        }

        // 1) basic prechecks
        if (generator.pathCreator == null)
        {
            Debug.LogError("[LevelGenDebugTester] pathCreator is NULL on generator. Assign Road Creator and retry.");
            return;
        }

        var vp = generator.pathCreator.path;
        if (vp == null)
        {
            Debug.LogError("[LevelGenDebugTester] generator.pathCreator.path is null.");
            return;
        }

        Debug.Log($"[LevelGenDebugTester] path.length = {vp.length:F2}");
        Debug.Log($"[LevelGenDebugTester] segmentLength={generator.segmentLength}, segmentsCount={generator.segmentsCount}");
        Debug.Log($"[LevelGenDebugTester] obstaclePrefabs={generator.obstaclePrefabs.Count}, pickupPrefabs={generator.pickupPrefabs.Count}, decorPrefabs={generator.decorPrefabs.Count}");
        Debug.Log($"[LevelGenDebugTester] forceSpawn={generator.forceSpawn}, debugMode={generator.debugMode}");

        // 2) If prefab pools are empty and allowed, create placeholder cube prefabs at runtime (for quick visual test)
        if (autoCreatePlaceholderIfEmpty &&
            generator.obstaclePrefabs.Count == 0 &&
            generator.pickupPrefabs.Count == 0 &&
            generator.decorPrefabs.Count == 0)
        {
            Debug.LogWarning("[LevelGenDebugTester] No prefabs assigned — creating temporary placeholder objects to force spawn.");
            GameObject cubeObs = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubeObs.name = "Placeholder_Obstacle";
            cubeObs.transform.localScale = Vector3.one * 0.8f;
            // make placeholder a prefab-like GameObject (not saved to assets) — generator will Instantiate it fine
            generator.obstaclePrefabs.Add(cubeObs);

            GameObject coin = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            coin.name = "Placeholder_Coin";
            coin.transform.localScale = Vector3.one * 0.4f;
            generator.pickupPrefabs.Add(coin);
        }

        // 3) ensure segmentLength set
        if (generator.segmentsCount <= 0 && generator.segmentLength <= 0.001f)
        {
            Debug.LogWarning("[LevelGenDebugTester] segmentsCount and segmentLength are zero — setting segmentLength=6 for test.");
            generator.segmentLength = 6f;
        }

        // 4) force spawn mode to avoid spacing blocking all spawns (toggle back after test)
        bool oldForce = generator.forceSpawn;
        generator.forceSpawn = true;

        // 5) call GenerateAll() and catch exceptions
        try
        {
            generator.GenerateAll();
            Debug.Log("[LevelGenDebugTester] GenerateAll() called successfully.");
        }
        catch (System.Exception ex)
        {
            Debug.LogError("[LevelGenDebugTester] Exception during GenerateAll(): " + ex);
        }

        // restore forceSpawn if it was off
        generator.forceSpawn = oldForce;
    }
}
