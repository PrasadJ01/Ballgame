using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CoinSpin : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        // Rotate around X to look like a flipping coin
        transform.Rotate(Vector3.right * 180f * Time.deltaTime);

        // Add spin around Y for realism
        transform.Rotate(Vector3.up * 90f * Time.deltaTime, Space.World);

        // Optional bob motion
        transform.position += Vector3.up * Mathf.Sin(Time.time * 4f) * 0.002f;
    }


}
