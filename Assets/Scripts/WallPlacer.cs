using UnityEngine;
using PathCreation;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class WallPlacer : MonoBehaviour
{
    public PathCreator pathCreator;          // Assign your Path Creator (road)
    public GameObject wallPrefab;            // Assign your wall prefab
    public float wallHeight = 2f;
    public float wallThickness = 0.5f;
    public float spacing = 2f;               // Distance between wall segments
    public float sideOffset = 2.5f;          // How far from road center
    public bool createBothSides = true;
    public bool clearPrevious = true;

#if UNITY_EDITOR
    [ContextMenu("Generate Walls")]
    public void GenerateWalls()
    {
        if (pathCreator == null || wallPrefab == null)
        {
            Debug.LogError("⚠️ WallPlacer: Assign PathCreator and WallPrefab first!");
            return;
        }

        Transform holder = GetOrCreateHolder("Walls");

        if (clearPrevious)
        {
            for (int i = holder.childCount - 1; i >= 0; i--)
                DestroyImmediate(holder.GetChild(i).gameObject);
        }

        BezierPath path = pathCreator.bezierPath;
        VertexPath vPath = pathCreator.path;

        for (float dist = 0; dist < vPath.length; dist += spacing)
        {
            Vector3 point = vPath.GetPointAtDistance(dist);
            Vector3 tangent = vPath.GetDirectionAtDistance(dist);
            Vector3 normal = Vector3.Cross(Vector3.up, tangent).normalized;

            // LEFT wall
            Vector3 leftPos = point - normal * sideOffset + Vector3.up * (wallHeight / 2);
            GameObject left = (GameObject)PrefabUtility.InstantiatePrefab(wallPrefab);
            left.transform.position = leftPos;
            left.transform.rotation = Quaternion.LookRotation(tangent, Vector3.up);
            left.transform.localScale = new Vector3(wallThickness, wallHeight, spacing);
            left.transform.SetParent(holder);

            if (createBothSides)
            {
                // RIGHT wall
                Vector3 rightPos = point + normal * sideOffset + Vector3.up * (wallHeight / 2);
                GameObject right = (GameObject)PrefabUtility.InstantiatePrefab(wallPrefab);
                right.transform.position = rightPos;
                right.transform.rotation = Quaternion.LookRotation(tangent, Vector3.up);
                right.transform.localScale = new Vector3(wallThickness, wallHeight, spacing);
                right.transform.SetParent(holder);
            }
        }

        Debug.Log("✅ WallPlacer: Generated walls successfully.");
    }

    private Transform GetOrCreateHolder(string name)
    {
        Transform t = transform.Find(name);
        if (t == null)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform);
            t = go.transform;
        }
        return t;
    }
#endif
}
