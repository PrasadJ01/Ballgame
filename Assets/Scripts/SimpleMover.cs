
using UnityEngine;

public class SimpleMover : MonoBehaviour
{
    public float distance = 2f;
    public float speed = 1.5f;

    Vector3 a;
    Vector3 b;
    float t;

    void Start()
    {
        Vector3 dir = transform.right;
        a = transform.position - dir * (distance * 0.5f);
        b = transform.position + dir * (distance * 0.5f);
        t = Random.Range(0f, 1f);
    }

    void Update()
    {
        if (distance <= 0.001f) return;
        t += Time.deltaTime * speed;
        float s = (Mathf.Sin(t * Mathf.PI * 2f) + 1f) * 0.5f;
        transform.position = Vector3.Lerp(a, b, s);
    }
}
