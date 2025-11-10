using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerJoystickController : MonoBehaviour
{
    public MobileJoystick joystick;
    public float moveSpeed = 10f;

    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        Vector3 move = new Vector3(joystick.Horizontal(), 0, joystick.Vertical());
        rb.AddForce(move * moveSpeed);
    }
}
