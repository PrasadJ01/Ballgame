using UnityEngine;
using PathCreation;

[RequireComponent(typeof(Rigidbody))]
public class RoadBoundaryConstraintSmooth : MonoBehaviour
{
    [Header("Path Settings")]
    public PathCreator pathCreator;     // assign the PathCreator used for the road
    public float roadWidth = 2.5f;      // full road width (use your Road Width value)
    [Tooltip("Distance between sample points when searching nearest point (meters). Smaller = more accurate.")]
    public float sampleStep = 0.5f;

    [Header("Correction / Smoothing")]
    [Tooltip("If true, MovePosition is used (smooth). If false, rb.position is assigned (not recommended).")]
    public bool useMovePosition = true;
    [Tooltip("How quickly the ball is moved to the clamped position (higher = snappier)")]
    [Range(0.1f, 50f)] public float correctionSpeed = 10f;
    [Tooltip("Small push applied outward if the ball penetrates edge (VelocityChange).")]
    public float correctionForce = 3f;
    [Tooltip("How quickly lateral velocity (toward walls) is damped (0 = none, 1 = remove instantly)")]
    [Range(0f, 1f)] public float lateralDamping = 0.9f;
    [Tooltip("Minimum penetration (meters) before applying push force")]
    public float minPenetrationForForce = 0.02f;

    [Header("Behavior Flags")]
    public bool clampPosition = true;
    public bool removeLateralVelocity = true;

    Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (pathCreator == null)
            Debug.LogError("RoadBoundaryConstraintSmooth: assign PathCreator.");
        // recommend: enable interpolation & continuous collision on Rigidbody in inspector for smoother motion
    }

    void FixedUpdate()
    {
        if (pathCreator == null || rb == null) return;

        var vPath = pathCreator.path;
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

        // Check the end
        Vector3 endPoint = vPath.GetPointAtDistance(pathLen);
        float endSq = (transform.position - endPoint).sqrMagnitude;
        if (endSq < bestSqr)
        {
            bestSqr = endSq;
            bestPoint = endPoint;
            bestDistAlong = pathLen;
        }

        // Path tangent and right vector
        Vector3 tangent = vPath.GetDirectionAtDistance(bestDistAlong).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;

        // lateral offset (signed)
        Vector3 delta = transform.position - bestPoint;
        float lateral = Vector3.Dot(delta, right);
        float halfWidth = roadWidth * 0.5f;

        // If outside allowed half-width, correct
        if (Mathf.Abs(lateral) > halfWidth)
        {
            float clampedLateral = Mathf.Clamp(lateral, -halfWidth, halfWidth);
            Vector3 targetPos = bestPoint + right * clampedLateral;
            // keep current Y so gravity/jump not lost
            targetPos.y = rb.position.y;

            if (clampPosition)
            {
                if (useMovePosition)
                {
                    // Smoothly move toward target using MovePosition (cooperates with physics)
                    Vector3 desired = Vector3.Lerp(rb.position, targetPos, 1f - Mathf.Exp(-correctionSpeed * Time.fixedDeltaTime));
                    rb.MovePosition(desired);
                }
                else
                {
                    // direct set (less recommended)
                    rb.position = targetPos;
                }
            }

            // Velocity correction: remove / dampen component toward the wall (lateral)
            if (removeLateralVelocity)
            {
                Vector3 vel = rb.velocity;
                // separate velocity along tangent + up + lateral (right)
                Vector3 velAlongForward = Vector3.Project(vel, tangent);
                Vector3 velUp = Vector3.Project(vel, Vector3.up);
                float velLateral = Vector3.Dot(vel, right);

                // damp lateral rather than zeroing instantly to avoid snap
                float newLateral = velLateral * (1f - lateralDamping);
                // rebuild velocity
                Vector3 newVel = velAlongForward + velUp + right * newLateral;
                rb.velocity = newVel;
            }

            // Apply a small corrective impulse when penetration is significant to avoid overlap/sticking
            float penetration = Mathf.Abs(lateral) - halfWidth;
            if (penetration > minPenetrationForForce && correctionForce > 0f)
            {
                // direction pushing back inside (negative of sign(lateral) along right)
                float sign = (lateral > 0f) ? -1f : 1f;
                Vector3 push = right * sign; // push toward center
                // velocity-change force is instantaneous and prevents sticking
                rb.AddForce(push * correctionForce * penetration, ForceMode.VelocityChange);
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        if (pathCreator == null) return;
        var vPath = pathCreator.path;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 0.1f);

        int steps = Mathf.CeilToInt(vPath.length / Mathf.Max(0.01f, sampleStep));
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
