using UnityEngine;
using System.Collections;

public class PlayerHitDebug : MonoBehaviour
{
    public string enemyTag = "Enemy";
    public int damage = 1;
    public float invincibilitySeconds = 0.5f;
    private bool invincible = false;

    void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.CompareTag(enemyTag))
            HandleHit();
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(enemyTag))
            HandleHit();
    }

    void HandleHit()
    {
        if (invincible) return;
        invincible = true;

        if (GameManager.Instance != null)
        {
            // pass this.transform so effect spawns at the player
            GameManager.Instance.OnPlayerHit(damage, this.transform);
        }

        if (invincibilitySeconds > 0f)
            StartCoroutine(InvincibilityCoroutine());
    }

    IEnumerator InvincibilityCoroutine()
    {
        yield return new WaitForSeconds(invincibilitySeconds);
        invincible = false;
    }
}
