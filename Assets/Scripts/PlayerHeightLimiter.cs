using UnityEngine;
using PathCreation;

[RequireComponent(typeof(Rigidbody))]
public class PlayerHeightLimiter : MonoBehaviour
{
    public PathCreator pathCreator;        // drag the Road here (same as Player)
    [Tooltip("Max vertical distance above the path centerline the player is allowed")]
    public float maxHeightAbovePath = 1.2f;
    [Tooltip("If exceeded, push back toward path by this velocity change factor")]
    public float correctivePush = 4f;
    [Tooltip("If true, will also slowly move player down using MovePosition to avoid teleportation")]
    public bool smoothCorrection = true;
    public float smoothSpeed = 6f;

    Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (pathCreator == null) Debug.LogWarning("PlayerHeightLimiter: assign PathCreator.");
    }

    void FixedUpdate()
    {
        if (pathCreator == null || rb == null) return;

        // find nearest point on path (coarse sampling; this is cheap)
        var vPath = pathCreator.path;
        float bestD = 0f;
        Vector3 bestPoint = vPath.GetPointAtDistance(0f);
        float bestSqr = (transform.position - bestPoint).sqrMagnitude;
        float step = Mathf.Clamp(vPath.length / 30f, 0.05f, 1f);

        for (float d = step; d <= vPath.length; d += step)
        {
            Vector3 p = vPath.GetPointAtDistance(d);
            float sq = (transform.position - p).sqrMagnitude;
            if (sq < bestSqr) { bestSqr = sq; bestPoint = p; bestD = d; }
        }

        float allowedY = bestPoint.y + maxHeightAbovePath;
        if (rb.position.y > allowedY)
        {
            // 1) push downward gently with velocity change to escape high flight
            Vector3 pushDir = Vector3.down;
            rb.AddForce(pushDir * correctivePush * (rb.position.y - allowedY), ForceMode.VelocityChange);

            if (smoothCorrection)
            {
                // 2) MovePosition a little toward the allowed Y for a soft correction
                Vector3 target = new Vector3(rb.position.x, allowedY, rb.position.z);
                Vector3 desired = Vector3.Lerp(rb.position, target, 1f - Mathf.Exp(-smoothSpeed * Time.fixedDeltaTime));
                rb.MovePosition(desired);
            }
        }
    }
}
