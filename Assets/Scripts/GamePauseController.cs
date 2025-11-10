using UnityEngine;
using UnityEngine.UI;

public class GamePauseController : MonoBehaviour
{
    [Header("Button References")]
    public Button playButton;
    public Button pauseButton;

    private bool isPaused = false;

    void Start()
    {
        if (playButton != null)
            playButton.onClick.AddListener(ResumeGame);
        if (pauseButton != null)
            pauseButton.onClick.AddListener(PauseGame);

        // Hide Play button at start
        playButton.gameObject.SetActive(false);
        pauseButton.gameObject.SetActive(true);

        Time.timeScale = 1f;
    }

    public void PauseGame()
    {
        if (isPaused) return;

        isPaused = true;
        Time.timeScale = 0f;
        playButton.gameObject.SetActive(true);
        pauseButton.gameObject.SetActive(false);

        Debug.Log("⏸ Game Paused");
    }

    public void ResumeGame()
    {
        if (!isPaused) return;

        isPaused = false;
        Time.timeScale = 1f;
        playButton.gameObject.SetActive(false);
        pauseButton.gameObject.SetActive(true);

        Debug.Log("▶ Game Resumed");
    }

    void Update()
    {
        // Optional: keyboard shortcut
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPaused) ResumeGame();
            else PauseGame();
        }
    }
}
