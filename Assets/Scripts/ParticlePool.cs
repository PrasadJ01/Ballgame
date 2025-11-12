using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Simple object pool for ParticleSystem prefabs.
/// Place on a GameObject (e.g., "Pools") and assign particlePrefab.
/// Call Spawn(position, rotation) to get a particle (it will play and return to pool on completion).
/// </summary>
public class ParticlePool : MonoBehaviour
{
    [Tooltip("Particle prefab (must have a ParticleSystem root)")]
    public ParticleSystem particlePrefab;

    [Tooltip("Initial pool size")]
    public int initialSize = 8;

    private readonly Queue<ParticleSystem> pool = new Queue<ParticleSystem>();

    void Awake()
    {
        if (particlePrefab == null)
        {
            Debug.LogWarning("[ParticlePool] particlePrefab is not assigned.");
            return;
        }

        for (int i = 0; i < initialSize; i++)
            pool.Enqueue(CreateNew());
    }

    private ParticleSystem CreateNew()
    {
        var ps = Instantiate(particlePrefab, transform);
        ps.gameObject.SetActive(false);
        return ps;
    }

    /// <summary>
    /// Spawn a particle at world position/rotation. The particle plays and is returned to the pool automatically.
    /// </summary>
    public void Spawn(Vector3 position, Quaternion rotation)
    {
        if (particlePrefab == null) return;

        ParticleSystem ps = pool.Count > 0 ? pool.Dequeue() : CreateNew();
        ps.transform.SetPositionAndRotation(position, rotation);
        ps.gameObject.SetActive(true);
        ps.Clear();
        ps.Play();
        StartCoroutine(ReturnWhenFinished(ps));
    }

    private System.Collections.IEnumerator ReturnWhenFinished(ParticleSystem ps)
    {
        // Wait for duration & max lifetime (unscaled)
        float life = ps.main.duration + ps.main.startLifetime.constantMax;
        float elapsed = 0f;
        while (elapsed < life)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ps.gameObject.SetActive(false);
        pool.Enqueue(ps);
    }
}
