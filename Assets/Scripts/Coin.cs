using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Coin : MonoBehaviour
{
    [Header("Coin Settings")]
    public int scoreValue = 10;
    public int coinValue = 1;
    public AudioClip collectSound;
    public GameObject pickupVFX;       // ← assign CoinSparkleVFX prefab here
    public float vfxLifetime = 1.0f;

    bool collected = false;

    void Awake()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (collected) return;

        if (other.CompareTag("Player"))
        {
            collected = true;

            // Update score + coin count
            if (GameManager.Instance != null)
            {
                GameManager.Instance.AddScore(scoreValue);
                GameManager.Instance.AddCoin(coinValue);
            }

            // 🔊 play pickup sound
            if (collectSound != null)
                AudioSource.PlayClipAtPoint(collectSound, transform.position);

            // ✨ spawn sparkle effect
            if (pickupVFX != null)
            {
                var vfx = Instantiate(pickupVFX, transform.position, Quaternion.identity);
                Destroy(vfx, vfxLifetime);
            }

            // disable coin visuals
            var rend = GetComponentInChildren<Renderer>();
            if (rend) rend.enabled = false;
            GetComponent<Collider>().enabled = false;

            Destroy(gameObject, 0.1f);
        }
    }
}
