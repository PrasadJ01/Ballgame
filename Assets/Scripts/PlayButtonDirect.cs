using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Button))]
public class PlayButtonFixed2 : MonoBehaviour
{
    [Tooltip("If true the script will load a scene by name. If false it will open a panel in this scene.")]
    public bool loadScene = false;

    [Tooltip("Exact scene name to load if loadScene = true")]
    public string sceneName = "Level_1";

    [Tooltip("If loadScene = false, assign the LevelSelect Panel GameObject to open it.")]
    public GameObject levelSelectPanel;

    Button btn;

    void Awake()
    {
        btn = GetComponent<Button>();
        if (btn == null)
        {
            Debug.LogError("[PlayButtonFixed2] Button component missing.");
            return;
        }

        // Ensure the button is interactable and raycast target is enabled
        btn.interactable = true;
        var img = GetComponent<Image>();
        if (img != null) img.raycastTarget = true;

        // Remove other listeners (optional) and add ours — safe single wiring
        btn.onClick.RemoveListener(OnClicked);
        btn.onClick.AddListener(OnClicked);

        Debug.Log($"[PlayButtonFixed2] Initialized on '{gameObject.name}'. loadScene={loadScene}, scene='{sceneName}', levelSelectPanel={(levelSelectPanel!=null)}");
    }

    void Start()
    {
        Debug.Log("[PlayButtonFixed2] Start() — ready to receive clicks.");
    }

    public void OnClicked()
    {
        Debug.Log("[PlayButtonFixed2] OnClicked() fired.");

        if (EventSystem.current == null) Debug.LogWarning("[PlayButtonFixed2] No EventSystem found — UI clicks may not work.");

        if (loadScene)
        {
            // load scene path check
            bool inBuild = false;
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(i);
                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                if (name == sceneName) { inBuild = true; break; }
            }

            if (!inBuild)
            {
                Debug.LogError($"[PlayButtonFixed2] Scene '{sceneName}' is not in Build Settings. Add it via File->Build Settings->Add Open Scenes.");
                return;
            }

            Debug.Log($"[PlayButtonFixed2] Loading scene '{sceneName}'...");
            Time.timeScale = 1f;
            SceneManager.LoadScene(sceneName);
            return;
        }

        // open panel mode
        if (levelSelectPanel == null)
        {
            Debug.LogError("[PlayButtonFixed2] levelSelectPanel not assigned. Assign the Levels panel GameObject in inspector to open it.");
            return;
        }

        // Activate and bring to front
        levelSelectPanel.SetActive(true);
        BringPanelToFront(levelSelectPanel);

        Debug.Log("[PlayButtonFixed2] Activated levelSelectPanel: " + levelSelectPanel.name);
    }

    void BringPanelToFront(GameObject panel)
    {
        // make sure panel is last sibling in canvas so it renders on top
        var rt = panel.transform as RectTransform;
        if (rt != null) rt.SetAsLastSibling();

        // ensure the panel's Canvas (or parent Canvas) has high sorting order
        Canvas canvas = panel.GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            canvas.overrideSorting = true;
            canvas.sortingOrder = 999;
        }

        // if panel has CanvasGroup that blocks raycasts, ensure it allows them
        var cg = panel.GetComponent<CanvasGroup>();
        if (cg == null) cg = panel.AddComponent<CanvasGroup>();
        cg.interactable = true;
        cg.blocksRaycasts = true;
        cg.alpha = 1f;
    }

    // run this from the Inspector (component gear -> Test PlayClick) to simulate a click
    [ContextMenu("Test PlayClick (Editor)")]
    void TestPlayClick()
    {
        Debug.Log("[PlayButtonFixed2] TestPlayClick invoked from Inspector.");
        OnClicked();
    }
}
