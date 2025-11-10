using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class MobilePlayerController : MonoBehaviour
{
    public enum MovementMode { Torque, Force }
    [Header("References")]
    public FloatingJoystick joystick;
    public Camera mainCamera; // assign your main camera (for directional mapping)

    [Header("Movement Settings")]
    public MovementMode mode = MovementMode.Torque;
    public float torqueStrength = 10f;   // for Torque mode
    public float moveForce = 8f;         // for Force mode
    public float maxSpeed = 12f;
    public float deadZone = 0.15f;       // ignore tiny joystick inputs

    Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (mainCamera == null) mainCamera = Camera.main;
    }

    void FixedUpdate()
    {
        if (joystick == null) return;

        Vector2 j = joystick.Direction;
        if (j.magnitude < deadZone)
        {
            // small dead zone -> do nothing
            return;
        }

        // Map joystick (x,y) to world direction relative to camera
        Vector3 camForward = mainCamera.transform.forward;
        camForward.y = 0;
        camForward.Normalize();
        Vector3 camRight = mainCamera.transform.right;
        camRight.y = 0;
        camRight.Normalize();

        Vector3 moveDir = (camRight * j.x + camForward * j.y).normalized;

        if (mode == MovementMode.Torque)
        {
            // torque axis is perpendicular to desired move direction to roll the ball
            Vector3 torqueAxis = Vector3.Cross(Vector3.up, moveDir).normalized;
            // torque magnitude scales with joystick magnitude and torqueStrength
            Vector3 appliedTorque = torqueAxis * (joystick.Magnitude * torqueStrength);
            rb.AddTorque(appliedTorque, ForceMode.Force);

            // optional: clamp horizontal speed
            Vector3 horizontal = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
            if (horizontal.magnitude > maxSpeed)
            {
                Vector3 clamped = horizontal.normalized * maxSpeed;
                rb.velocity = new Vector3(clamped.x, rb.velocity.y, clamped.z);
            }
        }
        else // Force or direct movement
        {
            Vector3 desiredVel = moveDir * moveForce;
            Vector3 velChange = desiredVel - new Vector3(rb.velocity.x, 0f, rb.velocity.z);
            rb.AddForce(velChange, ForceMode.VelocityChange);

            Vector3 horizontal = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
            if (horizontal.magnitude > maxSpeed)
            {
                Vector3 clamped = horizontal.normalized * maxSpeed;
                rb.velocity = new Vector3(clamped.x, rb.velocity.y, clamped.z);
            }
        }
    }
}
