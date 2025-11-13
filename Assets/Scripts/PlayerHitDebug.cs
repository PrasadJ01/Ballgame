using UnityEngine;
using System.Collections;

public class PlayerHitDebug : MonoBehaviour
{
    [Tooltip("Tag used by enemy objects")]
    public string enemyTag = "Enemy";

    [Tooltip("Tag used by finish/goal object")]
    public string finishTag = "Finish";

    [Tooltip("Damage to apply when hit")]
    public int damage = 1;

    [Tooltip("Seconds of temporary invincibility after a hit")]
    public float invincibilitySeconds = 0.5f;

    private bool invincible = false;

    void Start()
    {
        Debug.Log($"[PlayerHitDebug] ready. enemyTag={enemyTag}, finishTag={finishTag}");
    }

    void OnCollisionEnter(Collision collision)
    {
        // Check finish first so a finish object never causes damage
        if (collision.collider.CompareTag(finishTag))
        {
            HandleFinish(collision.collider.transform);
            return;
        }

        // Then check enemy
        if (collision.collider.CompareTag(enemyTag))
        {
            HandleHit();
            return;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        // Check finish first
        if (other.CompareTag(finishTag))
        {
            HandleFinish(other.transform);
            return;
        }

        if (other.CompareTag(enemyTag))
        {
            HandleHit();
            return;
        }
    }

    void HandleHit()
    {
        if (invincible) return;

        // extra safety: if game is already won, ignore hits
        if (GameManager.Instance != null && GameManager.Instance.IsWinState())
        {
            if (GameManager.Instance != null)
                Debug.Log("[PlayerHitDebug] Ignored hit because game is in Win state.");
            return;
        }

        invincible = true;
        Debug.Log("[PlayerHitDebug] Applying damage: " + damage);

        if (GameManager.Instance != null)
        {
            // pass transform so effect spawns at player if GameManager uses it
            GameManager.Instance.OnPlayerHit(damage, this.transform);
        }
        else
        {
            Debug.LogError("[PlayerHitDebug] GameManager.Instance is NULL — check GameManager is in the scene and compiled.");
        }

        if (invincibilitySeconds > 0f)
            StartCoroutine(InvincibilityCoroutine());
    }

    void HandleFinish(Transform finishTransform)
    {
        Debug.Log("[PlayerHitDebug] Reached finish.");
        if (GameManager.Instance != null)
        {
            GameManager.Instance.Win(finishTransform);
        }
        else
        {
            Debug.LogError("[PlayerHitDebug] GameManager.Instance is NULL — cannot call Win().");
        }
    }

    IEnumerator InvincibilityCoroutine()
    {
        float t = 0f;
        while (t < invincibilitySeconds)
        {
            t += Time.deltaTime;
            yield return null;
        }
        invincible = false;
        Debug.Log("[PlayerHitDebug] Invincibility ended");
    }
}
