using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections.Generic;

public class LevelSelectController : MonoBehaviour
{
    [Tooltip("Button prefab with Button component, a TMP_Text for label, and optional lock image reference.")]
    public GameObject levelButtonPrefab;
    [Tooltip("Parent transform with GridLayoutGroup or Vertical Layout")]
    public Transform contentParent;

    [Tooltip("Total number of levels in your game")]
    public int totalLevels = 10;

    List<GameObject> buttons = new List<GameObject>();

    void Start()
    {
        Populate();
    }

    void Populate()
    {
        // clear existing
        foreach (Transform t in contentParent) Destroy(t.gameObject);
        buttons.Clear();

        for (int i = 1; i <= totalLevels; i++)
        {
            GameObject go = Instantiate(levelButtonPrefab, contentParent);
            buttons.Add(go);

            // find components (you can set these in the prefab)
            Button btn = go.GetComponent<Button>();
            TMP_Text label = go.GetComponentInChildren<TMP_Text>();
            Image lockImg = go.transform.Find("Lock")?.GetComponent<Image>(); // optional child named Lock

            if (label != null) label.text = "Level " + i;

            int levelIndex = i; // capture for closure
            bool unlocked = IsLevelUnlocked(levelIndex);

            if (lockImg != null) lockImg.gameObject.SetActive(!unlocked);

            if (btn != null)
            {
                btn.interactable = unlocked;
                btn.onClick.RemoveAllListeners();

                if (unlocked)
                {
                    btn.onClick.AddListener(() => { LoadLevelByNumber(levelIndex); });
                }
                else
                {
                    // Show Buy / Unlock prompt; this example will call Purchase mock
                    btn.onClick.AddListener(() => { ShowPurchasePrompt(levelIndex); });
                }
            }
        }
    }

    public static bool IsLevelUnlocked(int levelNumber)
    {
        if (levelNumber <= 1) return true; // Level 1 always unlocked
        return PlayerPrefs.GetInt("LevelUnlocked_" + levelNumber, 0) == 1;
    }

    void LoadLevelByNumber(int levelNumber)
    {
        string name = $"Level_{levelNumber}";
        SceneManager.LoadScene(name);
    }

    void ShowPurchasePrompt(int levelNumber)
    {
        // Replace with real shop UI. For demo we'll simulate purchase
        Debug.Log($"[LevelSelect] Purchase needed to unlock Level {levelNumber}. Simulating buy...");
        SimulatePurchase(levelNumber);
    }

    // Demo purchase - in real game integrate platform IAP; after success call UnlockLevel()
    void SimulatePurchase(int levelNumber)
    {
        // You can show popup, then on confirmation:
        UnlockLevel(levelNumber);
        // update UI:
        Populate();
    }

    public static void UnlockLevel(int levelNumber)
    {
        PlayerPrefs.SetInt("LevelUnlocked_" + levelNumber, 1);
        PlayerPrefs.Save();
    }
}
