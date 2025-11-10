using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Player joystick controller - attach this file to your Player GameObject.
/// Filename MUST be PlayerJoystickController.cs
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerJoystickController : MonoBehaviour
{
    [Tooltip("The MobileJoystick component (Drag JoystickBG here)")]
    public MobileJoystick joystick;

    [Tooltip("Movement force applied to the player RigidBody.")]
    public float moveSpeed = 10f;

    private Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
            Debug.LogError("PlayerJoystickController requires a Rigidbody on the same GameObject.");
    }

    void FixedUpdate()
    {
        if (joystick == null) return;

        float h = joystick.Horizontal();
        float v = joystick.Vertical();

        // Build local world movement (X,Z)
        Vector3 move = new Vector3(h, 0f, v);

        // If magnitude small, don't apply
        if (move.sqrMagnitude < 0.0001f) return;

        // Apply force (VelocityChange style for responsive control)
        Vector3 velChange = (move.normalized * moveSpeed) - new Vector3(rb.velocity.x, 0f, rb.velocity.z);
        rb.AddForce(velChange, ForceMode.VelocityChange);
    }
}
