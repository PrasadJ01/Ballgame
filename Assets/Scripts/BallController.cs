using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BallController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveForce = 30f;
    public float jumpForce = 8f;
    public float maxSpeed = 15f;
    public LayerMask groundLayer;

    private Rigidbody rb;
    private bool isGrounded;

    private float moveX = 0f;
    private float moveZ = 0f;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        // Check if the player is grounded using a raycast
        isGrounded = Physics.Raycast(transform.position, Vector3.down, 1.1f, groundLayer);
    }

    void FixedUpdate()
    {
        // Apply movement
        Vector3 movement = new Vector3(moveX, 0f, moveZ);
        rb.AddForce(movement * moveForce);

        // Limit max speed
        if (rb.velocity.magnitude > maxSpeed)
            rb.velocity = rb.velocity.normalized * maxSpeed;
    }

    // ========= BUTTON FUNCTIONS =========

    // Move Left
    public void MoveLeftDown() { moveX = -1f; }
    public void MoveLeftUp() { moveX = 0f; }

    // Move Right
    public void MoveRightDown() { moveX = 1f; }
    public void MoveRightUp() { moveX = 0f; }

    // Move Forward (Up)
    public void MoveForwardDown() { moveZ = 1f; }
    public void MoveForwardUp() { moveZ = 0f; }

    // Move Backward (Down)
    public void MoveBackwardDown() { moveZ = -1f; }
    public void MoveBackwardUp() { moveZ = 0f; }

    // Jump
    public void Jump()
    {
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        Debug.Log("🟢 Jump called directly (no ground check).");
    }


    // Full Stop (stop all movement + velocity)
    public void StopMovement()
    {
        moveX = 0f;
        moveZ = 0f;
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }
}
