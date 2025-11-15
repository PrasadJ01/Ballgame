using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    [Header("Delay before scene loads (animation time)")]
    public float buttonDelay = 0.3f;   // Adjust based on your animation length

    public void PlayGame()
    {
        StartCoroutine(WaitAndLoad("LevelSelect"));
    }

    public void OpenLevel1()
    {
        StartCoroutine(WaitAndLoad("Level_1"));
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    private System.Collections.IEnumerator WaitAndLoad(string sceneName)
    {
        yield return new WaitForSeconds(buttonDelay);  
        SceneManager.LoadScene(sceneName);
    }
}
