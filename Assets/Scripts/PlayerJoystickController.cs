using UnityEngine;

/// <summary>
/// Player movement that reads MobileJoystick input and moves a Rigidbody player.
/// Attach to Player (sphere) which must have a Rigidbody and Collider.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerJoystickController : MonoBehaviour
{
    public enum MovementMode { Torque, Force }

    [Header("References")]
    public MobileJoystick joystick;        // Drag JoystickBG here (has MobileJoystick component)
    public Camera referenceCamera;         // Drag Main Camera here (for relative movement)

    [Header("Movement")]
    public MovementMode movementMode = MovementMode.Torque;
    public float torqueStrength = 18f;     // try 12-30 for rolling ball
    public float forceStrength = 8f;       // for Force mode (velocity change)
    public float maxSpeed = 14f;
    public float deadZone = 0.12f;         // ignore tiny inputs

    [Header("Jump (optional)")]
    public float jumpForce = 6f;
    public LayerMask groundMask;
    public float groundCheckDistance = 0.6f;
    public Transform groundCheckOrigin;    // child transform near bottom of player (optional)

    Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (referenceCamera == null) referenceCamera = Camera.main;
        if (joystick == null) Debug.LogWarning("PlayerJoystickController: Assign joystick in Inspector.");
    }

    void FixedUpdate()
    {
        if (joystick == null) return;

        Vector2 in2 = joystick.Direction();
        if (in2.magnitude < deadZone) return;

        // Map joystick to world direction relative to camera
        Vector3 camF = referenceCamera.transform.forward; camF.y = 0; camF.Normalize();
        Vector3 camR = referenceCamera.transform.right; camR.y = 0; camR.Normalize();

        Vector3 moveDir = (camR * in2.x + camF * in2.y);
        if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();

        if (movementMode == MovementMode.Torque)
        {
            // Roll: apply torque around axis perpendicular to movement direction
            Vector3 torqueAxis = Vector3.Cross(Vector3.up, moveDir).normalized;
            Vector3 torque = torqueAxis * (in2.magnitude * torqueStrength);
            rb.AddTorque(torque, ForceMode.Force);

            // clamp horizontal speed
            Vector3 hor = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
            if (hor.magnitude > maxSpeed)
            {
                Vector3 clamped = hor.normalized * maxSpeed;
                rb.velocity = new Vector3(clamped.x, rb.velocity.y, clamped.z);
            }
        }
        else // Force mode
        {
            Vector3 desiredVel = moveDir * forceStrength;
            Vector3 velChange = desiredVel - new Vector3(rb.velocity.x, 0f, rb.velocity.z);
            rb.AddForce(velChange, ForceMode.VelocityChange);

            Vector3 hor = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
            if (hor.magnitude > maxSpeed)
            {
                Vector3 clamped = hor.normalized * maxSpeed;
                rb.velocity = new Vector3(clamped.x, rb.velocity.y, clamped.z);
            }
        }
    }

    public void Jump()
    {
        if (IsGrounded())
            rb.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);
    }

    bool IsGrounded()
    {
        Vector3 origin = groundCheckOrigin != null ? groundCheckOrigin.position : transform.position;
        return Physics.Raycast(origin, Vector3.down, groundCheckDistance, groundMask);
    }
}
