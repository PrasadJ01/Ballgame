// RoadBoundaryConstraint.cs
using UnityEngine;
using PathCreation;

[RequireComponent(typeof(Rigidbody))]
public class RoadBoundaryConstraint : MonoBehaviour
{
    [Header("Path Settings")]
    public PathCreator pathCreator;     // assign the PathCreator used for the road
    public float roadWidth = 2.5f;     // full road width (use your Road Width value)
    [Tooltip("Distance between sample points when searching nearest point (meters). Smaller = more accurate.")]
    public float sampleStep = 0.5f;

    [Header("Behavior")]
    public bool clampPosition = true;  // clamp position to road edges
    public bool removeLateralVelocity = true; // remove lateral velocity component when clamped

    Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (pathCreator == null)
            Debug.LogError("RoadBoundaryConstraint: assign PathCreator.");
    }

    void FixedUpdate()
    {
        if (pathCreator == null || rb == null) return;

        var vPath = pathCreator.path; // VertexPath
        float pathLen = vPath.length;

        // Find closest point on path by sampling distances
        float bestDistAlong = 0f;
        Vector3 bestPoint = vPath.GetPointAtDistance(0f);
        float bestSqr = (transform.position - bestPoint).sqrMagnitude;

        for (float d = sampleStep; d <= pathLen; d += sampleStep)
        {
            Vector3 p = vPath.GetPointAtDistance(d);
            float sq = (transform.position - p).sqrMagnitude;
            if (sq < bestSqr)
            {
                bestSqr = sq;
                bestPoint = p;
                bestDistAlong = d;
            }
        }

        // Also test the very end of path (in case loop missed due to rounding)
        Vector3 endPoint = vPath.GetPointAtDistance(pathLen);
        float endSq = (transform.position - endPoint).sqrMagnitude;
        if (endSq < bestSqr)
        {
            bestSqr = endSq;
            bestPoint = endPoint;
            bestDistAlong = pathLen;
        }

        // Get path tangent (forward) at that distance
        Vector3 tangent = vPath.GetDirectionAtDistance(bestDistAlong).normalized;

        // Compute right vector perpendicular to tangent (assuming world-up Y)
        Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;

        // Compute lateral offset from centerline (signed)
        Vector3 delta = transform.position - bestPoint;
        float lateral = Vector3.Dot(delta, right);

        float halfWidth = roadWidth * 0.5f;
        if (Mathf.Abs(lateral) > halfWidth)
        {
            // clamp lateral into the allowed range
            float clampedLateral = Mathf.Clamp(lateral, -halfWidth, halfWidth);

            // New position on clamped lateral but same forward & height (Y)
            Vector3 newPos = bestPoint + right * clampedLateral;
            newPos.y = transform.position.y; // keep current Y (so gravity / jumping still works)

            if (clampPosition)
            {
                // Move rigidbody to clamped position (preserve physics as much as possible)
                rb.position = newPos;
                // or use MovePosition: rb.MovePosition(newPos);
            }

            if (removeLateralVelocity)
            {
                // Remove lateral velocity so player doesn't keep sliding into the wall
                Vector3 vel = rb.velocity;
                Vector3 velAlongForward = Vector3.Project(vel, tangent);
                rb.velocity = new Vector3(velAlongForward.x, vel.y, velAlongForward.z);
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        if (pathCreator == null) return;
        var vPath = pathCreator.path;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 0.1f);

        // draw left/right road edges along path for visual debug
        int steps = Mathf.CeilToInt(vPath.length / sampleStep);
        float halfW = roadWidth * 0.5f;
        for (int i = 0; i <= steps; i++)
        {
            float d = (i / (float)steps) * vPath.length;
            Vector3 p = vPath.GetPointAtDistance(d);
            Vector3 t = vPath.GetDirectionAtDistance(d).normalized;
            Vector3 r = Vector3.Cross(Vector3.up, t).normalized;
            Gizmos.color = Color.red;
            Gizmos.DrawLine(p - r * halfW + Vector3.up * 0.05f, p + r * halfW + Vector3.up * 0.05f);
        }
    }
}
