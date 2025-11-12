using UnityEngine;

public class PlayerHit3D : MonoBehaviour
{
    public string enemyTag = "Enemy";
    public int damage = 1;

    void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.CompareTag(enemyTag))
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.LoseLife(damage);
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(enemyTag))
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.LoseLife(damage);
            }
        }
    }
}
