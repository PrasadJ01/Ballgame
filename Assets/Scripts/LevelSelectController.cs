using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LevelSelectController : MonoBehaviour
{
    [Tooltip("Prefab for each level button (must have LevelButton component)")]
    public GameObject levelButtonPrefab;
    [Tooltip("Parent (Content) transform where buttons are instantiated")]
    public Transform contentParent;
    [Tooltip("How many levels you have")]
    public int totalLevels = 9;

    void Start()
    {
        PopulateButtons();
    }

    public void PopulateButtons()
    {
        if (levelButtonPrefab == null || contentParent == null) return;

        // Clear existing
        foreach (Transform t in contentParent) Destroy(t.gameObject);

        for (int i = 1; i <= totalLevels; i++)
        {
            GameObject go = Instantiate(levelButtonPrefab, contentParent, false);
            var lb = go.GetComponent<LevelButton>();
            if (lb != null) lb.Setup(i);
            // name helpful for debug / selection
            go.name = "LevelButton_" + i;
        }
    }
}
