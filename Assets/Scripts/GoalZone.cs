using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GoalZone : MonoBehaviour
{
    public GameObject youWinText; // Assign your "You Win" prefab here

    void Start()
    {
        if (youWinText != null)
            youWinText.SetActive(false); // Hide at start
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("You Win!");

            // Show "You Win" UI
            if (youWinText != null)
                youWinText.SetActive(true);

            // Stop the game
            Time.timeScale = 0f;

            // Optional: auto restart after delay
            // StartCoroutine(RestartAfterDelay(3f));
        }
    }

    // Optional restart
    IEnumerator RestartAfterDelay(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
