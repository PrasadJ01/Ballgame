using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using System.Collections;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Player Stats")]
    public int startLives = 3;
    [HideInInspector] public int lives;
    public int score = 0;
    public int coins = 0;

    [Header("UI References")]
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI coinsText;
    public TextMeshProUGUI livesText;

    [Tooltip("Panel or Canvas that contains the Game Over UI")]
    public GameObject gameOverPanel;
    public TextMeshProUGUI gameOverText;
    public Button restartButton;

    [Header("Particle & Pool")]
    [Tooltip("ParticlePool that will spawn life-loss particles")]
    public ParticlePool particlePool;            // assign in inspector
    [Tooltip("Particle system local offset from player position when spawning (optional)")]
    public Vector3 lifeLostOffset = Vector3.up * 1.0f;

    private bool isGameOver = false;

    void Awake()
    {
        // singleton
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (EventSystem.current == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
            DontDestroyOnLoad(es);
        }
    }

    void Start()
    {
        lives = startLives;
        if (gameOverPanel) gameOverPanel.SetActive(false);
        if (restartButton) restartButton.gameObject.SetActive(false);
        if (restartButton != null)
        {
            restartButton.onClick.RemoveAllListeners();
            restartButton.onClick.AddListener(RestartGame);
        }
        UpdateUI();
    }

    // call this from PlayerHit
    public void OnPlayerHit(int damage = 1, Transform hitTransform = null)
    {
        if (isGameOver) return;

        // play particle at hit location (prefer hitTransform, else center)
        Vector3 spawnPos = (hitTransform != null) ? hitTransform.position + lifeLostOffset : lifeLostOffset;
        PlayLifeLostEffect(spawnPos);

        lives -= damage;
        if (lives < 0) lives = 0;
        UpdateUI();

        if (lives <= 0) ShowGameOver();
    }

    /// <summary>
    /// Play a particle effect for life lost. Uses the ParticlePool if assigned.
    /// </summary>
    public void PlayLifeLostEffect(Vector3 worldPosition)
    {
        if (particlePool != null && particlePool.particlePrefab != null)
        {
            particlePool.Spawn(worldPosition, Quaternion.identity);
        }
        else
        {
            // fallback: simple debug log if no pool
            Debug.Log("[GameManager] PlayLifeLostEffect: particlePool not assigned.");
        }
    }

    void ShowGameOver()
    {
        isGameOver = true;
        if (gameOverPanel) gameOverPanel.SetActive(true);
        if (restartButton) restartButton.gameObject.SetActive(true);
        Time.timeScale = 0f;
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // ... existing AddScore/AddCoin/LoseLife/UpdateUI methods (unchanged) ...
    public void AddScore(int amount) { score += amount; UpdateUI(); }
    public void AddCoin(int amount)  { coins += amount; UpdateUI(); }
    void UpdateUI()
    {
        if (scoreText) scoreText.text = "Score: " + score.ToString("0000");
        if (coinsText) coinsText.text = "Coins: " + coins.ToString();
        if (livesText) livesText.text = "Lives: " + lives.ToString();
    }
}
