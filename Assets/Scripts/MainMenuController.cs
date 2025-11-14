using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [Tooltip("Scene to load when Play pressed")]
    public string sceneNameToLoad = "Level_1";

    void Awake()
    {
        Debug.Log("[MainMenuController] Awake()");
    }

    void Start()
    {
        Debug.Log("[MainMenuController] Start() - ready. sceneNameToLoad=" + sceneNameToLoad);
    }

    // Called by the Play button
    public void PlayFirstLevel()
    {
        Debug.Log("[MainMenuController] PlayFirstLevel() called.");

        // quick check: is scene in build settings?
        bool inBuild = false;
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings; i++)
        {
            string path = UnityEngine.SceneManagement.SceneUtility.GetScenePathByBuildIndex(i);
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (name == sceneNameToLoad) { inBuild = true; break; }
        }
        Debug.Log("[MainMenuController] scene in build = " + inBuild);

        // Try to load (won't crash editor if missing, the log will show)
        if (inBuild)
        {
            Time.timeScale = 1f;
            Debug.Log("[MainMenuController] Loading scene: " + sceneNameToLoad);
            SceneManager.LoadScene(sceneNameToLoad);
        }
        else
        {
            Debug.LogWarning("[MainMenuController] Scene '" + sceneNameToLoad + "' is NOT in Build Settings. Add it via File → Build Settings → Add Open Scenes.");
        }
    }

    // small editor helper: call this from Inspector gear to confirm wiring (Context Menu)
    [ContextMenu("Test PlayFirstLevel() (Editor)")]
    void TestPlayFromInspector()
    {
        PlayFirstLevel();
    }
}
