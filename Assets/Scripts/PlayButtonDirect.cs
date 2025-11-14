using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(UnityEngine.UI.Button))]
public class PlayButtonDirect : MonoBehaviour, IPointerClickHandler
{
    [Tooltip("Exact scene name to load (must match Build Settings)")]
    public string sceneName = "Level_1";

    void Start()
    {
        Debug.Log($"[PlayButtonDirect] Ready on GameObject '{gameObject.name}'. sceneName='{sceneName}'");
    }

    // Called when pointer (mouse/touch) clicks this UI element
    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log($"[PlayButtonDirect] Click detected. Attempting to load scene '{sceneName}'.");

        // quick safety: check if in build settings
        bool inBuild = false;
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (name == sceneName) { inBuild = true; break; }
        }

        if (!inBuild)
        {
            Debug.LogError($"[PlayButtonDirect] Scene '{sceneName}' is NOT in Build Settings. Add it via File -> Build Settings -> Add Open Scenes.");
            return;
        }

        // Ensure time scale is normal
        Time.timeScale = 1f;

        // Load the scene
        SceneManager.LoadScene(sceneName);
    }
}
