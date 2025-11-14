using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Button))]
public class LevelButton : MonoBehaviour
{
    public TMP_Text label;         // assign in prefab
    public Image lockIcon;         // assign in prefab (optional)
    public Button button;          // cached Button

    int levelNumber = 1;

    void Awake()
    {
        button = GetComponent<Button>();
        if (button != null) button.onClick.RemoveAllListeners();
    }

    // Called by LevelSelectController when instantiating
    public void Setup(int level)
    {
        levelNumber = level;
        if (label != null) label.text = level.ToString();

        // Check if unlocked (Level 1 always unlocked)
        bool unlocked = (level == 1) || (PlayerPrefs.GetInt("LevelUnlocked_" + level, 0) == 1);
        if (lockIcon != null) lockIcon.gameObject.SetActive(!unlocked);
        if (button != null) button.interactable = unlocked;

        if (unlocked)
        {
            button.onClick.AddListener(() => LoadLevel());
        }
        else
        {
            // locked: could show buy prompt (for now you can still call Unlock for testing)
            button.onClick.AddListener(() => Debug.Log($"Level {levelNumber} is locked. Call shop to unlock."));
        }
    }

    void LoadLevel()
    {
        string sceneName = "Level_" + levelNumber; // e.g., Level_1
        // check in build settings
        bool inBuild = false;
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings; i++)
        {
            string path = UnityEngine.SceneManagement.SceneUtility.GetScenePathByBuildIndex(i);
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (name == sceneName) { inBuild = true; break; }
        }

        if (!inBuild)
        {
            Debug.LogError($"Scene '{sceneName}' not in Build Settings. Add it and try again.");
            return;
        }

        Debug.Log($"Loading scene {sceneName}");
        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneName);
    }
}
