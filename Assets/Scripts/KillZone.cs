using UnityEngine;
using UnityEngine.SceneManagement;

public class Enemy : MonoBehaviour
{
    public GameObject gameOverText; // Assign your TMP "GameOverText" object here!

    void Start()
    {
        if (gameOverText != null)
            gameOverText.SetActive(false); // Ensure hidden at start
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("Game Over - Hit Enemy!");

            // Show Game Over text
            if (gameOverText != null)
                gameOverText.SetActive(true);

            // Stop all gameplay
            Time.timeScale = 0f;

            // If you want restart by button, do not auto reload.
            // If you want auto restart after delay:
            // StartCoroutine(RestartAfterDelay(2.0f));
        }
    }

    // Optional: Restore timescale before reloading
    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // For auto restart, uncomment below:
    // IEnumerator RestartAfterDelay(float delay)
    // {
    //     yield return new WaitForSecondsRealtime(delay);
    //     RestartGame();
    // }
}
