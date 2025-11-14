using UnityEngine;
using UnityEngine.SceneManagement;

// Small helper you can attach to any GameObject (e.g. MainMenuManager)
// It exposes two public methods you can call from the Inspector or wire to a temporary UI button.
// Use OpenPanel() to show a panel object in-scene.
// Use LoadSceneDirect() to load a named scene.
public class TestOpener : MonoBehaviour
{
    [Tooltip("Assign the LevelSelect panel here (or leave empty if using scene load)")]
    public GameObject levelSelectPanel;

    [Tooltip("Exact scene name to load when testing scene load")]
    public string testSceneName = "Level_1";

    // Call from Inspector context menu or from a UI Button OnClick
    [ContextMenu("OpenPanelNow")]
    public void OpenPanel()
    {
        if (levelSelectPanel == null)
        {
            Debug.LogError("[TestOpener] levelSelectPanel not assigned.");
            return;
        }
        levelSelectPanel.SetActive(true);
        // bring to front
        var rt = levelSelectPanel.transform as RectTransform;
        if (rt != null) rt.SetAsLastSibling();
        var canvas = levelSelectPanel.GetComponentInParent<Canvas>();
        if (canvas != null) { canvas.overrideSorting = true; canvas.sortingOrder = 999; }
        Debug.Log("[TestOpener] Opened panel: " + levelSelectPanel.name);
    }

    [ContextMenu("LoadSceneNow")]
    public void LoadSceneDirect()
    {
        // check build settings
        bool inBuild = false;
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (name == testSceneName) { inBuild = true; break; }
        }
        if (!inBuild)
        {
            Debug.LogError("[TestOpener] Scene '" + testSceneName + "' not in Build Settings.");
            return;
        }
        Debug.Log("[TestOpener] Loading scene: " + testSceneName);
        Time.timeScale = 1f;
        SceneManager.LoadScene(testSceneName);
    }
}
