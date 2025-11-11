using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;
using UnityEngine.EventSystems;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Stats")]
    public int score = 0, coins = 0, lives = 3;

    [Header("UI")]
    public TextMeshProUGUI scoreText, coinsText, livesText;
    public GameObject gameOverPanel, pausePanel;
    public Button replayButton;

    [Header("Settings")]
    public float restartDelay = 0.5f;

    private bool isGameOver = false;

    void Awake()
    {
        // Singleton
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Ensure EventSystem exists with StandaloneInputModule so UI works reliably
        if (EventSystem.current == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
            DontDestroyOnLoad(es);
        }
    }

    void Start()
    {
        UpdateUI();

        if (gameOverPanel) gameOverPanel.SetActive(false);
        if (pausePanel) pausePanel.SetActive(false);

        if (replayButton != null)
        {
            replayButton.onClick.RemoveAllListeners();
            replayButton.onClick.AddListener(() =>
            {
                Debug.Log("[GameManager] Replay button clicked!");
                RestartGame();
            });
            // keep it interactable by default; we'll ensure it's enabled at GameOver
            replayButton.interactable = true;
        }
        else
        {
            Debug.LogWarning("[GameManager] Replay button not assigned in inspector!");
        }
    }

    // Public API for other scripts
    public void AddScore(int a) { score += a; UpdateUI(); }
    public void AddCoin(int a)  { coins += a; UpdateUI(); }
    public void AddLife(int a = 1) { if (!isGameOver) { lives += a; UpdateUI(); } }

    // Call this to apply damage (reduces lives, triggers GameOver if <= 0)
    public void LoseLife(int a = 1)
    {
        if (isGameOver) return;

        lives -= a;
        if (lives <= 0)
        {
            lives = 0;
            UpdateUI();
            GameOver(); // trigger game over flow
            return;
        }
        UpdateUI();
    }

    // Internal GameOver flow (keeps GameOver panel active & Replay button clickable)
    void GameOver()
    {
        isGameOver = true;

        if (gameOverPanel)
        {
            gameOverPanel.SetActive(true);
            Canvas.ForceUpdateCanvases(); // refresh layout so raycasts are correct
        }

        if (replayButton)
        {
            // Ensure button GameObject is active and interactable
            replayButton.gameObject.SetActive(true);
            replayButton.interactable = true;

            // Set as selected so keyboard/controller and focus works
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(replayButton.gameObject);
        }

        Debug.Log("[GameManager] Game Over! Pausing after delay...");
        StartCoroutine(PauseAfterDelayUnscaled(restartDelay));
    }

    IEnumerator PauseAfterDelayUnscaled(float delay)
    {
        float t = 0f;
        while (t < delay)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        // Pause gameplay but keep UI interactive via EventSystem
        Time.timeScale = 0f;
        Debug.Log("[GameManager] Paused (Time.timeScale = 0). UI remains interactive.");
    }

    public void RestartGame()
    {
        Debug.Log("[GameManager] Restarting game...");
        Time.timeScale = 1f; // ensure time resumes before reload

        isGameOver = false;
        score = 0;
        coins = 0;
        lives = 3;

        if (gameOverPanel) gameOverPanel.SetActive(false);
        if (pausePanel) pausePanel.SetActive(false);

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void TogglePause()
    {
        if (isGameOver) return;

        bool toPause = Time.timeScale > 0f;
        if (pausePanel) pausePanel.SetActive(toPause);
        Time.timeScale = toPause ? 0f : 1f;
        Debug.Log("[GameManager] Pause toggled: " + (toPause ? "Paused" : "Resumed"));
    }

    void UpdateUI()
    {
        if (scoreText) scoreText.text = "Score: " + score.ToString("0000");
        if (coinsText) coinsText.text = "Coins: " + coins;
        if (livesText) livesText.text = "Lives: " + lives;
    }

#if UNITY_EDITOR
    // Debug helper: press R to restart in editor
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            Debug.Log("[GameManager] Debug restart (R key).");
            RestartGame();
        }
    }
#endif
}
