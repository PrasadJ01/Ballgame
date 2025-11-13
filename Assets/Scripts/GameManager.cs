using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

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

    [Header("Game Over UI")]
    public GameObject gameOverPanel;
    public TextMeshProUGUI gameOverText;
    public Button restartButton;

    [Header("Win UI")]
    public GameObject winPanel;
    public TextMeshProUGUI winText;
    public Button winContinueButton;

    [Header("Effects")]
    public ParticleSystem lifeLostEffectPrefab;
    public ParticleSystem winEffectPrefab;

    bool isGameOver = false;
    bool isWin = false;

    void Awake()
    {
        if (Instance != null && Instance != this) 
        { 
            Destroy(gameObject); 
            return; 
        }
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
        if (winPanel) winPanel.SetActive(false);

        if (restartButton != null)
        {
            restartButton.gameObject.SetActive(false);
            restartButton.onClick.RemoveAllListeners();
            restartButton.onClick.AddListener(RestartGame);   // ✅ SAFE NOW
        }

        if (winContinueButton != null)
        {
            winContinueButton.gameObject.SetActive(false);
            winContinueButton.onClick.RemoveAllListeners();
            winContinueButton.onClick.AddListener(OnWinContinue);
        }

        UpdateUI();
    }

    // ---------------------------------------------------------
    // PUBLIC API
    // ---------------------------------------------------------
    public void AddScore(int amount)
    {
        score += amount;
        UpdateUI();
    }

    public void AddCoin(int amount)
    {
        coins += amount;
        UpdateUI();
    }

    public bool IsWinState()
    {
        return isWin;
    }

    public void OnPlayerHit(int damage = 1, Transform hitTransform = null)
    {
        if (isGameOver || isWin) return;

        if (lifeLostEffectPrefab && hitTransform)
        {
            Instantiate(lifeLostEffectPrefab, hitTransform.position, Quaternion.identity);
        }

        LoseLife(damage);
    }

    public void LoseLife(int amount = 1)
    {
        if (isGameOver || isWin) return;

        lives -= amount;
        if (lives < 0) lives = 0;

        UpdateUI();

        if (lives <= 0)
        {
            ShowGameOver();
        }
    }

    // ---------------------------------------------------------
    // GAME OVER
    // ---------------------------------------------------------
    void ShowGameOver()
    {
        isGameOver = true;

        if (gameOverPanel) gameOverPanel.SetActive(true);
        if (gameOverText) gameOverText.text = "GAME OVER";

        if (restartButton) restartButton.gameObject.SetActive(true);

        Time.timeScale = 0f;
    }

    // ---------------------------------------------------------
    // WIN
    // ---------------------------------------------------------
    public void Win(Transform finishTransform)
    {
        if (isGameOver || isWin) return;

        isWin = true;

        if (winEffectPrefab && finishTransform)
            Instantiate(winEffectPrefab, finishTransform.position, Quaternion.identity);

        ShowWin();
    }

    void ShowWin()
    {
        if (winPanel) winPanel.SetActive(true);
        if (winText) winText.text = "YOU WIN!";
        if (winContinueButton) winContinueButton.gameObject.SetActive(true);

        Time.timeScale = 0f;
    }

    void OnWinContinue()
    {
        Time.timeScale = 1f;
        int next = SceneManager.GetActiveScene().buildIndex + 1;

        if (next < SceneManager.sceneCountInBuildSettings)
            SceneManager.LoadScene(next);
        else
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // ---------------------------------------------------------
    // REQUIRED — FIXES YOUR ERROR
    // ---------------------------------------------------------
    public void RestartGame()   // 🎯 THIS METHOD IS WHAT WAS MISSING
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // ---------------------------------------------------------
    // UI UPDATE
    // ---------------------------------------------------------
    void UpdateUI()
    {
        if (scoreText) scoreText.text = "Score: " + score.ToString("0000");
        if (coinsText) coinsText.text = "Coins: " + coins;
        if (livesText) livesText.text = "Lives: " + lives;
    }
}
