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
    [Tooltip("Number of lives player starts with")]
    public int startLives = 3;
    [HideInInspector] public int lives;
    public int score = 0;
    public int coins = 0;

    [Header("UI References")]
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI coinsText;
    public TextMeshProUGUI livesText;

    [Header("Game Over UI")]
    [Tooltip("Panel that appears on Game Over")]
    public GameObject gameOverPanel;
    public TextMeshProUGUI gameOverText;
    [Tooltip("Restart button inside the Game Over panel")]
    public Button restartButton;

    [Header("Win UI")]
    [Tooltip("Panel that appears on Win")]
    public GameObject winPanel;
    public TextMeshProUGUI winText;
    [Tooltip("Continue button inside the Win panel")]
    public Button winContinueButton;

    [Header("Optional Effects")]
    [Tooltip("Optional ParticleSystem prefab to play when life is lost")]
    public ParticleSystem lifeLostEffectPrefab;
    [Tooltip("Optional ParticleSystem prefab to play on win")]
    public ParticleSystem winEffectPrefab;

    [Header("Settings")]
    [Tooltip("If true, show debug messages")]
    public bool debugLogs = false;

    private bool isGameOver = false;
    private bool isWin = false;

    void Awake()
    {
        // singleton
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // ensure EventSystem exists so UI works
        if (EventSystem.current == null)
        {
            var esGO = new GameObject("EventSystem");
            esGO.AddComponent<EventSystem>();
            esGO.AddComponent<StandaloneInputModule>();
            DontDestroyOnLoad(esGO);
        }
    }

    void Start()
    {
        // initialize lives
        lives = Mathf.Max(0, startLives);

        // hide UI panels at start
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (winPanel != null) winPanel.SetActive(false);

        // hide buttons initially (they'll be enabled when needed)
        if (restartButton != null) restartButton.gameObject.SetActive(false);
        if (winContinueButton != null) winContinueButton.gameObject.SetActive(false);

        // wire button callbacks
        if (restartButton != null)
        {
            restartButton.onClick.RemoveAllListeners();
            restartButton.onClick.AddListener(RestartGame);
        }
        if (winContinueButton != null)
        {
            winContinueButton.onClick.RemoveAllListeners();
            winContinueButton.onClick.AddListener(OnWinContinueButtonPressed);
        }

        UpdateUI();

        if (debugLogs) Debug.Log($"[GameManager] Start() lives={lives} score={score} coins={coins}");
    }

    // ----------------------
    // Public API (other scripts call these)
    // ----------------------

    public void AddScore(int amount)
    {
        score += amount;
        UpdateUI();
        if (debugLogs) Debug.Log($"[GameManager] AddScore({amount}) -> score={score}");
    }

    public void AddCoin(int amount)
    {
        coins += amount;
        UpdateUI();
        if (debugLogs) Debug.Log($"[GameManager] AddCoin({amount}) -> coins={coins}");
    }

    // Called by Player when hit. hitTransform optional - used for effects location.
    public void OnPlayerHit(int damage = 1, Transform hitTransform = null)
    {
        if (isGameOver || isWin) return;

        if (debugLogs) Debug.Log($"[GameManager] OnPlayerHit(damage={damage})");

        // play life-lost effect (optional)
        if (lifeLostEffectPrefab != null)
        {
            Vector3 spawnPos = (hitTransform != null) ? hitTransform.position : Vector3.zero;
            Instantiate(lifeLostEffectPrefab, spawnPos, Quaternion.identity);
        }

        LoseLife(damage);
    }

    // Some scripts may call this directly
    public void LoseLife(int amount = 1)
    {
        if (isGameOver || isWin) return;

        lives -= amount;
        lives = Mathf.Max(0, lives);
        UpdateUI();

        if (debugLogs) Debug.Log($"[GameManager] LoseLife({amount}) -> lives={lives}");

        if (lives <= 0)
        {
            ShowGameOver();
        }
    }

    // Returns whether the game is in win state (useful for player script safety)
    public bool IsWinState()
    {
        return isWin;
    }

    // ----------------------
    // Game Over flow
    // ----------------------
    void ShowGameOver()
    {
        if (isGameOver) return;
        isGameOver = true;

        if (debugLogs) Debug.Log("[GameManager] ShowGameOver()");

        // show panel if assigned
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
            BringToFront(gameOverPanel);
        }

        if (gameOverText != null) gameOverText.text = "GAME OVER";

        // show restart button
        if (restartButton != null)
        {
            restartButton.gameObject.SetActive(true);
            restartButton.interactable = true;
            EventSystem.current?.SetSelectedGameObject(restartButton.gameObject);
        }

        // pause gameplay (UI still works)
        Time.timeScale = 0f;
    }

    // ----------------------
    // Win flow
    // ----------------------
    /// <summary> Call when player reaches the finish/goal. </summary>
    public void Win(Transform finishTransform = null)
    {
        if (isGameOver || isWin) return;
        isWin = true;

        if (debugLogs) Debug.Log("[GameManager] Win() called.");

        // spawn win effect if available
        if (finishTransform != null && winEffectPrefab != null)
        {
            Instantiate(winEffectPrefab, finishTransform.position, Quaternion.identity);
        }

        ShowWin();
    }

    void ShowWin()
    {
        if (winPanel != null)
        {
            winPanel.SetActive(true);
            BringToFront(winPanel);
        }

        if (winText != null) winText.text = "YOU WIN!";

        if (winContinueButton != null)
        {
            winContinueButton.gameObject.SetActive(true);
            winContinueButton.interactable = true;
            EventSystem.current?.SetSelectedGameObject(winContinueButton.gameObject);
        }

        // pause gameplay
        Time.timeScale = 0f;
    }

    void OnWinContinueButtonPressed()
    {
        // Default: load next scene if available, otherwise reload current
        int nextIndex = SceneManager.GetActiveScene().buildIndex + 1;
        if (nextIndex < SceneManager.sceneCountInBuildSettings)
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(nextIndex);
        }
        else
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }

    // ----------------------
    // Restart / Utility
    // ----------------------
    public void RestartGame()
    {
        if (debugLogs) Debug.Log("[GameManager] RestartGame() called.");
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void BringToFront(GameObject panel)
    {
        var rt = panel.transform as RectTransform;
        if (rt != null) rt.SetAsLastSibling();
        var canvas = panel.GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            var gr = canvas.GetComponent<GraphicRaycaster>();
            if (gr == null) canvas.gameObject.AddComponent<GraphicRaycaster>();
            canvas.sortingOrder = 999;
        }
        var cg = panel.GetComponent<CanvasGroup>();
        if (cg == null) cg = panel.AddComponent<CanvasGroup>();
        cg.interactable = true;
        cg.blocksRaycasts = true;
        if (cg.alpha <= 0f) cg.alpha = 1f;
    }

    void UpdateUI()
    {
        if (scoreText) scoreText.text = "Score: " + score.ToString("0000");
        if (coinsText) coinsText.text = "Coins: " + coins.ToString();
        if (livesText) livesText.text = "Lives: " + lives.ToString();
    }
}
