using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using System.Collections;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Stats")]
    public int score = 0;
    public int coins = 0;
    public int lives = 3;

    [Header("UI")]
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI coinsText;
    public TextMeshProUGUI livesText;
    public GameObject gameOverPanel;
    public Button replayButton;

    [Header("Settings")]
    public float restartDelay = 0.5f;

    private bool isGameOver = false;

    void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Ensure EventSystem exists so UI can be clicked
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

        if (replayButton != null)
        {
            replayButton.onClick.RemoveAllListeners();
            replayButton.onClick.AddListener(RestartGame);
            replayButton.interactable = true;
        }
    }

    // --- Public API used by other scripts ---
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

    public void AddLife(int amount = 1)
    {
        if (isGameOver) return;
        lives += amount;
        UpdateUI();
    }

    public void LoseLife(int amount = 1)
    {
        if (isGameOver) return;
        lives -= amount;
        if (lives <= 0)
        {
            lives = 0;
            UpdateUI();
            GameOver();
            return;
        }
        UpdateUI();
    }

    // --- Internal flow ---
    void GameOver()
    {
        isGameOver = true;

        if (gameOverPanel)
        {
            gameOverPanel.SetActive(true);
            Canvas.ForceUpdateCanvases();
        }

        if (replayButton)
        {
            replayButton.gameObject.SetActive(true);
            replayButton.interactable = true;
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(replayButton.gameObject);
        }

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

        Time.timeScale = 0f;
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        isGameOver = false;
        score = 0;
        coins = 0;
        lives = 3;
        if (gameOverPanel) gameOverPanel.SetActive(false);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void UpdateUI()
    {
        if (scoreText) scoreText.text = "Score: " + score.ToString("0000");
        if (coinsText) coinsText.text = "Coins: " + coins.ToString();
        if (livesText) livesText.text = "Lives: " + lives.ToString();
    }

#if UNITY_EDITOR
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            RestartGame();
        }
    }
#endif
}
