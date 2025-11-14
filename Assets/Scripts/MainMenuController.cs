using UnityEngine;

public class MainMenuController : MonoBehaviour
{
    [Tooltip("Panel that contains the level buttons (GameObject under Canvas)")]
    public GameObject levelSelectPanel; // assign LevelSelect panel here

    void Start()
    {
        if (levelSelectPanel != null) levelSelectPanel.SetActive(false); // hide at start
    }

    // Called by Play button OnClick
    public void OpenLevelSelect()
    {
        if (levelSelectPanel != null)
        {
            levelSelectPanel.SetActive(true);
            // optional: set the first selectable button
            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(
                levelSelectPanel.transform.Find("Content/LevelButton_1")?.gameObject
            );
        }
    }

    // Optional: call this from a Back button inside levelSelectPanel
    public void CloseLevelSelect()
    {
        if (levelSelectPanel != null) levelSelectPanel.SetActive(false);
    }
}
